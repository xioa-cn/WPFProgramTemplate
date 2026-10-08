using System.Reflection;
using System.Runtime.ExceptionServices;
using ST.Library.UI.NodeEditor;
using WorkFlowCore.Nodes.Data;
using WorkFlowCore.Nodes.Operation;
using Xunit;

namespace WorkFlowCore.Tests;

public sealed class OperationNodeTests
{
    public static IEnumerable<object[]> NodeTypes => OperationNodeCatalog.NodeTypes.Select(type => new object[] { type });

    [Fact]
    public void CatalogRegistersEveryConcreteOperationOnPageAndInPersistence()
    {
        RunSta(() =>
        {
            var actual = typeof(OperationNode).Assembly.GetTypes()
                .Where(type => typeof(OperationNode).IsAssignableFrom(type) && !type.IsAbstract).ToHashSet();
            Assert.Equal(34, actual.Count);
            Assert.True(actual.SetEquals(OperationNodeCatalog.NodeTypes));
            var panel = new XTNodeEditorPannel();
            OperationNodeCatalog.Register(panel);
            Assert.True(actual.SetEquals(panel.TreeView.GetTypes()));
            Assert.True(actual.SetEquals(panel.Editor.GetTypes()));
            foreach (var type in actual)
            {
                Assert.NotNull(type.GetCustomAttribute<XTNodeAttribute>());
                Assert.NotNull(type.GetCustomAttribute<System.ComponentModel.DisplayNameAttribute>());
            }
        });
    }

