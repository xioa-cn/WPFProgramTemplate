using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CsxPad.Wpf.Services;

internal static class ScriptDebugInstrumenter
{
    public static string Instrument(string code, IReadOnlySet<int> breakpoints)
    {
        if (breakpoints.Count == 0)
        {
            return code;
        }

        var tree = CSharpSyntaxTree.ParseText(
            code,
            new CSharpParseOptions(LanguageVersion.Latest, kind: SourceCodeKind.Script));
        var root = tree.GetCompilationUnitRoot();
        return new BreakpointRewriter(root, breakpoints).Visit(root)!.ToFullString();
    }

    private sealed class BreakpointRewriter : CSharpSyntaxRewriter
    {
        private readonly IReadOnlyDictionary<int, int> _breakpointLinesByStatement;
        private Dictionary<string, VisibleVariable> _visibleVariables = new(StringComparer.Ordinal);

        public BreakpointRewriter(CompilationUnitSyntax root, IReadOnlySet<int> breakpoints)
        {
            var statements = root.DescendantNodes()
                .OfType<StatementSyntax>()
                .Where(statement => statement is not BlockSyntax and not EmptyStatementSyntax)
                .Select(statement => new StatementLocation(
                    statement.SpanStart,
                    statement.Span.Length,
                    statement.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                    statement.GetLocation().GetLineSpan().EndLinePosition.Line + 1))
                .ToArray();
            _breakpointLinesByStatement = BindBreakpoints(statements, breakpoints);
        }

        public override SyntaxNode? VisitCompilationUnit(CompilationUnitSyntax node)
        {
            var members = new List<MemberDeclarationSyntax>();
            foreach (var member in node.Members)
            {
                if (member is not GlobalStatementSyntax global)
                {
                    var scriptScope = _visibleVariables;
                    _visibleVariables = new Dictionary<string, VisibleVariable>(StringComparer.Ordinal);
                    try
                    {
                        members.Add((MemberDeclarationSyntax)Visit(member)!);
                    }
                    finally
                    {
                        _visibleVariables = scriptScope;
                    }
                    continue;
                }

                var checkpoint = SyntaxFactory.GlobalStatement(
                    CreateCheckpoint(global.Statement, _visibleVariables.Values));
                members.Add(checkpoint);
                var visited = (MemberDeclarationSyntax)Visit(global)!;
                visited = visited.WithoutLeadingTrivia();

                members.Add(visited);
                AddDeclaredVariables(global.Statement, _visibleVariables);
            }

            return node.WithMembers(SyntaxFactory.List(members));
        }

        public override SyntaxNode? VisitBlock(BlockSyntax node)
        {
            var parentScope = _visibleVariables;
            _visibleVariables = new Dictionary<string, VisibleVariable>(parentScope, StringComparer.Ordinal);
            try
            {
                AddOwningScopeVariables(node, _visibleVariables);
                return node.WithStatements(RewriteStatements(node.Statements));
            }
            finally
            {
                _visibleVariables = parentScope;
            }
        }

        public override SyntaxNode? VisitSwitchSection(SwitchSectionSyntax node)
        {
            var parentScope = _visibleVariables;
            _visibleVariables = new Dictionary<string, VisibleVariable>(parentScope, StringComparer.Ordinal);
            try
            {
                var visitedLabels = VisitList(node.Labels);
                return node.WithLabels(visitedLabels).WithStatements(RewriteStatements(node.Statements));
            }
            finally
            {
                _visibleVariables = parentScope;
            }
        }

        public override SyntaxNode? VisitIfStatement(IfStatementSyntax node)
        {
            var visited = (IfStatementSyntax)base.VisitIfStatement(node)!;
            visited = visited.WithStatement(RewriteEmbeddedStatement(node.Statement, visited.Statement));
            if (node.Else is not null && visited.Else is not null)
            {
                visited = visited.WithElse(visited.Else.WithStatement(
                    RewriteEmbeddedStatement(node.Else.Statement, visited.Else.Statement)));
            }

            return visited;
        }

        public override SyntaxNode? VisitForStatement(ForStatementSyntax node)
        {
            var visited = (ForStatementSyntax)base.VisitForStatement(node)!;
            return visited.WithStatement(RewriteEmbeddedStatement(node.Statement, visited.Statement));
        }

        public override SyntaxNode? VisitForEachStatement(ForEachStatementSyntax node)
        {
            var visited = (ForEachStatementSyntax)base.VisitForEachStatement(node)!;
            return visited.WithStatement(RewriteEmbeddedStatement(node.Statement, visited.Statement));
        }

