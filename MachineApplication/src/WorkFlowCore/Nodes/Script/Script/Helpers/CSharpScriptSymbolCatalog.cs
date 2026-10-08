using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CsxPad.Wpf.Helpers;

public static class CSharpScriptSymbolCatalog
{
    public static bool IsCompletionContext(string script, int caretOffset)
    {
        if (string.IsNullOrEmpty(script) || caretOffset <= 0)
        {
            return true;
        }

        var syntaxTree = CSharpSyntaxTree.ParseText(
            script,
            new CSharpParseOptions(LanguageVersion.Preview, kind: SourceCodeKind.Script));
        var root = syntaxTree.GetRoot();
        var position = Math.Clamp(caretOffset - 1, 0, script.Length - 1);
        var token = root.FindToken(position, findInsideTrivia: true);
        if (token.IsKind(SyntaxKind.StringLiteralToken) ||
            token.IsKind(SyntaxKind.Utf8StringLiteralToken) ||
            token.IsKind(SyntaxKind.SingleLineRawStringLiteralToken) ||
            token.IsKind(SyntaxKind.MultiLineRawStringLiteralToken) ||
            token.IsKind(SyntaxKind.InterpolatedStringTextToken) ||
            token.IsKind(SyntaxKind.CharacterLiteralToken))
        {
            return false;
        }

        var trivia = root.FindTrivia(position, findInsideTrivia: true);
        if (trivia.Span.Contains(position) && trivia.Kind() is
            SyntaxKind.SingleLineCommentTrivia or
            SyntaxKind.MultiLineCommentTrivia or
            SyntaxKind.SingleLineDocumentationCommentTrivia or
            SyntaxKind.MultiLineDocumentationCommentTrivia or
            SyntaxKind.DisabledTextTrivia or
            SyntaxKind.PreprocessingMessageTrivia or
            SyntaxKind.SkippedTokensTrivia)
        {
            return false;
        }

        return token.Parent?.AncestorsAndSelf().Any(node => node is DirectiveTriviaSyntax) != true;
    }

