using System.Diagnostics;
using System.Reflection;
using System.Runtime.ExceptionServices;
using ST.Library.UI.NodeEditor;
using WorkFlowCore.Nodes;
using WorkFlowCore.Nodes.Flow;
using Xunit;

namespace WorkFlowCore.Tests;

public sealed class FlowNodeTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    public void DelayWaitsBeforeActivatingOutput(int milliseconds)
    {
        RunSta(() =>
        {
            var node = new DelayNode { DelayMilliseconds = milliseconds };
            var context = new EditorExecutionContext();
            Assert.True(node.CanExecute(context).IsReady);
            var elapsed = Stopwatch.StartNew();
            var result = node.Execute(context);
            Assert.True(elapsed.ElapsedMilliseconds >= milliseconds);
            Assert.True(result.IsSuccess);
            Assert.Same(node.Output, Assert.Single(result.ActiveOutputs));
            Assert.Equal(context.ExecutionId, Assert.IsType<EditorFlowSignal>(node.Output.Data).ExecutionId);
        });
    }

    [Fact]
    public void DelayRejectsNegativeDurationWithoutChangingSetting()
    {
        RunSta(() =>
        {
            var node = new DelayNode();
            Assert.Equal(1000, node.DelayMilliseconds);
            Assert.Throws<ArgumentOutOfRangeException>(() => node.DelayMilliseconds = -1);
            Assert.Equal(1000, node.DelayMilliseconds);
            node.DelayMilliseconds = int.MaxValue;
            Assert.True(node.CanExecute(new()).IsReady);
        });
    }

    [Fact]
    public void CancellationInterruptsAnActiveWaitWithoutBroadcasting()
    {
        RunSta(() =>
        {
            using var cancellation = new CancellationTokenSource();
            var context = new EditorExecutionContext(cancellation.Token);
            var node = new DelayNode { DelayMilliseconds = 10000 };
            var editor = new XTNodeEditor();
            var next = new EmptyBeatNode();
            editor.Nodes.Add(node);
            editor.Nodes.Add(next);
            Assert.Equal(ConnectionStatus.Connected, node.Output.ConnectOption(next.Input));
            var transfers = 0;
            next.Input.DataTransfer += (_, _) => transfers++;
            typeof(EditorExecutionContext).GetProperty("NodeProgressReporter", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(context, (Action<string>)(_ => cancellation.CancelAfter(50)));
            var elapsed = Stopwatch.StartNew();
            Assert.ThrowsAny<OperationCanceledException>(() => node.Execute(context));
            Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(5));
            Assert.Null(node.Output.Data);
            Assert.Null(next.Input.Data);
            Assert.Equal(0, transfers);
            Assert.Equal("等待已取消", node.RuntimeText);
        });
    }

    [Theory]
    [InlineData(typeof(DelayNode))]
    [InlineData(typeof(EmptyBeatNode))]
    public void NodesValidateContextAndClearPreviousOutputOnCancellation(Type nodeType)
    {
        RunSta(() =>
        {
            var node = (WorkflowNode)Activator.CreateInstance(nodeType)!;
            if (node is DelayNode delay) delay.DelayMilliseconds = 0;
            var executable = (IEditorExecutableNode)node;
            Assert.True(executable.Execute(new()).IsSuccess);
            var output = Assert.Single(node.GetOutputOptions());
            Assert.NotNull(output.Data);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            var context = new EditorExecutionContext(cancellation.Token);
            Assert.ThrowsAny<OperationCanceledException>(() => executable.Execute(context));
            Assert.Null(output.Data);
            Assert.Throws<ArgumentNullException>(() => executable.Execute(null!));
            if (node is IEditorNodeReadiness readiness)
            {
                Assert.ThrowsAny<OperationCanceledException>(() => readiness.CanExecute(context));
                Assert.Throws<ArgumentNullException>(() => readiness.CanExecute(null!));
            }
        });
    }

    [Fact]
    public void FlowNodesConnectAndBroadcastToMultipleSuccessors()
    {
        RunSta(() =>
        {
            var editor = new XTNodeEditor();
            var start = new StartNode();
            var delay = new DelayNode { DelayMilliseconds = 0 };
            var beat = new EmptyBeatNode();
            var next = new EmptyBeatNode();
            foreach (var node in new WorkflowNode[] { start, delay, beat, next }) editor.Nodes.Add(node);
            Assert.Equal(ConnectionStatus.Connected, start.Output.ConnectOption(delay.Input));
            Assert.Equal(ConnectionStatus.Connected, delay.Output.ConnectOption(beat.Input));
            Assert.Equal(ConnectionStatus.Connected, delay.Output.ConnectOption(next.Input));
            var context = new EditorExecutionContext();
            Assert.True(start.Execute(context).IsSuccess);
            Assert.Same(start.Output.Data, delay.Input.Data);
            Assert.True(delay.Execute(context).IsSuccess);
            Assert.Same(delay.Output.Data, beat.Input.Data);
            Assert.Same(delay.Output.Data, next.Input.Data);
            var result = beat.Execute(context);
            Assert.True(result.IsSuccess);
            Assert.Same(beat.Output, Assert.Single(result.ActiveOutputs));
            Assert.Equal(context.ExecutionId, Assert.IsType<EditorFlowSignal>(beat.Output.Data).ExecutionId);
            delay.Output.DisConnectOption(beat.Input);
            Assert.Null(beat.Input.Data);
        });
    }

    [Fact]
    public void FlowNodesAreDiscoverableAndPersistPropertiesAndConnections()
    {
        RunSta(() =>
        {
            var panel = new XTNodeEditorPannel();
            var delay = new DelayNode { DelayMilliseconds = 250, NodeName = "工位等待", EnableExecutionLog = true };
            var beat = new EmptyBeatNode { NodeName = "预留步骤" };
            foreach (var node in new WorkflowNode[] { delay, beat })
            {
                Assert.Equal("流程控制", node.GetType().GetCustomAttribute<XTNodeAttribute>()!.Path);
                panel.AddXTNode(node.GetType());
                Assert.Contains(node.GetType(), panel.TreeView.GetTypes());
                panel.Editor.Nodes.Add(node);
            }
            Assert.Equal(ConnectionStatus.Connected, delay.Output.ConnectOption(beat.Input));
            panel.Editor.LoadCanvas(panel.Editor.GetCanvasData());
            var restoredDelay = Assert.Single(panel.Editor.Nodes.OfType<DelayNode>());
            var restoredBeat = Assert.Single(panel.Editor.Nodes.OfType<EmptyBeatNode>());
            Assert.Equal(250, restoredDelay.DelayMilliseconds);
            Assert.Equal("工位等待", restoredDelay.Title);
            Assert.True(restoredDelay.EnableExecutionLog);
            Assert.Equal("预留步骤", restoredBeat.Title);
            Assert.Same(restoredBeat.Input, Assert.Single(restoredDelay.Output.ConnectedOption));
            Assert.Null(restoredDelay.Output.Data);
            Assert.Null(restoredBeat.Output.Data);
        });
    }

    private static void RunSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception exception) { failure = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "STA 测试线程超时。");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
