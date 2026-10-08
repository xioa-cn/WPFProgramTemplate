using System.Reflection;
using System.Windows;
using System.Windows.Media;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes;



public abstract class WorkflowNode : XTNode, IEditorLoggableNode
{
    private string _nodeName = string.Empty;
    private string _nodeTypeTitle = string.Empty;

    /// <summary>节点正文展示简短分类，完整描述和作者资料仍集中在右侧信息区。</summary>
    protected override double BodyHeaderHeight => 24;

    /// <summary>绘制分类摘要后沿用基础端口绘制，摘要不参与编辑或文件持久化。</summary>
    protected override void OnDrawBody(DrawingTools tools)
    {
        var category = GetType().GetCustomAttribute<XTNodeAttribute>()?.Path ?? "流程节点";
        NodeDrawing.Text(tools, category.Replace("/", " · "), new Rect(14, TitleHeight + 7, Width - 28, 18),
            NodeDrawing.Brush(this, "Workflow.Muted", Color.FromRgb(152, 168, 195)), 11);
        base.OnDrawBody(tools);
    }

    [XTNodeProperty("节点名称", "可选名称；留空时不在画布上显示。")]
    public string NodeName
    {
        get => _nodeName;
        set
        {
            _nodeName = value == null ? string.Empty : value.Trim();
            Title = string.IsNullOrEmpty(_nodeName) ? _nodeTypeTitle : _nodeName;
        }
    }

    [XTNodeProperty("生成执行日志", "是否在每次流程执行时记录该节点的日志。")]
    public bool EnableExecutionLog { get; set; } = false;

    protected void SetNodeTypeTitle(string title)
    {
        _nodeTypeTitle = title ?? string.Empty;
        Title = string.IsNullOrEmpty(_nodeName) ? _nodeTypeTitle : _nodeName;
    }
}
