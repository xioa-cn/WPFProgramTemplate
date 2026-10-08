namespace WorkFlowCore.Nodes.Script;

[System.ComponentModel.DisplayName("脚本类定义")]
[ST.Library.UI.NodeEditor.XTNode("脚本", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/",
    "在 CsxPad 中定义 class；执行时仅编译校验并输出类定义，不调用构造函数或方法。")]
public sealed class ScriptClassNode : ScriptNode, ST.Library.UI.NodeEditor.IEditorExecutableNode, ST.Library.UI.NodeEditor.IEditorNodeReadiness
{
    public ScriptClassNode() : base("脚本类定义")
    {
        Output = OutputOptions.Add("类定义", typeof(ScriptClassDefinition), false);
        Output.Description = "已校验的脚本源码和类名，连接到脚本方法节点的类定义输入。";
    }

    public ST.Library.UI.NodeEditor.XTNodeOption Output { get; }

    public ST.Library.UI.NodeEditor.EditorNodeReadinessResult CanExecute(ST.Library.UI.NodeEditor.EditorExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.CancellationToken.ThrowIfCancellationRequested();
        try { GetDefinition(); return ST.Library.UI.NodeEditor.EditorNodeReadinessResult.Ready(); }
        catch (Exception exception) when (exception is not OperationCanceledException)
        { return ST.Library.UI.NodeEditor.EditorNodeReadinessResult.NotReady(exception.Message); }
    }

    public ST.Library.UI.NodeEditor.EditorNodeExecutionResult Execute(ST.Library.UI.NodeEditor.EditorExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        Output.Data = null;
        context.CancellationToken.ThrowIfCancellationRequested();
        try
        {
            var definition = GetDefinition();
            var result = Task.Run(() => CSharpScriptCore.CSharpScript.CompileCodeAsync(
                definition.Code + "\n typeof(" + definition.ClassName + ")", definition.CreateOptions(), context.CancellationToken))
                .GetAwaiter().GetResult();
            if (result.RunResult != CSharpScriptCore.Models.ScriptRunStatus.ReadyToRun) return Failure(result, context);
            context.CancellationToken.ThrowIfCancellationRequested();
            Output.TransferData(definition);
            RuntimeText = "class " + definition.ClassName;
            return ST.Library.UI.NodeEditor.EditorNodeExecutionResult.Success("类定义校验通过，未创建实例。", Output);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Output.Data = null;
            RuntimeText = "类定义无效";
            return ST.Library.UI.NodeEditor.EditorNodeExecutionResult.Failure(exception.Message);
        }
    }
}