    public static string? GetExpectedTypeName(string script, int caretOffset)
    {
        if (string.IsNullOrWhiteSpace(script) || caretOffset <= 0)
        {
            return null;
        }

        var syntaxTree = CSharpSyntaxTree.ParseText(
            script,
            new CSharpParseOptions(kind: SourceCodeKind.Script));
        var root = syntaxTree.GetRoot();
        var position = Math.Clamp(caretOffset - 1, 0, script.Length - 1);
        var objectCreation = root.FindToken(position, findInsideTrivia: true)
            .Parent?
            .AncestorsAndSelf()
            .OfType<ObjectCreationExpressionSyntax>()
            .FirstOrDefault();
        var declarationType = objectCreation?
            .Ancestors()
            .OfType<EqualsValueClauseSyntax>()
            .FirstOrDefault()?
            .Parent?
            .AncestorsAndSelf()
            .OfType<VariableDeclarationSyntax>()
            .FirstOrDefault()?
            .Type;
        if (declarationType is null || declarationType.IsVar)
        {
            return null;
        }

        var name = declarationType switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            GenericNameSyntax generic => generic.Identifier.ValueText,
            QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
            AliasQualifiedNameSyntax alias => alias.Name.Identifier.ValueText,
            _ => declarationType.ToString()
        };
        return string.IsNullOrWhiteSpace(name) ? null : name;
    }

    public static bool IsDeclarationIdentifier(string script, int caretOffset)
    {
        if (string.IsNullOrEmpty(script) || caretOffset <= 0)
        {
            return false;
        }

        var syntaxTree = CSharpSyntaxTree.ParseText(
            script,
            new CSharpParseOptions(kind: SourceCodeKind.Script));
        var root = syntaxTree.GetRoot();
        var position = Math.Clamp(caretOffset - 1, 0, script.Length - 1);
        var token = root.FindToken(position, findInsideTrivia: true);

        return token.Parent?.AncestorsAndSelf().Any(node => node switch
        {
            VariableDeclaratorSyntax variable => variable.Identifier.Span.Contains(position),
            ParameterSyntax parameter => parameter.Identifier.Span.Contains(position),
            ForEachStatementSyntax statement => statement.Identifier.Span.Contains(position),
            CatchDeclarationSyntax declaration => declaration.Identifier.Span.Contains(position),
            SingleVariableDesignationSyntax designation => designation.Identifier.Span.Contains(position),
            BaseTypeDeclarationSyntax declaration => declaration.Identifier.Span.Contains(position),
            DelegateDeclarationSyntax declaration => declaration.Identifier.Span.Contains(position),
            _ => false
        }) == true;
    }

    public static bool IsAutomaticCompletionContext(string script, int caretOffset)
    {
        if (!IsCompletionContext(script, caretOffset) || IsDeclarationIdentifier(script, caretOffset))
        {
            return false;
        }

        var syntaxTree = CSharpSyntaxTree.ParseText(
            script,
            new CSharpParseOptions(LanguageVersion.Preview, kind: SourceCodeKind.Script));
        var root = syntaxTree.GetRoot();
        var position = Math.Clamp(caretOffset - 1, 0, script.Length - 1);
        var token = root.FindToken(position, findInsideTrivia: true);
        var accessor = token.Parent?.AncestorsAndSelf().OfType<AccessorDeclarationSyntax>().FirstOrDefault();
        if (accessor is not null &&
            accessor.Body?.Span.Contains(position) != true &&
            accessor.ExpressionBody?.Span.Contains(position) != true)
        {
            return false;
        }

        var accessorList = token.Parent?.AncestorsAndSelf().OfType<AccessorListSyntax>().FirstOrDefault();
        if (accessorList is not null && !accessorList.Accessors.Any(item =>
                item.Body?.Span.Contains(position) == true || item.ExpressionBody?.Span.Contains(position) == true))
        {
            return false;
        }

        return token.Parent?.AncestorsAndSelf().Any(node => node switch
        {
            UsingDirectiveSyntax => true,
            NamespaceDeclarationSyntax declaration when declaration.Name.Span.Contains(position) => true,
            FileScopedNamespaceDeclarationSyntax declaration when declaration.Name.Span.Contains(position) => true,
            NameColonSyntax nameColon when nameColon.Name.Span.Contains(position) => true,
            NameEqualsSyntax nameEquals when nameEquals.Name.Span.Contains(position) => true,
            LabeledStatementSyntax statement when statement.Identifier.Span.Contains(position) => true,
            AttributeTargetSpecifierSyntax => true,
            _ => false
        }) != true;
    }

    public static bool IsMemberAccessDot(string script, int dotOffset)
    {
        if (string.IsNullOrEmpty(script) || dotOffset < 0 || dotOffset >= script.Length || script[dotOffset] != '.')
        {
            return false;
        }

        var syntaxTree = CSharpSyntaxTree.ParseText(
            script,
            new CSharpParseOptions(LanguageVersion.Preview, kind: SourceCodeKind.Script));
        var root = syntaxTree.GetRoot();
        return root.DescendantNodes()
            .OfType<MemberAccessExpressionSyntax>()
            .Any(expression => expression.OperatorToken.SpanStart == dotOffset);
    }

    public static ScriptSnippetContext? GetSnippetContext(string script, int caretOffset)
    {
        if (string.IsNullOrEmpty(script) || caretOffset <= 0)
        {
            return null;
        }

        var syntaxTree = CSharpSyntaxTree.ParseText(
            script,
            new CSharpParseOptions(LanguageVersion.Preview, kind: SourceCodeKind.Script));
        var root = syntaxTree.GetRoot();
        var position = Math.Clamp(caretOffset - 1, 0, script.Length - 1);
        var token = root.FindToken(position, findInsideTrivia: true);
        var type = token.Parent?.AncestorsAndSelf()
            .OfType<TypeDeclarationSyntax>()
            .FirstOrDefault(declaration =>
                declaration.OpenBraceToken.SpanStart < caretOffset &&
                (declaration.CloseBraceToken.IsMissing || caretOffset <= declaration.CloseBraceToken.SpanStart));
        if (type is null || type is InterfaceDeclarationSyntax)
        {
            return null;
        }

        var nestedExecutableContext = token.Parent?.AncestorsAndSelf()
            .TakeWhile(node => node != type)
            .Any(node => node is BlockSyntax or ArrowExpressionClauseSyntax or EqualsValueClauseSyntax or
                AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax) == true;
        return nestedExecutableContext
            ? null
            : new ScriptSnippetContext(type.Identifier.ValueText);
    }

    public static ScriptDeclarationContext GetDeclarationContext(string script, int caretOffset)
    {
        if (!IsAutomaticCompletionContext(script, caretOffset) || string.IsNullOrEmpty(script))
        {
            return ScriptDeclarationContext.None;
        }

        var syntaxTree = CSharpSyntaxTree.ParseText(
            script,
            new CSharpParseOptions(LanguageVersion.Preview, kind: SourceCodeKind.Script));
        var root = syntaxTree.GetRoot();
        var position = Math.Clamp(caretOffset - 1, 0, script.Length - 1);
        var ancestors = root.FindToken(position, findInsideTrivia: true)
            .Parent?
            .AncestorsAndSelf()
            .ToArray() ?? [];

        if (ancestors.Any(node => node is ParameterSyntax or ParameterListSyntax or
                BracketedParameterListSyntax))
        {
            return ScriptDeclarationContext.Parameter;
        }

        var type = ancestors.OfType<TypeDeclarationSyntax>().FirstOrDefault();
        var executable = ancestors.Any(node => node is BlockSyntax or ArrowExpressionClauseSyntax or
            EqualsValueClauseSyntax or AnonymousFunctionExpressionSyntax or
            ArgumentListSyntax or BracketedArgumentListSyntax or InitializerExpressionSyntax);
        if (executable)
        {
            return ScriptDeclarationContext.Executable;
        }

        if (type is not null && type.OpenBraceToken.SpanStart < caretOffset &&
            (type.CloseBraceToken.IsMissing || caretOffset <= type.CloseBraceToken.SpanStart))
        {
            return ScriptDeclarationContext.TypeMember;
        }

        return ScriptDeclarationContext.TopLevel;
    }

}

public sealed record ScriptSnippetContext(string TypeName);

public enum ScriptDeclarationContext
{
    None,
    TopLevel,
    TypeMember,
    Parameter,
    Executable
}
