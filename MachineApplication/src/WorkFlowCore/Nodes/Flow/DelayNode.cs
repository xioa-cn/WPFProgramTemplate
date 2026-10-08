using System.ComponentModel;
using System.Windows.Media;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.Flow;

[XTNode("流程控制", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/",
    "等待指定毫秒数后继续流程；等待期间支持取消，0 表示立即继续。")]
[DisplayName("等待")]
public sealed class DelayNode : WorkflowNode, IEditorExecutableNode, IEditorNodeReadiness
{
    private int _delayMilliseconds = 1000;

    public DelayNode()
    {
        SetNodeTypeTitle("等待");
        TitleColor = Color.FromRgb(58, 190, 130);
        Input = InputOptions.Add("输入", typeof(object), true);
        Input.Description = "上游流程触发输入。";
        Input.DataTransfer += (_, args) =>
            Input.Data = args.Status == ConnectionStatus.Connected ? args.TargetOption.Data : null;
        Output = OutputOptions.Add("输出", typeof(object), false);
        Output.Description = "等待结束后输出流程信号，支持连接多个后续节点。";
        RuntimeText = $"等待 {DelayMilliseconds} ms";
    }

    [XTNodeProperty("等待时间（毫秒）", "非负整数，默认 1000 毫秒；0 表示不等待，取消时不触发后续节点。")]
    public int DelayMilliseconds
    {
        get => _delayMilliseconds;
        set
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value), "等待时间不能小于 0 毫秒。");
            _delayMilliseconds = value;
            RuntimeText = $"等待 {value} ms";
        }
    }

    public XTNodeOption Input { get; }
    public XTNodeOption Output { get; }

    public EditorNodeReadinessResult CanExecute(EditorExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.CancellationToken.ThrowIfCancellationRequested();
        return EditorNodeReadinessResult.Ready();
    }

    public EditorNodeExecutionResult Execute(EditorExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        Output.Data = null;
        var delay = DelayMilliseconds;
        try
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            context.ReportNodeProgress($"等待 {delay} ms…");
            Task.Delay(delay, context.CancellationToken).GetAwaiter().GetResult();
            context.CancellationToken.ThrowIfCancellationRequested();
            Output.TransferData(new EditorFlowSignal(context.ExecutionId));
            RuntimeText = $"已等待 {delay} ms";
            return EditorNodeExecutionResult.Success($"等待 {delay} 毫秒完成。", Output);
        }
        catch (OperationCanceledException)
        {
            RuntimeText = "等待已取消";
            throw;
        }
    }
}
