using System.ComponentModel;
using System.Windows.Media;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.Flow;

[XTNode("流程控制", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/",
    "流程的起始节点，只提供可连接多个后续节点的输出端口。")]
// 作者：xioa
// 作者邮箱：1327916255@qq.com
[DisplayName("开始")]
public class StartNode : WorkflowNode, IEditorExecutableNode, IEditorStartNode
{
    /// <summary>创建绿色流程入口节点，分类色同步用于标题、端口和连线。</summary>
    public StartNode()
    {
        SetNodeTypeTitle("开始");
        TitleColor = Color.FromRgb(58, 190, 130);
        LetGetOptions = true;
        Output = OutputOptions.Add("输出", typeof(object), false);
    }

    public XTNodeOption Output { get; }

    public EditorNodeExecutionResult Execute(EditorExecutionContext context)
    {
        if (context == null) throw new System.ArgumentNullException(nameof(context));
        context.CancellationToken.ThrowIfCancellationRequested();
        Output.Data = new EditorFlowSignal(context.ExecutionId);
        Output.TransferData();
        return EditorNodeExecutionResult.Success("流程已从开始节点启动。", Output);
    }
}
