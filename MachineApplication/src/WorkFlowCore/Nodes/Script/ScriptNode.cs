using System.IO;
using System.Windows;
using System.Windows.Media;
using CSharpScriptCore.Models;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.Script;

public abstract class ScriptNode : WorkflowNode, IEditorCustomEditorNode
{
    private string _scriptCode =
        "public class WorkflowScript\n{\n    public static int Execute(int value = 0) => value + 1;\n}";

    protected ScriptNode(string title)
    {
        SetNodeTypeTitle(title);
        TitleColor = Color.FromRgb(150, 116, 224);
        RuntimeText = "通过 CsxPad 编辑脚本";
    }

    [XTNodeProperty("脚本源码", "使用 CsxPad 编辑，源码随工作流保存。")]
    public string ScriptCode
    {
        get => _scriptCode;
        set
        {
            _scriptCode = value ?? string.Empty;
            foreach (var output in GetOutputOptions()) output.Data = null;
            RuntimeText = "脚本已更新，待执行";
        }
    }

    [XTNodeProperty("类名", "源码中顶层 class 的简单名称；类定义节点不创建实例。")]
    public string ClassName { get; set; } = "WorkflowScript";

    [XTNodeProperty("脚本目录", "相对 #load、#r 的基础目录；留空使用当前工作目录。")]
    public string ScriptDirectory { get; set; } = string.Empty;

    public string EditorButtonText => "在 CsxPad 中编辑";

    public bool OpenEditor(Window? owner)
    {
        var editor = new CsxPad.Wpf.MainWindow(ScriptCode, ClassName + ".csx");
        if (owner is not null) editor.Owner = owner;
        if (editor.ShowDialog() != true || editor.EditedScript is null) return false;
        ScriptCode = editor.EditedScript;
        return true;
    }

    protected internal override bool IsPropertyVisible(string propertyName) =>
        propertyName != nameof(ScriptCode) && base.IsPropertyVisible(propertyName);

    protected ScriptClassDefinition GetDefinition()
    {
        var definition = new ScriptClassDefinition(ScriptCode, ClassName.Trim(),
            Path.GetFullPath(
                string.IsNullOrWhiteSpace(ScriptDirectory) ? Environment.CurrentDirectory : ScriptDirectory));
        definition.Validate();
        return definition;
    }

    protected EditorNodeExecutionResult Failure(ScriptResult result, EditorExecutionContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        if (result.RunResult == ScriptRunStatus.Cancelled)
            throw new OperationCanceledException(result.Exception?.Message);
        RuntimeText = "脚本执行失败";
        var message = result.Diagnostics.Count > 0
            ? string.Join(Environment.NewLine, result.Diagnostics)
            : result.Exception?.Message ?? result.Output;
        return EditorNodeExecutionResult.Failure(string.IsNullOrWhiteSpace(message)
            ? result.RunResult.ToString()
            : message);
    }
}