        public override SyntaxNode? VisitForEachVariableStatement(ForEachVariableStatementSyntax node)
        {
            var visited = (ForEachVariableStatementSyntax)base.VisitForEachVariableStatement(node)!;
            return visited.WithStatement(RewriteEmbeddedStatement(node.Statement, visited.Statement));
        }

        public override SyntaxNode? VisitWhileStatement(WhileStatementSyntax node)
        {
            var visited = (WhileStatementSyntax)base.VisitWhileStatement(node)!;
            return visited.WithStatement(RewriteEmbeddedStatement(node.Statement, visited.Statement));
        }

        public override SyntaxNode? VisitDoStatement(DoStatementSyntax node)
        {
            var visited = (DoStatementSyntax)base.VisitDoStatement(node)!;
            return visited.WithStatement(RewriteEmbeddedStatement(node.Statement, visited.Statement));
        }

        public override SyntaxNode? VisitUsingStatement(UsingStatementSyntax node)
        {
            var visited = (UsingStatementSyntax)base.VisitUsingStatement(node)!;
            return visited.WithStatement(RewriteEmbeddedStatement(node.Statement, visited.Statement));
        }

        public override SyntaxNode? VisitLockStatement(LockStatementSyntax node)
        {
            var visited = (LockStatementSyntax)base.VisitLockStatement(node)!;
            return visited.WithStatement(RewriteEmbeddedStatement(node.Statement, visited.Statement));
        }

        public override SyntaxNode? VisitFixedStatement(FixedStatementSyntax node)
        {
            var visited = (FixedStatementSyntax)base.VisitFixedStatement(node)!;
            return visited.WithStatement(RewriteEmbeddedStatement(node.Statement, visited.Statement));
        }

        private SyntaxList<StatementSyntax> RewriteStatements(SyntaxList<StatementSyntax> statements)
        {
            var rewritten = new List<StatementSyntax>();
            foreach (var statement in statements)
            {
                rewritten.Add(CreateCheckpoint(statement, _visibleVariables.Values));
                var visited = (StatementSyntax)Visit(statement)!;
                visited = visited.WithoutLeadingTrivia();

                rewritten.Add(visited);
                AddDeclaredVariables(statement, _visibleVariables);
            }

            return SyntaxFactory.List(rewritten);
        }

        private StatementSyntax RewriteEmbeddedStatement(
            StatementSyntax original,
            StatementSyntax visited)
        {
            if (original is BlockSyntax)
            {
                return visited;
            }

            var embeddedVariables = new Dictionary<string, VisibleVariable>(
                _visibleVariables,
                StringComparer.Ordinal);
            AddEmbeddedScopeVariables(original, embeddedVariables);
            var checkpoint = CreateCheckpoint(original, embeddedVariables.Values);
            return SyntaxFactory.Block(checkpoint, visited.WithoutLeadingTrivia())
                .WithLeadingTrivia(original.GetLeadingTrivia());
        }

        private static void AddEmbeddedScopeVariables(
            StatementSyntax statement,
            IDictionary<string, VisibleVariable> variables)
        {
            switch (statement.Parent)
            {
                case ForEachStatementSyntax forEach when forEach.Statement == statement:
                    AddVariable(forEach.Identifier, variables);
                    break;
                case ForStatementSyntax forStatement when forStatement.Statement == statement &&
                                                          forStatement.Declaration is not null:
                    AddDeclarators(forStatement.Declaration.Variables, variables, requireInitializer: false);
                    break;
                case UsingStatementSyntax { Declaration: not null } usingStatement
                    when usingStatement.Statement == statement:
                    AddDeclarators(usingStatement.Declaration.Variables, variables, requireInitializer: false);
                    break;
            }
        }

        private StatementSyntax CreateCheckpoint(
            StatementSyntax statement,
            IEnumerable<VisibleVariable> visibleVariables)
        {
            var statementLine = statement.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            var line = _breakpointLinesByStatement.TryGetValue(statement.SpanStart, out var breakpointLine)
                ? breakpointLine
                : statementLine;
            var entries = string.Join(
                ", ",
                visibleVariables.Select(variable => $"[\"{variable.Key}\"] = {variable.Expression}"));
            return SyntaxFactory.ParseStatement(
                    $"global::CsxPad.Wpf.Scripting.ScriptDebugger.Pause({line}, " +
                    $"new global::System.Collections.Generic.Dictionary<string, object?> {{ {entries} }});")
                .WithLeadingTrivia(statement.GetLeadingTrivia())
                .WithTrailingTrivia(SyntaxFactory.Space);
        }

