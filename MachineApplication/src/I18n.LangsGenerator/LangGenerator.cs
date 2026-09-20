namespace I18n.LangsGenerator;

using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

[Generator(LanguageNames.CSharp)]
public sealed class LangGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor InvalidFile = new(
        "LANG001", "Invalid language file", "Language file '{0}': {1}",
        "I18n", DiagnosticSeverity.Error, true);
    private static readonly DiagnosticDescriptor InvalidClass = new(
        "LANG002", "Invalid language class", "Language class '{0}': {1}",
        "I18n", DiagnosticSeverity.Error, true);
    private static readonly DiagnosticDescriptor InvalidKey = new(
        "LANG003", "Invalid language key", "Language key '{0}' in '{1}': {2}",
        "I18n", DiagnosticSeverity.Error, true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var files = context.AdditionalTextsProvider
            .Where(file => IsLanguageFile(file.Path))
            .Select((file, token) => new LanguageFile(file.Path, file.GetText(token)?.ToString()))
            .Collect();
        var classes = context.SyntaxProvider.CreateSyntaxProvider(
                (node, _) => node is ClassDeclarationSyntax declaration && declaration.BaseList != null
                             && declaration.Modifiers.Any(SyntaxKind.PartialKeyword),
                (syntax, token) => syntax.SemanticModel.GetDeclaredSymbol(
                    (ClassDeclarationSyntax)syntax.Node, token) as INamedTypeSymbol)
            .Where(symbol => symbol != null && InheritsLangBase(symbol))
            .Collect();
        context.RegisterSourceOutput(files.Combine(classes),
            (production, input) => Generate(production, input.Left, input.Right));
    }

    private static bool IsLanguageFile(string path)
    {
        var name = Path.GetFileName(path);
        return name.Length >= 10 && name.StartsWith("lang.", StringComparison.OrdinalIgnoreCase)
               && name.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
    }

    private static bool InheritsLangBase(INamedTypeSymbol symbol)
    {
        for (var parent = symbol.BaseType; parent != null; parent = parent.BaseType)
            if (parent.ToDisplayString() == "I18nExtensions.LangBase") return true;
        return false;
    }

    private static void Generate(SourceProductionContext context, ImmutableArray<LanguageFile> files,
        ImmutableArray<INamedTypeSymbol?> classes)
    {
        if (files.IsEmpty || classes.IsEmpty) return;
        var translations = new SortedDictionary<string, SortedDictionary<string, string>>(StringComparer.Ordinal);
        var cultures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files.OrderBy(file => file.Path, StringComparer.Ordinal))
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            var name = Path.GetFileName(file.Path);
            var culture = name.Substring(5, name.Length - 10);
            try
            {
                if (string.IsNullOrWhiteSpace(culture)) throw new FormatException("Culture must not be empty.");
                CultureInfo.GetCultureInfo(culture);
                if (!cultures.Add(culture)) throw new FormatException("Duplicate culture.");
                if (file.Text == null) throw new FormatException("Cannot read file.");
                var root = JObject.Parse(file.Text, new JsonLoadSettings
                {
                    DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error
                });
                var entries = new SortedDictionary<string, string>(StringComparer.Ordinal);
                ReadObject(root, null, entries);
                translations.Add(culture, entries);
            }
            catch (Exception error) when (error is JsonException || error is ArgumentException || error is FormatException)
            {
                context.ReportDiagnostic(Diagnostic.Create(InvalidFile, Location.None, file.Path, error.Message));
            }
        }

        var duplicateNames = new HashSet<string>(classes.OfType<INamedTypeSymbol>()
            .Distinct(SymbolEqualityComparer.Default)
            .GroupBy(symbol => symbol!.Name, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key), StringComparer.Ordinal);
        var seen = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        foreach (var candidate in classes)
        {
            if (candidate == null || !seen.Add(candidate)) continue;
            context.CancellationToken.ThrowIfCancellationRequested();
            EmitClass(context, candidate, translations, duplicateNames.Contains(candidate.Name));
        }
    }

    private static void ReadObject(JObject source, string? prefix, IDictionary<string, string> entries)
    {
        foreach (var property in source.Properties())
        {
            var key = prefix == null ? property.Name : prefix + "_" + property.Name;
            if (property.Value is JObject child)
            {
                ReadObject(child, key, entries);
                continue;
            }
            if (property.Value.Type != JTokenType.String)
                throw new FormatException($"'{property.Path}' must be a string or an object.");
            if (entries.ContainsKey(key))
                throw new FormatException($"Duplicate flattened key '{key}' at '{property.Path}'.");
            entries.Add(key, property.Value.Value<string>()!);
        }
    }

    private static void EmitClass(SourceProductionContext context, INamedTypeSymbol symbol,
        SortedDictionary<string, SortedDictionary<string, string>> translations, bool useQualifiedName)
    {
        var hierarchy = new Stack<INamedTypeSymbol>();
        for (var current = symbol; current != null; current = current.ContainingType)
        {
            if (current.DeclaringSyntaxReferences.Any(reference =>
                    reference.GetSyntax(context.CancellationToken) is not ClassDeclarationSyntax declaration
                    || !declaration.Modifiers.Any(SyntaxKind.PartialKeyword)
                    || declaration.Modifiers.Any(SyntaxKind.FileKeyword)))
            {
                context.ReportDiagnostic(Diagnostic.Create(InvalidClass, symbol.Locations.FirstOrDefault(),
                    symbol.Name, "The class and its containing types must be partial, non-file-local classes."));
                return;
            }
            hierarchy.Push(current);
        }
        if (symbol.GetMembers("Initialized").Length != 0 || symbol.GetMembers("Initialize").Length != 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidClass, symbol.Locations.FirstOrDefault(),
                symbol.Name, "Initialize and Initialized are generated; remove existing declarations."));
            return;
        }
        var hook = FindMember(symbol.BaseType, "Initialized") as IMethodSymbol;
        if (hook == null || hook.IsSealed || !(hook.IsVirtual || hook.IsOverride || hook.IsAbstract))
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidClass, symbol.Locations.FirstOrDefault(),
                symbol.Name, "The inherited Initialized method must be overridable."));
            return;
        }

        var keys = new SortedSet<string>(translations.Values.SelectMany(entries => entries.Keys), StringComparer.Ordinal);
        var validKeys = new List<string>();
        foreach (var key in keys)
        {
            if (!IsIdentifier(key) || key == "Initialize" || key == "Initialized"
                || hierarchy.Any(type => type.Name == key || type.TypeParameters.Any(parameter => parameter.Name == key))
                || FindMember(symbol, key) != null)
            {
                context.ReportDiagnostic(Diagnostic.Create(InvalidKey, symbol.Locations.FirstOrDefault(),
                    key, symbol.ToDisplayString(), "Expected a C# identifier that does not conflict with an existing member or type parameter."));
                continue;
            }
            validKeys.Add(key);
        }

        var code = new StringBuilder("// <auto-generated/>\n#nullable enable\n");
        var hasNamespace = !symbol.ContainingNamespace.IsGlobalNamespace;
        if (hasNamespace) code.Append("namespace ").Append(symbol.ContainingNamespace.ToDisplayString()).AppendLine(" {");
        foreach (var type in hierarchy)
        {
            code.Append("partial class ").Append(EscapeIdentifier(type.Name));
            if (type.TypeParameters.Length != 0)
                code.Append('<').Append(string.Join(", ", type.TypeParameters.Select(parameter => EscapeIdentifier(parameter.Name)))).Append('>');
            code.AppendLine(" {");
        }
        foreach (var key in validKeys)
        {
            var identifier = EscapeIdentifier(key);
            code.Append("    public string ").Append(identifier).Append(" => GetValue(nameof(")
                .Append(identifier).AppendLine("));");
        }
        code.AppendLine("    protected override void Initialized()\n    {");
        if (!hook.IsAbstract) code.AppendLine("        base.Initialized();");
        code.AppendLine("        Initialize();\n    }");
        code.AppendLine("    public void Initialize()\n    {");
        foreach (var translation in translations)
        foreach (var entry in translation.Value)
            code.Append("        SetLangInfo(").Append(Literal(translation.Key)).Append(", ")
                .Append(Literal(entry.Key)).Append(", ").Append(Literal(entry.Value)).AppendLine(");");
        code.AppendLine("    }");
        foreach (var _ in hierarchy) code.AppendLine("}");
        if (hasNamespace) code.AppendLine("}");
        var hint = symbol.Name;
        if (useQualifiedName)
        {
            hint = string.Join("+", hierarchy.Select(type => type.MetadataName.Replace('`', '-')));
            if (hasNamespace) hint = symbol.ContainingNamespace.ToDisplayString() + "." + hint;
        }
        var formattedCode = SyntaxFactory.ParseCompilationUnit(code.ToString())
            .NormalizeWhitespace(indentation: "    ", eol: "\n")
            .ToFullString();
        context.AddSource(hint + ".g.cs", SourceText.From(formattedCode + "\n", Encoding.UTF8));
    }

    private static ISymbol? FindMember(INamedTypeSymbol? symbol, string name)
    {
        for (var current = symbol; current != null; current = current.BaseType)
        {
            var member = current.GetMembers(name).FirstOrDefault();
            if (member != null) return member;
        }
        return null;
    }

    private static bool IsIdentifier(string value) => (SyntaxFacts.IsValidIdentifier(value)
        || SyntaxFacts.GetKeywordKind(value) != SyntaxKind.None)
        && SyntaxFactory.ParseToken(EscapeIdentifier(value)).ValueText == value;
    private static string EscapeIdentifier(string value) => SyntaxFacts.GetKeywordKind(value) != SyntaxKind.None
        || SyntaxFacts.GetContextualKeywordKind(value) != SyntaxKind.None ? "@" + value : value;
    private static string Literal(string value) => SymbolDisplay.FormatLiteral(value, true);

    private sealed class LanguageFile(string path, string? text)
    {
        public string Path { get; } = path;
        public string? Text { get; } = text;
    }
}
