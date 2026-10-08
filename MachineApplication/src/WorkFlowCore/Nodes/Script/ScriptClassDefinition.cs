using CSharpScriptCore.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace WorkFlowCore.Nodes.Script;

public sealed record ScriptClassDefinition(string Code, string ClassName, string BaseDirectory)
{
    public string? PackageWorkspaceDirectory { get; init; }
    public string? ScriptPath { get; init; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Code)) throw new ArgumentException("请先在 CsxPad 中编写类定义。");
        if (!SyntaxFacts.IsValidIdentifier(ClassName)) throw new ArgumentException("类名必须是脚本顶层类的简单名称。");
        var root = CSharpSyntaxTree.ParseText(Code, new CSharpParseOptions(kind: SourceCodeKind.Script)).GetCompilationUnitRoot();
        var errors = root.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();
        if (errors.Length > 0) throw new ArgumentException(string.Join(Environment.NewLine, errors.Select(error => error.ToString())));
        if (root.Members.Any(member => member is not BaseTypeDeclarationSyntax and not DelegateDeclarationSyntax))
            throw new ArgumentException("这里用于定义 class；请把可执行语句放入类的方法内，不要在脚本顶层执行代码。");
        if (!root.Members.OfType<ClassDeclarationSyntax>().Any(type => type.Identifier.Text == ClassName))
            throw new ArgumentException($"源码中未找到顶层 class {ClassName}。");
    }

    public ScriptExecutionOptions CreateOptions()
    {
        var options = new ScriptExecutionOptions
        {
            BaseDirectory = BaseDirectory,
            ScriptPath = ScriptPath ?? System.IO.Path.Combine(BaseDirectory, ClassName + ".csx")
        };
        if (!string.IsNullOrWhiteSpace(PackageWorkspaceDirectory))
            options.PackageWorkspaceDirectory = PackageWorkspaceDirectory;
        return options;
    }
}