    [Theory]
    [MemberData(nameof(NodeTypes))]
    public void EveryNodeExecutesWithDefaultsAndRoundTripsConfiguration(Type type)
    {
        RunSta(() =>
        {
            var panel = new XTNodeEditorPannel();
            OperationNodeCatalog.Register(panel);
            var node = (OperationNode)Activator.CreateInstance(type)!;
            node.NodeName = "测试节点";
            node.EnableExecutionLog = true;
            panel.Editor.Nodes.Add(node);
            var context = new EditorExecutionContext();
            Execute(new SetVarialbeNode { ValueJson = "7" }, context);
            Assert.True(node.CanExecute(context).IsReady);
            var result = node.Execute(context);
            Assert.True(result.IsSuccess, result.Message);
            Assert.NotEmpty(result.ActiveOutputs);
            panel.Editor.LoadCanvas(panel.Editor.GetCanvasData());
            var loaded = Assert.IsAssignableFrom<OperationNode>(Assert.Single(panel.Editor.Nodes));
            Assert.Equal(type, loaded.GetType());
            Assert.Equal(node.Guid, loaded.Guid);
            foreach (var property in type.GetProperties().Where(property => property.GetCustomAttribute<XTNodePropertyAttribute>() is not null))
                Assert.Equal(property.GetValue(node), property.GetValue(loaded));
            Assert.All(loaded.GetOutputOptions(), output => Assert.Null(output.Data));
            result = loaded.Execute(context);
            Assert.True(result.IsSuccess, result.Message);
        });
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void BooleanTruthTables(bool left, bool right)
    {
        RunSta(() =>
        {
            var leftJson = left ? "true" : "false";
            var rightJson = right ? "true" : "false";
            Assert.Equal(left && right, Execute(new AndNode { LeftJson = leftJson, RightJson = rightJson }));
            Assert.Equal(left || right, Execute(new OrNode { LeftJson = leftJson, RightJson = rightJson }));
            Assert.Equal(!left, Execute(new NotNode { ValueJson = leftJson }));
            var invalid = new AndNode { LeftJson = "false", RightJson = "123" };
            Assert.False(invalid.CanExecute(new()).IsReady);
            Assert.False(invalid.Execute(new()).IsSuccess);
        });
    }

    [Fact]
    public void ComparisonsHandleNullStringsNumbersAndInvalidInputs()
    {
        RunSta(() =>
        {
            Assert.Equal(true, Execute(new EqualToNode { LeftJson = "null", RightJson = "null" }));
            Assert.Equal(true, Execute(new NotEqualToNode { LeftJson = "null", RightJson = "0" }));
            Assert.Equal(false, Execute(new EqualToNode { LeftJson = "\"A\"", RightJson = "\"a\"" }));
            var node = new EqualToNode();
            node.LeftInput.Data = 12L;
            node.RightInput.Data = 12m;
            Assert.Equal(true, Execute(node));
            var comparison = new SizeComparisonNode { LeftJson = "2", RightJson = "1" };
            Assert.Equal(true, Execute(comparison));
            comparison.Mode = ComparisonMode.LessOrEqual;
            Assert.Equal(false, Execute(comparison));
            comparison.LeftInput.Data = DateTime.UnixEpoch;
            comparison.RightInput.Data = DateTime.UnixEpoch.AddDays(1);
            Assert.Equal(true, Execute(comparison));
            comparison.LeftInput.Data = double.NaN;
            comparison.RightInput.Data = 1;
            Assert.False(comparison.Execute(new()).IsSuccess);
            Assert.Null(comparison.Output.Data);
            comparison.Mode = (ComparisonMode)99;
            Assert.False(comparison.CanExecute(new()).IsReady);
        });
    }

    [Fact]
    public void ArraysImplementAllOperationsWithoutMutatingInputs()
    {
        RunSta(() =>
        {
            object?[] original = [1, 2, null, 2];
            var create = new ArrayCreateNode();
            create.ArrayInput.Data = new List<int> { 1, 2 };
            Assert.Equal(new object?[] { 1, 2 }, Assert.IsType<object[]>(Execute(create)));
            var add = new ArrayAddNode { ValueJson = "3" };
            add.ArrayInput.Data = original;
            Assert.Equal(new object?[] { 1, 2, null, 2, 3 }, Assert.IsType<object[]>(Execute(add)));
            Assert.Equal(4, Execute(new ArrayCountNode { ItemsJson = "[1,2,null,2]" }));
            Assert.Null(Execute(new ArrayGetNode { ItemsJson = "[1,2,null]", Index = 2 }));
            Assert.Equal(true, Execute(new ArrayContainsNode { ItemsJson = "[1,null]" }));
            Assert.Equal(1, Execute(new ArrayIndexOfNode { ItemsJson = "[1,2,2]", ValueJson = "2.0" }));
            Assert.Equal(-1, Execute(new ArrayIndexOfNode { ValueJson = "100" }));
            var set = new ArraySetNode { Index = 0, ValueJson = "9" };
            set.ArrayInput.Data = original;
            Assert.Equal(new object?[] { 9, 2, null, 2 }, Assert.IsType<object[]>(Execute(set)));
            var remove = new ArrayRemoveAtNode { Index = 1 };
            remove.ArrayInput.Data = original;
            Assert.Equal(new object?[] { 1, null, 2 }, Assert.IsType<object[]>(Execute(remove)));
            var reverse = new ArrayReverseNode();
            reverse.ArrayInput.Data = original;
            Assert.Equal(new object?[] { 2, null, 2, 1 }, Assert.IsType<object[]>(Execute(reverse)));
            Assert.Equal(new object?[] { 2, 3 }, Assert.IsType<object[]>(Execute(new ArraySliceNode { Start = 1 })));
            Assert.Empty(Assert.IsType<object[]>(Execute(new ArraySliceNode { Start = 3 })));
            Assert.Empty(Assert.IsType<object[]>(Execute(new ArrayClearNode())));
            Assert.Equal(new object?[] { 1, 2, null, 2 }, original);
        });
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    [InlineData(int.MaxValue)]
    public void InvalidArrayIndexesFailWithoutOldOutput(int index)
    {
        RunSta(() =>
        {
            var get = new ArrayGetNode();
            Execute(get);
            get.Index = index;
            Assert.False(get.CanExecute(new()).IsReady);
            Assert.False(get.Execute(new()).IsSuccess);
            Assert.Null(get.Output.Data);
            Assert.False(new ArraySetNode { Index = index }.Execute(new()).IsSuccess);
            Assert.False(new ArrayRemoveAtNode { Index = index }.Execute(new()).IsSuccess);
            Assert.False(new ArraySliceNode { Length = int.MaxValue }.Execute(new()).IsSuccess);
        });
    }

    [Fact]
    public void DictionaryOperationsPreserveNullAndDoNotMutateSources()
    {
        RunSta(() =>
        {
            var original = new Dictionary<string, int> { ["key"] = 1 };
            var set = new DicSetNode { ValueJson = "null" };
            set.DictionaryInput.Data = original;
            var changed = Assert.IsType<Dictionary<string, object?>>(Execute(set));
            Assert.Null(changed["key"]);
            Assert.Equal(1, original["key"]);
            Assert.Null(Execute(new DicGetNode { EntriesJson = "{\"key\":null}" }));
            Assert.False(new DicGetNode { Key = "missing" }.CanExecute(new()).IsReady);
            Assert.False(new DicGetNode { Key = "missing" }.Execute(new()).IsSuccess);
            Assert.Equal(false, Execute(new DicContainsKeyNode { Key = "KEY" }));
            Assert.Equal(true, Execute(new DicContainsKeyNode()));
            Assert.Equal(true, Execute(new DicContainsValueNode { ValueJson = "1.0" }));
            Assert.Equal(new[] { "key" }, Assert.IsType<string[]>(Execute(new DicGetKeysNode())));
            Assert.Equal(new object?[] { 1 }, Assert.IsType<object[]>(Execute(new DicGetValuesNode())));
            Assert.Empty(Assert.IsType<Dictionary<string, object?>>(Execute(new DicRemoveNode())));
            Assert.Single(Assert.IsType<Dictionary<string, object?>>(Execute(new DicRemoveNode { Key = "missing" })));
            Assert.Empty(Assert.IsType<Dictionary<string, object?>>(Execute(new DicClearNode())));
            Assert.Empty(Assert.IsType<Dictionary<string, object?>>(Execute(new DicCreateNode())));
        });
    }

    [Fact]
    public void NullTransfersAreDistinctFromPendingConnectedInputsAndDefaults()
    {
        RunSta(() =>
        {
            var editor = new XTNodeEditor();
            var source = new ArrayGetNode { ItemsJson = "[null]" };
            var receiver = new IsNullNode { ValueJson = "7" };
            editor.Nodes.Add(source);
            editor.Nodes.Add(receiver);
            Assert.Equal(ConnectionStatus.Connected, source.Output.ConnectOption(receiver.ValueInput));
            Assert.True(receiver.CanExecute(new()).IsReady);
            Assert.False(receiver.Execute(new()).IsSuccess);
            Execute(source);
            Assert.Equal(true, Execute(receiver));
            source.Output.DisConnectOption(receiver.ValueInput);
            Assert.Equal(false, Execute(receiver));
        });
    }

    [Fact]
    public void IfAndSwitchActivateOnlyTheChosenOutput()
    {
        RunSta(() =>
        {
            var context = new EditorExecutionContext();
            var conditional = new IfNode { Condition = true };
            Assert.Same(conditional.TrueOutput, Assert.Single(conditional.Execute(context).ActiveOutputs));
            conditional.Condition = false;
            Assert.Same(conditional.FalseOutput, Assert.Single(conditional.Execute(context).ActiveOutputs));
            Assert.Null(conditional.TrueOutput.Data);
            Assert.Equal(context.ExecutionId, Assert.IsType<EditorFlowSignal>(conditional.FalseOutput.Data).ExecutionId);
            var choice = new SwitchNode { CasesJson = "[null,1,\"ok\"]", ValueJson = "1.0" };
            Assert.Same(choice.CaseOutputs[1], Assert.Single(choice.Execute(context).ActiveOutputs));
            choice.ValueJson = "\"missing\"";
            Assert.Same(choice.DefaultOutput, Assert.Single(choice.Execute(context).ActiveOutputs));
            Assert.All(choice.CaseOutputs, port => Assert.Null(port.Data));
            choice.ValueJson = "null";
            Assert.Same(choice.CaseOutputs[0], Assert.Single(choice.Execute(context).ActiveOutputs));
            Assert.Throws<ArgumentException>(() => choice.CasesJson = "[1,1.0]");
            Assert.Throws<ArgumentException>(() => choice.CasesJson = "[{}]");
            Assert.Equal(3, choice.CaseOutputs.Count);
        });
    }

    [Fact]
    public void SwitchCustomPortsAndWiresRoundTripAndRejectConnectedChanges()
    {
        RunSta(() =>
        {
            var panel = new XTNodeEditorPannel();
            OperationNodeCatalog.Register(panel);
            var choice = new SwitchNode { CasesJson = "[\"a\",\"b\",\"c\",null]", ValueJson = "\"c\"" };
            var target = new GetVarialbeNode { UseDefaultWhenMissing = true };
            panel.Editor.Nodes.Add(choice);
            panel.Editor.Nodes.Add(target);
            Assert.Equal(ConnectionStatus.Connected, choice.CaseOutputs[2].ConnectOption(target.TriggerInput));
            Assert.Throws<InvalidOperationException>(() => choice.CasesJson = "[]");
            panel.Editor.LoadCanvas(panel.Editor.GetCanvasData());
            var loaded = Assert.Single(panel.Editor.Nodes.OfType<SwitchNode>());
            Assert.Equal(4, loaded.CaseOutputs.Count);
            Assert.Same(Assert.Single(panel.Editor.Nodes.OfType<GetVarialbeNode>()).TriggerInput,
                Assert.Single(loaded.CaseOutputs[2].ConnectedOption));
            Assert.Same(loaded.CaseOutputs[2], Assert.Single(loaded.Execute(new()).ActiveOutputs));
        });
    }

    [Fact]
    public void VariablesRespectScopeAndReadinessDoesNotWrite()
    {
        RunSta(() =>
        {
            var key = "operation-test-" + Guid.NewGuid();
            var context = new EditorExecutionContext();
            var set = new SetVarialbeNode { VariableName = key, ValueJson = "null" };
            var get = new GetVarialbeNode { VariableName = key.ToUpperInvariant() };
            Assert.True(set.CanExecute(context).IsReady);
            Assert.Empty(context.Items);
            Assert.False(get.Execute(context).IsSuccess);
            Execute(set, context);
            Assert.Null(Execute(get, context));
            Assert.False(get.Execute(new()).IsSuccess);
            get.UseDefaultWhenMissing = true;
            get.DefaultJson = "42";
            Assert.Equal(42, Execute(get));
            try
            {
                set.Scope = VariableScope.Global;
                Assert.True(set.CanExecute(context).IsReady);
                Assert.False(GlobalDataStore.TryGet(key, out _));
                Execute(set, context);
                get.Scope = VariableScope.Global;
                Assert.Null(Execute(get));
                GlobalDataStore.Set(key, 9);
                Assert.Equal(9, Execute(get));
            }
            finally { GlobalDataStore.Remove(key); }
        });
    }

    [Fact]
    public void TypeOperationsConvertOrReportErrors()
    {
        RunSta(() =>
        {
            Assert.Equal(typeof(int), Execute(new GetTypeNode { ValueJson = "1" }));
            Assert.Null(Execute(new GetTypeNode()));
            Assert.Equal(true, Execute(new IsNotNullNode { ValueJson = "0" }));
            Assert.Equal(123, Execute(new TypeConverterNode()));
            Assert.Equal(true, Execute(new TypeConverterNode { ValueJson = "\"true\"", TargetType = WorkflowDataValueType.Boolean }));
            Assert.False(new TypeConverterNode { ValueJson = "null" }.Execute(new()).IsSuccess);
            Assert.False(new TypeConverterNode { ValueJson = "9999999999999999999" }.Execute(new()).IsSuccess);
            Assert.False(new TypeConverterNode { ValueJson = "\"bad\"" }.Execute(new()).IsSuccess);
            Assert.Null(Execute(new TypeConverterNode { ValueJson = "null", TargetType = WorkflowDataValueType.Object }));
        });
    }

    [Theory]
    [MemberData(nameof(NodeTypes))]
    public void CancellationClearsOutputsAndDoesNotExecute(Type type)
    {
        RunSta(() =>
        {
            var node = (OperationNode)Activator.CreateInstance(type)!;
            using var cancellation = new CancellationTokenSource();
            var context = new EditorExecutionContext(cancellation.Token);
            Execute(new SetVarialbeNode { ValueJson = "7" }, context);
            Execute(node, context);
            cancellation.Cancel();
            Assert.Throws<OperationCanceledException>(() => node.Execute(context));
            Assert.All(node.GetOutputOptions(), output => Assert.Null(output.Data));
        });
    }

    [Fact]
    public void InvalidCollectionsAndDefaultsDoNotThrowOutOfExecution()
    {
        RunSta(() =>
        {
            var count = new ArrayCountNode();
            foreach (var invalid in new object[] { "abc", 12, new int[2, 2], new Dictionary<string, int>() })
            {
                count.ArrayInput.Data = invalid;
                Assert.False(count.CanExecute(new()).IsReady);
                Assert.False(count.Execute(new()).IsSuccess);
                Assert.Null(count.Output.Data);
            }
            var dictionary = new DicGetKeysNode();
            dictionary.DictionaryInput.Data = new Dictionary<int, int> { [1] = 2 };
            Assert.False(dictionary.Execute(new()).IsSuccess);
            Assert.False(new ArrayCreateNode { ItemsJson = "null" }.Execute(new()).IsSuccess);
            Assert.False(new ArrayCreateNode { ItemsJson = "[1e9999]" }.Execute(new()).IsSuccess);
            Assert.False(new ArrayCreateNode { ItemsJson = "not json" }.Execute(new()).IsSuccess);
            Assert.False(new DicCreateNode { EntriesJson = "[]" }.Execute(new()).IsSuccess);
            Assert.False(new ArraySliceNode { Length = -2 }.Execute(new()).IsSuccess);
            Assert.False(new ArrayGetNode { ItemsJson = "[]" }.Execute(new()).IsSuccess);
            Assert.False(new SetVarialbeNode { VariableName = " " }.Execute(new()).IsSuccess);
        });
    }

    [Fact]
    public void LoadingVariableNodesDoesNotRestoreRuntimeValues()
    {
        RunSta(() =>
        {
            var key = "load-test-" + Guid.NewGuid();
            try
            {
                var panel = new XTNodeEditorPannel();
                OperationNodeCatalog.Register(panel);
                var writer = new SetVarialbeNode { VariableName = key, Scope = VariableScope.Global, ValueJson = "123" };
                panel.Editor.Nodes.Add(writer);
                Execute(writer);
                var bytes = panel.Editor.GetCanvasData();
                GlobalDataStore.Remove(key);
                panel.Editor.LoadCanvas(bytes);
                Assert.False(GlobalDataStore.TryGet(key, out _));
                var restored = Assert.Single(panel.Editor.Nodes.OfType<SetVarialbeNode>());
                Assert.True(restored.CanExecute(new()).IsReady);
                Assert.False(GlobalDataStore.TryGet(key, out _));
                Assert.Equal(123, Execute(restored));
            }
            finally { GlobalDataStore.Remove(key); }
        });
    }

    private static object? Execute(OperationNode node, EditorExecutionContext? context = null)
    {
        var result = node.Execute(context ?? new());
        Assert.True(result.IsSuccess, result.Message);
        return node.Output.Data;
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
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "运算节点测试超时。");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
