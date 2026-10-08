using System.IO;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using CsxPad.Wpf.Scripting;
using CSharpScriptCore.Runtime;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CsxPad.Wpf.Services;

public static class CSharpSemanticCompletionService
{
    private static readonly Lazy<IReadOnlyList<MetadataReference>> PlatformReferences = new(CreatePlatformReferences);
    private static readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, string>> XmlDocumentationCache =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly SymbolDisplayFormat QuickInfoFormat = new(
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        memberOptions:
            SymbolDisplayMemberOptions.IncludeContainingType |
            SymbolDisplayMemberOptions.IncludeExplicitInterface |
            SymbolDisplayMemberOptions.IncludeParameters |
            SymbolDisplayMemberOptions.IncludeType,
        parameterOptions:
            SymbolDisplayParameterOptions.IncludeType |
            SymbolDisplayParameterOptions.IncludeName |
            SymbolDisplayParameterOptions.IncludeDefaultValue |
            SymbolDisplayParameterOptions.IncludeParamsRefOut,
        miscellaneousOptions:
            SymbolDisplayMiscellaneousOptions.UseSpecialTypes |
            SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
            SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    public static IReadOnlyList<SemanticMemberCompletion> GetMembers(
        string script,
        int dotOffset,
        IEnumerable<string> packageReferencePaths,
        string? scriptPath = null)
        => GetMemberResult(script, dotOffset, packageReferencePaths, scriptPath).Members;

    public static SemanticMemberCompletionResult GetMemberResult(
        string script,
        int dotOffset,
        IEnumerable<string> packageReferencePaths,
        string? scriptPath = null)
    {
        try
        {
            var context = CreateContext(script, packageReferencePaths, scriptPath);
            var memberAccess = context.Root.DescendantNodes()
                .OfType<MemberAccessExpressionSyntax>()
                .Where(expression => expression.OperatorToken.SpanStart == dotOffset)
                .OrderBy(expression => expression.Span.Length)
                .FirstOrDefault();
            if (memberAccess is null)
            {
                return SemanticMemberCompletionResult.Unresolved;
            }

            var receiverSymbol = context.SemanticModel.GetSymbolInfo(memberAccess.Expression).Symbol;
            if (receiverSymbol is IAliasSymbol alias)
            {
                receiverSymbol = alias.Target;
            }

            if (receiverSymbol is INamespaceSymbol receiverNamespace)
            {
                var namespaceMembers = receiverNamespace.GetMembers()
                    .Where(symbol => IsAccessibleCompletionSymbol(symbol, context.SemanticModel, dotOffset))
                    .Select(CreateNamespaceCompletion)
                    .Where(completion => completion is not null)
                    .Cast<SemanticMemberCompletion>()
                    .GroupBy(completion => $"{completion.Kind}:{completion.Name}", StringComparer.Ordinal)
                    .Select(group => group.First())
                    .OrderBy(completion => completion.Name, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                return new SemanticMemberCompletionResult(true, namespaceMembers, SemanticReceiverKind.Namespace);
            }

            var receiverType = context.SemanticModel.GetTypeInfo(memberAccess.Expression).Type;
            if (receiverType is null || receiverType.TypeKind == TypeKind.Error)
            {
                return SemanticMemberCompletionResult.Unresolved;
            }

            var receiverKind = receiverSymbol is INamedTypeSymbol
                ? SemanticReceiverKind.Type
                : SemanticReceiverKind.Instance;
            var members = context.SemanticModel
                .LookupSymbols(
                    Math.Min(dotOffset + 1, script.Length),
                    receiverType,
                    includeReducedExtensionMethods: true)
                .Where(symbol => IsAccessibleCompletionSymbol(symbol, context.SemanticModel, dotOffset))
                .Where(symbol => IsValidForReceiver(symbol, receiverKind))
                .Select(CreateCompletion)
                .Where(completion => completion is not null)
                .Cast<SemanticMemberCompletion>()
                .GroupBy(completion => $"{completion.Kind}:{completion.Name}", StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderBy(completion => completion.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            return new SemanticMemberCompletionResult(true, members, receiverKind);
        }
        catch (Exception)
        {
            return SemanticMemberCompletionResult.Unresolved;
        }
    }

    public static IReadOnlyList<SemanticRootCompletion> GetVisibleSymbols(
        string script,
        int position,
        IEnumerable<string> packageReferencePaths,
        string? scriptPath = null)
    {
        try
        {
            var context = CreateContext(script, packageReferencePaths, scriptPath);
            var lookupPosition = Math.Clamp(position, 0, script.Length);
            return context.SemanticModel.LookupSymbols(lookupPosition)
                .Where(symbol => IsAccessibleCompletionSymbol(symbol, context.SemanticModel, lookupPosition))
                .Select(CreateRootCompletion)
                .Where(completion => completion is not null)
                .Cast<SemanticRootCompletion>()
                .GroupBy(completion => $"{completion.Kind}:{completion.Name}", StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderBy(completion => completion.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (Exception)
        {
            return [];
        }
    }

    public static IReadOnlyList<SemanticDiagnostic> GetDiagnostics(
        string script,
        IEnumerable<string> packageReferencePaths,
        CancellationToken cancellationToken = default,
        string? scriptPath = null)
    {
        try
        {
            var context = CreateContext(script, packageReferencePaths, scriptPath, cancellationToken);
            return context.Compilation
                .GetDiagnostics(cancellationToken)
                .Where(diagnostic =>
                    diagnostic.Location.IsInSource &&
                    ReferenceEquals(diagnostic.Location.SourceTree, context.SemanticModel.SyntaxTree) &&
                    diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)
                .Select(diagnostic =>
                {
                    var span = diagnostic.Location.SourceSpan;
                    var start = Math.Clamp(span.Start, 0, script.Length);
                    var availableLength = Math.Max(0, script.Length - start);
                    var length = Math.Min(Math.Max(1, span.Length), availableLength);
                    return new SemanticDiagnostic(
                        start,
                        length,
                        diagnostic.Severity == DiagnosticSeverity.Error ? "error" : "warning",
                        diagnostic.Id,
                        EnhanceDiagnosticMessage(diagnostic));
                })
                .Where(diagnostic => diagnostic.Length > 0)
                .OrderBy(diagnostic => diagnostic.Offset)
                .ThenBy(diagnostic => diagnostic.Severity, StringComparer.Ordinal)
                .ToArray();
        }
        catch (OperationCanceledException)
        {
            return [];
        }
        catch (Exception)
        {
            return [];
        }
    }

    public static SemanticQuickInfo? GetQuickInfo(
        string script,
        int offset,
        IEnumerable<string> packageReferencePaths,
        string? scriptPath = null)
    {
        if (string.IsNullOrEmpty(script))
        {
            return null;
        }

        try
        {
            var context = CreateContext(script, packageReferencePaths, scriptPath);
            var position = Math.Clamp(offset, 0, script.Length - 1);
            var token = context.Root.FindToken(position, findInsideTrivia: true);
            var name = token.Parent?.AncestorsAndSelf()
                .OfType<SimpleNameSyntax>()
                .FirstOrDefault(node => node.Span.Contains(position) || node.Span.End == offset);
            if (name is null)
            {
                return null;
            }

            var invocation = name.AncestorsAndSelf()
                .OfType<InvocationExpressionSyntax>()
                .FirstOrDefault(node => node.Expression.Span.Contains(name.Span));
            var objectCreation = name.AncestorsAndSelf()
                .OfType<ObjectCreationExpressionSyntax>()
                .FirstOrDefault(node => node.Type.Span.Contains(name.Span));
            var symbolInfo = invocation is not null
                ? context.SemanticModel.GetSymbolInfo(invocation)
                : objectCreation is not null
                    ? context.SemanticModel.GetSymbolInfo(objectCreation)
                    : context.SemanticModel.GetSymbolInfo(name);

            var symbols = new List<ISymbol>();
            if (symbolInfo.Symbol is not null)
            {
                symbols.Add(symbolInfo.Symbol);
            }

            symbols.AddRange(symbolInfo.CandidateSymbols);
            symbols.AddRange(context.SemanticModel.GetMemberGroup(name));
            symbols = symbols
                .Distinct(SymbolEqualityComparer.Default)
                .ToList();
            if (symbols.Count == 0)
            {
                return null;
            }

            var primary = symbolInfo.Symbol ?? symbols[0];
            var methods = symbols.OfType<IMethodSymbol>().ToList();
            if (primary is IMethodSymbol primaryMethod)
            {
                methods.AddRange(primaryMethod.ContainingType
                    .GetMembers(primaryMethod.Name)
                    .OfType<IMethodSymbol>()
                    .Where(method => method.DeclaredAccessibility == Microsoft.CodeAnalysis.Accessibility.Public));
            }

            var overloads = methods
                .Select(FormatSymbol)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(signature => signature, StringComparer.Ordinal)
                .ToArray();
            var documentation = ReadDocumentation(primary, packageReferencePaths);
            var parameterDocumentation = primary is IMethodSymbol method
                ? method.Parameters
                    .Select(parameter => new SemanticParameterDocumentation(
                        parameter.Name,
                        documentation.Parameters.TryGetValue(parameter.Name, out var description)
                            ? description
                            : string.Empty))
                    .Where(parameter => parameter.Description.Length > 0)
                    .ToArray()
                : [];

            return new SemanticQuickInfo(
                FormatSymbol(primary),
                documentation.Summary,
                overloads,
                parameterDocumentation);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static SemanticContext CreateContext(
        string script,
        IEnumerable<string> packageReferencePaths,
        string? scriptPath,
        CancellationToken cancellationToken = default)
    {
        var source = CSharpScriptDirectiveService.PrepareForEditorAnalysis(script);
        var fullScriptPath = string.IsNullOrWhiteSpace(scriptPath) ? string.Empty : Path.GetFullPath(scriptPath);
        var baseDirectory = fullScriptPath.Length == 0
            ? Environment.CurrentDirectory
            : Path.GetDirectoryName(fullScriptPath) ?? Environment.CurrentDirectory;
        var syntaxTree = CSharpSyntaxTree.ParseText(
            source,
            new CSharpParseOptions(LanguageVersion.Preview, kind: SourceCodeKind.Script),
            path: fullScriptPath,
            cancellationToken: cancellationToken);
        var references = PlatformReferences.Value
            .Concat(CreateReferences(packageReferencePaths))
            .Concat(CreateReferences([typeof(ScriptRuntime).Assembly.Location]))
            .GroupBy(reference => reference.Display, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
        var options = new CSharpCompilationOptions(
            OutputKind.DynamicallyLinkedLibrary,
            usings:
            [
                "System",
                "System.Collections.Generic",
                "System.Linq",
                "System.Threading",
                "System.Threading.Tasks",
                "CSharpScriptCore.Runtime"
            ],
            sourceReferenceResolver: new EditorScriptSourceResolver(
                new SourceFileResolver(ImmutableArray<string>.Empty, baseDirectory)));
        var compilation = CSharpCompilation.CreateScriptCompilation(
            "CsxPad.EditorAnalysis",
            syntaxTree,
            references,
            options,
            previousScriptCompilation: null,
            returnType: typeof(object),
            globalsType: null);
        var root = syntaxTree.GetRoot(cancellationToken);
        var semanticModel = compilation.GetSemanticModel(syntaxTree, ignoreAccessibility: true);
        return new SemanticContext(compilation, semanticModel, root);
    }

    private static IEnumerable<MetadataReference> CreateReferences(IEnumerable<string> paths)
    {
        foreach (var path in paths.Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            MetadataReference reference;
            try
            {
                reference = MetadataReference.CreateFromFile(path);
            }
            catch (IOException)
            {
                continue;
            }

            yield return reference;
        }
    }

    private static string FormatSymbol(ISymbol symbol) => symbol.ToDisplayString(QuickInfoFormat);

    private static SymbolDocumentation ReadDocumentation(ISymbol symbol, IEnumerable<string> referencePaths)
    {
        var documentedSymbol = symbol is IMethodSymbol { ReducedFrom: not null } reduced
            ? reduced.ReducedFrom
            : symbol;
        var xml = documentedSymbol.GetDocumentationCommentXml(expandIncludes: true);
        var documentationSets = referencePaths
            .Select(path => Path.ChangeExtension(path, ".xml"))
            .Where(path => path is not null && File.Exists(path))
            .Select(path => GetXmlDocumentation(path!))
            .ToArray();
        if (string.IsNullOrWhiteSpace(xml))
        {
            var documentationId = documentedSymbol.GetDocumentationCommentId();
            if (documentationId is not null)
            {
                xml = FindXmlDocumentation(documentationId, documentationSets);
            }
        }

        if (string.IsNullOrWhiteSpace(xml))
        {
            return SymbolDocumentation.Empty;
        }

        try
        {
            xml = ResolveInheritedDocumentation(xml, documentationSets, []);
            var root = XElement.Parse($"<root>{xml}</root>");
            var summary = NormalizeDocumentation(root.Descendants("summary").FirstOrDefault()?.Value);
            var parameters = root.Descendants("param")
                .Where(element => element.Attribute("name") is not null)
                .GroupBy(element => element.Attribute("name")!.Value, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => NormalizeDocumentation(group.First().Value),
                    StringComparer.Ordinal);
            return new SymbolDocumentation(summary, parameters);
        }
        catch
        {
            return SymbolDocumentation.Empty;
        }
    }

    private static string? FindXmlDocumentation(
        string documentationId,
        IEnumerable<IReadOnlyDictionary<string, string>> documentationSets) =>
        documentationSets
            .Select(set => set.TryGetValue(documentationId, out var value) ? value : null)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static string ResolveInheritedDocumentation(
        string xml,
        IReadOnlyList<IReadOnlyDictionary<string, string>> documentationSets,
        HashSet<string> visitedIds)
    {
        var root = XElement.Parse($"<root>{xml}</root>");
        if (root.Descendants("summary").Any())
        {
            return xml;
        }

        var inheritedId = root.Descendants("inheritdoc")
            .Select(element => element.Attribute("cref")?.Value)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        if (inheritedId is null || !visitedIds.Add(inheritedId))
        {
            return xml;
        }

        var inheritedXml = FindXmlDocumentation(inheritedId, documentationSets);
        return string.IsNullOrWhiteSpace(inheritedXml)
            ? xml
            : ResolveInheritedDocumentation(inheritedXml, documentationSets, visitedIds);
    }

    private static IReadOnlyDictionary<string, string> GetXmlDocumentation(string path) =>
        XmlDocumentationCache.GetOrAdd(path, static xmlPath =>
        {
            try
            {
                var document = XDocument.Load(xmlPath, LoadOptions.PreserveWhitespace);
                return document.Root?
                    .Element("members")?
                    .Elements("member")
                    .Where(element => element.Attribute("name") is not null)
                    .GroupBy(element => element.Attribute("name")!.Value, StringComparer.Ordinal)
                    .ToDictionary(
                        group => group.Key,
                        group => group.First().ToString(SaveOptions.DisableFormatting),
                        StringComparer.Ordinal)
                    ?? new Dictionary<string, string>(StringComparer.Ordinal);
            }
            catch
            {
                return new Dictionary<string, string>(StringComparer.Ordinal);
            }
        });

    private static string NormalizeDocumentation(string? value) => string.IsNullOrWhiteSpace(value)
        ? string.Empty
        : Regex.Replace(value, @"\s+", " ").Trim();

    private static SemanticMemberCompletion? CreateCompletion(ISymbol symbol) => symbol switch
    {
        IMethodSymbol method when method.MethodKind is not MethodKind.Constructor and not MethodKind.StaticConstructor =>
            new SemanticMemberCompletion(
                method.Name,
                method.ReducedFrom is null ? "method" : "extension",
                method.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                method.Arity),
        IPropertySymbol property => new SemanticMemberCompletion(
            property.Name,
            "property",
            property.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
            0),
        IFieldSymbol field => new SemanticMemberCompletion(
            field.Name,
            "field",
            field.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
            0),
        IEventSymbol eventSymbol => new SemanticMemberCompletion(
            eventSymbol.Name,
            "event",
            eventSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
            0),
        _ => null
    };

    private static SemanticRootCompletion? CreateRootCompletion(ISymbol symbol) => symbol switch
    {
        ILocalSymbol local => new SemanticRootCompletion(
            local.Name, "local", local.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)),
        IParameterSymbol parameter => new SemanticRootCompletion(
            parameter.Name, "parameter", parameter.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)),
        IRangeVariableSymbol range => new SemanticRootCompletion(range.Name, "local", range.Name),
        IFieldSymbol field when !field.IsImplicitlyDeclared => new SemanticRootCompletion(
            field.Name, "field", field.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)),
        IPropertySymbol property when !property.IsImplicitlyDeclared => new SemanticRootCompletion(
            property.Name, "property", property.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)),
        IMethodSymbol method when method.MethodKind is MethodKind.Ordinary or MethodKind.LocalFunction =>
            new SemanticRootCompletion(
                method.Name,
                "method",
                method.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                method.Arity),
        INamedTypeSymbol type when !type.IsImplicitlyDeclared => new SemanticRootCompletion(
            type.Name,
            type.TypeKind.ToString().ToLowerInvariant(),
            type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
            type.Arity),
        _ => null
    };

    private static SemanticMemberCompletion? CreateNamespaceCompletion(ISymbol symbol) => symbol switch
    {
        INamespaceSymbol typeNamespace => new SemanticMemberCompletion(
            typeNamespace.Name, "namespace", typeNamespace.ToDisplayString(), 0),
        INamedTypeSymbol type => new SemanticMemberCompletion(
            type.Name,
            type.TypeKind.ToString().ToLowerInvariant(),
            type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
            type.Arity),
        _ => null
    };

    private static bool IsValidForReceiver(ISymbol symbol, SemanticReceiverKind receiverKind)
    {
        if (symbol is INamedTypeSymbol)
        {
            return receiverKind == SemanticReceiverKind.Type;
        }

        return receiverKind switch
        {
            SemanticReceiverKind.Type => symbol.IsStatic,
            SemanticReceiverKind.Instance => !symbol.IsStatic || symbol is IMethodSymbol { ReducedFrom: not null },
            _ => false
        };
    }

    private static bool IsAccessibleCompletionSymbol(ISymbol symbol, SemanticModel semanticModel, int position) =>
        !symbol.IsImplicitlyDeclared &&
        symbol.CanBeReferencedByName &&
        semanticModel.IsAccessible(Math.Clamp(position, 0, semanticModel.SyntaxTree.Length), symbol);

    private static string EnhanceDiagnosticMessage(Diagnostic diagnostic)
    {
        var message = diagnostic.GetMessage();
        return diagnostic.Id == "CS8345"
            ? $"{message} CSX 顶层变量会保存为脚本状态对象的字段；请把 Span、ReadOnlySpan 或其他 ref struct 的使用移到方法体内。"
            : message;
    }

    private static IReadOnlyList<MetadataReference> CreatePlatformReferences()
    {
        var trustedAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (string.IsNullOrWhiteSpace(trustedAssemblies))
        {
            return [];
        }

        return trustedAssemblies
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Where(File.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => MetadataReference.CreateFromFile(path))
            .ToArray();
    }

    private sealed record SemanticContext(
        CSharpCompilation Compilation,
        SemanticModel SemanticModel,
        SyntaxNode Root);

    private sealed record SymbolDocumentation(
        string Summary,
        IReadOnlyDictionary<string, string> Parameters)
    {
        public static SymbolDocumentation Empty { get; } = new(
            string.Empty,
            new Dictionary<string, string>(StringComparer.Ordinal));
    }
}

public sealed record SemanticMemberCompletion(
    string Name,
    string Kind,
    string Description,
    int GenericArity);

public sealed record SemanticMemberCompletionResult(
    bool ReceiverResolved,
    IReadOnlyList<SemanticMemberCompletion> Members,
    SemanticReceiverKind ReceiverKind = SemanticReceiverKind.Unknown)
{
    public static SemanticMemberCompletionResult Unresolved { get; } = new(false, []);
}

public enum SemanticReceiverKind
{
    Unknown,
    Namespace,
    Type,
    Instance
}

public sealed record SemanticRootCompletion(
    string Name,
    string Kind,
    string Description,
    int GenericArity = 0);

public sealed record SemanticDiagnostic(
    int Offset,
    int Length,
    string Severity,
    string Id,
    string Message);

public sealed record SemanticQuickInfo(
    string Signature,
    string Summary,
    IReadOnlyList<string> Overloads,
    IReadOnlyList<SemanticParameterDocumentation> Parameters);

public sealed record SemanticParameterDocumentation(string Name, string Description);