        private static void AddOwningScopeVariables(
            BlockSyntax block,
            IDictionary<string, VisibleVariable> variables)
        {
            switch (block.Parent)
            {
                case ForEachStatementSyntax forEach when forEach.Statement == block:
                    AddVariable(forEach.Identifier, variables);
                    break;
                case ForStatementSyntax forStatement when forStatement.Statement == block &&
                                                          forStatement.Declaration is not null:
                    AddDeclarators(forStatement.Declaration.Variables, variables, requireInitializer: false);
                    break;
                case CatchClauseSyntax { Declaration: not null } catchClause:
                    AddVariable(catchClause.Declaration.Identifier, variables);
                    break;
                case UsingStatementSyntax { Declaration: not null } usingStatement:
                    AddDeclarators(usingStatement.Declaration.Variables, variables, requireInitializer: false);
                    break;
                case MethodDeclarationSyntax method:
                    AddParameters(method.ParameterList.Parameters, variables);
                    break;
                case ConstructorDeclarationSyntax constructor:
                    AddParameters(constructor.ParameterList.Parameters, variables);
                    break;
                case DestructorDeclarationSyntax destructor:
                    AddParameters(destructor.ParameterList.Parameters, variables);
                    break;
                case OperatorDeclarationSyntax operatorDeclaration:
                    AddParameters(operatorDeclaration.ParameterList.Parameters, variables);
                    break;
                case ConversionOperatorDeclarationSyntax conversion:
                    AddParameters(conversion.ParameterList.Parameters, variables);
                    break;
                case LocalFunctionStatementSyntax localFunction:
                    AddParameters(localFunction.ParameterList.Parameters, variables);
                    break;
                case ParenthesizedLambdaExpressionSyntax parenthesizedLambda:
                    AddParameters(parenthesizedLambda.ParameterList.Parameters, variables);
                    break;
                case SimpleLambdaExpressionSyntax simpleLambda:
                    AddVariable(simpleLambda.Parameter.Identifier, variables);
                    break;
                case AnonymousMethodExpressionSyntax { ParameterList: not null } anonymousMethod:
                    AddParameters(anonymousMethod.ParameterList.Parameters, variables);
                    break;
            }
        }

        private static void AddDeclaredVariables(
            StatementSyntax statement,
            IDictionary<string, VisibleVariable> variables)
        {
            if (statement is LocalDeclarationStatementSyntax declaration)
            {
                AddDeclarators(declaration.Declaration.Variables, variables, requireInitializer: true);
            }
        }

        private static void AddDeclarators(
            SeparatedSyntaxList<VariableDeclaratorSyntax> declarators,
            IDictionary<string, VisibleVariable> variables,
            bool requireInitializer)
        {
            foreach (var declarator in declarators)
            {
                if (!requireInitializer || declarator.Initializer is not null)
                {
                    AddVariable(declarator.Identifier, variables);
                }
            }
        }

        private static void AddVariable(
            SyntaxToken identifier,
            IDictionary<string, VisibleVariable> variables)
        {
            if (!identifier.IsMissing)
            {
                variables[identifier.ValueText] = new VisibleVariable(identifier.ValueText, identifier.Text);
            }
        }

        private static void AddParameters(
            SeparatedSyntaxList<ParameterSyntax> parameters,
            IDictionary<string, VisibleVariable> variables)
        {
            foreach (var parameter in parameters)
            {
                AddVariable(parameter.Identifier, variables);
            }
        }

        private static IReadOnlyDictionary<int, int> BindBreakpoints(
            IReadOnlyList<StatementLocation> statements,
            IReadOnlySet<int> breakpoints)
        {
            var result = new Dictionary<int, int>();
            foreach (var breakpoint in breakpoints.OrderBy(line => line))
            {
                var target = statements
                    .Where(statement => statement.StartLine <= breakpoint && breakpoint <= statement.EndLine)
                    .OrderBy(statement => statement.Length)
                    .ThenByDescending(statement => statement.StartLine)
                    .FirstOrDefault()
                    ?? statements
                        .Where(statement => statement.StartLine > breakpoint)
                        .OrderBy(statement => statement.StartLine)
                        .ThenBy(statement => statement.Length)
                        .FirstOrDefault()
                    ?? statements
                        .Where(statement => statement.StartLine < breakpoint)
                        .OrderByDescending(statement => statement.StartLine)
                        .ThenBy(statement => statement.Length)
                        .FirstOrDefault();
                if (target is not null)
                {
                    result.TryAdd(target.SpanStart, breakpoint);
                }
            }

            return result;
        }

        private sealed record VisibleVariable(string Key, string Expression);

        private sealed record StatementLocation(
            int SpanStart,
            int Length,
            int StartLine,
            int EndLine);
    }
}
