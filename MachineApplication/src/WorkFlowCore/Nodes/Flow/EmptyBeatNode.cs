using System.ComponentModel;
using System.Windows.Media;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.Flow;

[XTNode("流程控制", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/",
    "不执行业务操作、不增加等待时间，收到流程触发后直接继续，可作为流程占位或中转节点。")]
[DisplayName("空节拍")]
public sealed class EmptyBeatNode : WorkflowNode, IEditorExecutableNode
{
    public EmptyBeatNode()
    {
        SetNodeTypeTitle("空节拍");
        TitleColor = Color.FromRgb(58, 190, 130);
        Input = InputOptions.Add("输入", typeof(object), true);
        Input.Description = "上游流程触发输入。";
        Input.DataTransfer += (_, args) =>
            Input.Data = args.Status == ConnectionStatus.Connected ? args.TargetOption.Data : null;
        Output = OutputOptions.Add("输出", typeof(object), false);
        Output.Description = "直接输出流程信号，支持连接多个后续节点。";
    }

    public XTNodeOption Input { get; }
    public XTNodeOption Output { get; }

    public EditorNodeExecutionResult Execute(EditorExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        Output.Data = null;
        context.CancellationToken.ThrowIfCancellationRequested();
        Output.TransferData(new EditorFlowSignal(context.ExecutionId));
        return EditorNodeExecutionResult.Success("空节拍完成，继续流程。", Output);
    }
}
