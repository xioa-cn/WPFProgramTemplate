using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using ST.Library.UI.NodeEditor;
using WorkFlowCore.Nodes.Data;
using WorkFlowCore.Nodes.Math;
using Xunit;

namespace WorkFlowCore.Tests;

public sealed class MathNodeTests
{
    [Theory]
    [InlineData(typeof(AddNode), 7.5, 2, 9.5)]
    [InlineData(typeof(SubNode), 7.5, 2, 5.5)]
    [InlineData(typeof(MulNode), -7.5, 2, -15)]
    [InlineData(typeof(DivNode), 7, 2, 3.5)]
    [InlineData(typeof(ModNode), -7.5, 2, -1.5)]
    public void BinaryOperationsUseConfiguredDefaults(Type nodeType, double left, double right, double expected)
    {
        RunSta(() =>
        {
            var node = (BinaryMathNode)Activator.CreateInstance(nodeType)!;
            node.LeftValue = left;
            node.RightValue = right;
            Assert.True(node.CanExecute(new()).IsReady);
            AssertResult(node, expected);
        });
    }

    [Fact]
    public void NumericInputsSupportMixedClrTypesAndOverrideDefaults()
    {
        RunSta(() =>
        {
            var node = new AddNode { LeftValue = 100, RightValue = 100 };
            object[] numbers = [(byte)2, (sbyte)2, (short)2, (ushort)2, 2, 2u, 2L, 2UL, 2f, 2d, 2m];
            foreach (var number in numbers)
            {
                node.LeftInput.Data = number;
                node.RightInput.Data = 0.5m;
                AssertResult(node, 2.5);
            }
        });
    }

    [Theory]
    [InlineData(-5, 5)]
    [InlineData(0, 0)]
    [InlineData(2.5, 2.5)]
    public void AbsoluteValueHandlesSigns(double value, double expected)
    {
        RunSta(() => AssertResult(new AbsNode { Value = value }, expected));
    }

    [Theory]
    [InlineData(-5, 0)]
    [InlineData(0, 0)]
    [InlineData(0.5, 0.5)]
    [InlineData(1, 1)]
    [InlineData(5, 1)]
    public void ClampIncludesBothBounds(double value, double expected)
    {
        RunSta(() => AssertResult(new ClampNode { Value = value }, expected));
    }

    [Fact]
    public void InvalidInputsAndOverflowFailWithoutLeavingOldOutput()
    {
        RunSta(() =>
        {
            var node = new AddNode { LeftValue = 2, RightValue = 3 };
            object[] invalid = ["2", true, DateTime.UnixEpoch, new object(), double.NaN, double.PositiveInfinity, double.NegativeInfinity];
            foreach (var value in invalid)
            {
                node.LeftInput.Data = null;
                AssertResult(node, 5);
                node.LeftInput.Data = value;
                Assert.False(node.CanExecute(new()).IsReady);
                AssertFailure(node);
            }
            AssertFailure(new MulNode { LeftValue = double.MaxValue, RightValue = 2 });
            AssertFailure(new AbsNode { Value = double.NaN });
        });
    }

    [Fact]
    public void InvalidDivisorsAndReversedBoundsAreRejected()
    {
        RunSta(() =>
        {
            MathNode[] nodes =
            [
                new DivNode { RightValue = 0 },
                new ModNode { RightValue = -0.0 },
                new ClampNode { Minimum = 2, Maximum = 1 },
                new RandomNode { Minimum = 2, Maximum = 1 }
            ];
            foreach (var node in nodes)
            {
                Assert.False(node.CanExecute(new()).IsReady);
                AssertFailure(node);
            }
            AssertResult(new ClampNode { Minimum = 2, Maximum = 2, Value = 8 }, 2);
        });
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-10, -2)]
    [InlineData(-1.7976931348623157E+308, 1.7976931348623157E+308)]
    [InlineData(1.7976931348623155E+308, 1.7976931348623157E+308)]
    [InlineData(-5E-324, 0)]
    public void RandomValuesStayInHalfOpenRange(double minimum, double maximum)
    {
        RunSta(() =>
        {
            var node = new RandomNode { Minimum = minimum, Maximum = maximum };
            for (var index = 0; index < 1000; index++)
            {
                Assert.True(node.CanExecute(new()).IsReady);
                Assert.True(node.Execute(new()).IsSuccess);
                var result = Assert.IsType<double>(node.Output.Data);
                Assert.True(double.IsFinite(result));
                Assert.True(result >= minimum && result < maximum);
            }
            node.Minimum = maximum;
            AssertResult(node, maximum);
        });
    }

    [Fact]
    public void ConnectionsReceiveValuesWithoutExecutingAndDisconnectRestoresDefault()
    {
        RunSta(() =>
        {
            var editor = new XTNodeEditor();
            var source = new ConstDataNode { ValueType = WorkflowDataValueType.Int32, Value = "4" };
            var add = new AddNode { LeftValue = 1, RightValue = 2 };
            var absolute = new AbsNode();
            editor.Nodes.Add(source);
            editor.Nodes.Add(add);
            editor.Nodes.Add(absolute);
            Assert.Equal(ConnectionStatus.Connected, source.Output.ConnectOption(add.LeftInput));
            Assert.Equal(ConnectionStatus.Connected, add.Output.ConnectOption(absolute.Input));
            Assert.Null(add.Output.Data);
            AssertResult(add, 6);
            Assert.Null(absolute.Output.Data);
            AssertResult(absolute, 6);
            source.Value = "-10";
            Assert.True(source.Execute(new()).IsSuccess);
            AssertResult(add, -8);
            AssertResult(absolute, 8);
            source.Output.DisConnectOption(add.LeftInput);
            Assert.Null(add.LeftInput.Data);
            AssertResult(add, 3);
        });
    }

    [Fact]
    public void ConnectedMissingValuesNeverUseDefaultsDuringExecution()
    {
        RunSta(() =>
        {
            var editor = new XTNodeEditor();
            var source = new AddNode { LeftValue = 4 };
            var target = new AbsNode { Value = 99 };
            editor.Nodes.Add(source);
            editor.Nodes.Add(target);
            Assert.Equal(ConnectionStatus.Connected, source.Output.ConnectOption(target.Input));
            Assert.True(target.CanExecute(new()).IsReady);
            AssertFailure(target);
            AssertResult(source, 4);
            AssertResult(target, 4);
            source.Output.TransferData(null);
            AssertFailure(target);
        });
    }

    [Fact]
    public void CancellationAndNullContextAreNotConvertedToFailures()
    {
        RunSta(() =>
        {
            var node = new RandomNode();
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            var context = new EditorExecutionContext(cancellation.Token);
            Assert.Throws<OperationCanceledException>(() => node.CanExecute(context));
            Assert.Throws<OperationCanceledException>(() => node.Execute(context));
            Assert.Throws<ArgumentNullException>(() => node.CanExecute(null!));
            Assert.Throws<ArgumentNullException>(() => node.Execute(null!));
            Assert.Null(node.Output.Data);
        });
    }

    [Fact]
    public void AllNodesAreDiscoverableAndPersistSettingsAndConnections()
    {
        RunSta(() =>
        {
            var editor = new XTNodeEditor();
            MathNode[] nodes = [new AddNode(), new SubNode(), new MulNode(), new DivNode(), new ModNode(), new AbsNode(), new ClampNode(), new RandomNode(), new RoundNode(), new SignNode(), new FloorNode(), new SqrtNode(), new SinNode(), new CosNode(), new TanNode(), new CeilingNode()];
            foreach (var node in nodes)
            {
                Assert.NotNull(node.GetType().GetCustomAttribute<XTNodeAttribute>());
                editor.RegisterNodeType(node.GetType());
                editor.Nodes.Add(node);
                node.NodeName = "测试" + node.GetType().Name;
                foreach (var property in node.GetType().GetProperties().Where(property =>
                             property.PropertyType == typeof(double) && property.GetCustomAttribute<XTNodePropertyAttribute>() is not null))
                    property.SetValue(node, property.Name == "Maximum" ? 12.5 : 2.5);
            }
            var add = (AddNode)nodes[0];
            var absolute = (AbsNode)nodes[5];
            Assert.Equal(ConnectionStatus.Connected, add.Output.ConnectOption(absolute.Input));
            var saved = editor.GetCanvasData();
            editor.LoadCanvas(saved);
            var restored = editor.Nodes.Cast<MathNode>().ToArray();
            Assert.Equal(16, restored.Length);
            foreach (var node in restored)
            {
                Assert.Equal("测试" + node.GetType().Name, node.NodeName);
                Assert.Null(node.Output.Data);
                Assert.NotEmpty(node.Output.Description);
                Assert.NotEqual("结果 · Double", node.Output.Text);
                foreach (var property in node.GetType().GetProperties().Where(property =>
                             property.PropertyType == typeof(double) && property.GetCustomAttribute<XTNodePropertyAttribute>() is not null))
                    Assert.Equal(property.Name == "Maximum" ? 12.5 : 2.5, Assert.IsType<double>(property.GetValue(node)));
            }
            Assert.Single(restored.OfType<AddNode>().Single().Output.ConnectedOption);
            AssertResult(restored.OfType<AddNode>().Single(), 5);
            AssertResult(restored.OfType<AbsNode>().Single(), 5);
        });
    }

    [Theory]
    [InlineData(typeof(RoundNode), 2.5, 3)]
    [InlineData(typeof(RoundNode), -2.5, -3)]
    [InlineData(typeof(SignNode), -3.5, -1)]
    [InlineData(typeof(SignNode), 0, 0)]
    [InlineData(typeof(SignNode), 3.5, 1)]
    [InlineData(typeof(FloorNode), 1.8, 1)]
    [InlineData(typeof(FloorNode), -1.2, -2)]
    [InlineData(typeof(FloorNode), 0, 0)]
    [InlineData(typeof(CeilingNode), 1.2, 2)]
    [InlineData(typeof(CeilingNode), -1.8, -1)]
    [InlineData(typeof(CeilingNode), 0, 0)]
    [InlineData(typeof(CeilingNode), 3, 3)]
    [InlineData(typeof(CeilingNode), -3, -3)]
    [InlineData(typeof(CeilingNode), double.Epsilon, 1)]
    [InlineData(typeof(CeilingNode), -double.Epsilon, 0)]
    [InlineData(typeof(CeilingNode), double.MaxValue, double.MaxValue)]
    [InlineData(typeof(CeilingNode), double.MinValue, double.MinValue)]
    [InlineData(typeof(SqrtNode), 9, 3)]
    [InlineData(typeof(SqrtNode), 0.25, 0.5)]
    [InlineData(typeof(SqrtNode), 0, 0)]
    [InlineData(typeof(SinNode), 0, 0)]
    [InlineData(typeof(SinNode), System.Math.PI / 2, 1)]
    [InlineData(typeof(SinNode), -System.Math.PI / 2, -1)]
    [InlineData(typeof(CosNode), 0, 1)]
    [InlineData(typeof(CosNode), System.Math.PI, -1)]
    [InlineData(typeof(TanNode), 0, 0)]
    [InlineData(typeof(TanNode), System.Math.PI / 4, 1)]
    [InlineData(typeof(TanNode), -System.Math.PI / 4, -1)]
    public void NewUnaryNodesCalculateDefaultsAndTransferredInputs(Type nodeType, double value, double expected)
    {
        RunSta(() =>
        {
            var node = (UnaryMathNode)Activator.CreateInstance(nodeType)!;
            node.Value = value;
            Assert.True(node.CanExecute(new()).IsReady);
            Assert.True(node.Execute(new()).IsSuccess);
            Assert.Equal(expected, Assert.IsType<double>(node.Output.Data), 12);
            var editor = new XTNodeEditor();
            var source = new ConstDataNode { ValueType = WorkflowDataValueType.Double, Value = "0" };
            editor.Nodes.Add(source);
            editor.Nodes.Add(node);
            Assert.True(node.Input.IsUsingDefaultValue);
            Assert.False(node.Output.IsUsingDefaultValue);
            Assert.Equal(ConnectionStatus.Connected, source.Output.ConnectOption(node.Input));
            Assert.False(node.Input.IsUsingDefaultValue);
            Assert.True(source.Execute(new()).IsSuccess);
            AssertResult(node, node is CosNode ? 1 : 0);
            source.Output.DisConnectOption(node.Input);
            Assert.True(node.Input.IsUsingDefaultValue);
        });
    }

    [Theory]
    [InlineData(MidpointRounding.AwayFromZero, 1.125, 1.13)]
    [InlineData(MidpointRounding.ToEven, 1.125, 1.12)]
    [InlineData(MidpointRounding.ToZero, -1.125, -1.12)]
    [InlineData(MidpointRounding.ToNegativeInfinity, -1.125, -1.13)]
    [InlineData(MidpointRounding.ToPositiveInfinity, 1.125, 1.13)]
    public void RoundSupportsConfiguredPrecisionAndModes(MidpointRounding mode, double value, double expected)
    {
        RunSta(() => AssertResult(new RoundNode { Value = value, Digits = 2, RoundingMode = mode }, expected));
    }

    [Fact]
    public void RoundRejectsInvalidSettingsAndPersistsValidSettings()
    {
        RunSta(() =>
        {
            var node = new RoundNode { Value = 1.125, Digits = 2, RoundingMode = MidpointRounding.ToEven };
            Assert.Throws<ArgumentOutOfRangeException>(() => node.Digits = -1);
            Assert.Throws<ArgumentOutOfRangeException>(() => node.Digits = 16);
            Assert.Throws<ArgumentOutOfRangeException>(() => node.RoundingMode = (MidpointRounding)99);
            Assert.Equal(2, node.Digits);
            Assert.Equal(MidpointRounding.ToEven, node.RoundingMode);
            node.Digits = 15;
            AssertResult(node, 1.125);
            node.Digits = 2;
            var editor = new XTNodeEditor();
            editor.RegisterNodeType(typeof(RoundNode));
            editor.Nodes.Add(node);
            editor.LoadCanvas(editor.GetCanvasData());
            var restored = Assert.Single(editor.Nodes.Cast<RoundNode>());
            Assert.Equal(2, restored.Digits);
            Assert.Equal(MidpointRounding.ToEven, restored.RoundingMode);
            AssertResult(restored, 1.12);
        });
    }

    [Theory]
    [InlineData(typeof(RoundNode))]
    [InlineData(typeof(SignNode))]
    [InlineData(typeof(FloorNode))]
    [InlineData(typeof(CeilingNode))]
    [InlineData(typeof(SqrtNode))]
    [InlineData(typeof(SinNode))]
    [InlineData(typeof(CosNode))]
    [InlineData(typeof(TanNode))]
    public void NewUnaryNodesRejectNonNumericAndNonFiniteInputs(Type nodeType)
    {
        RunSta(() =>
        {
            var node = (UnaryMathNode)Activator.CreateInstance(nodeType)!;
            foreach (var invalid in new object[] { "3", true, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            {
                node.Input.Data = 4m;
                Assert.False(node.Input.IsUsingDefaultValue);
                Assert.True(node.Execute(new()).IsSuccess);
                node.Input.Data = invalid;
                Assert.False(node.CanExecute(new()).IsReady);
                AssertFailure(node);
            }
            node.Input.Data = null;
            Assert.True(node.Input.IsUsingDefaultValue);
        });
    }

    [Fact]
    public void SqrtRejectsNegativeValuesAndClearsPreviousResult()
    {
        RunSta(() =>
        {
            var node = new SqrtNode { Value = 9 };
            AssertResult(node, 3);
            node.Value = -1;
            Assert.False(node.CanExecute(new()).IsReady);
            AssertFailure(node);
            node.Input.Data = -0.25;
            AssertFailure(node);
            node.Input.Data = -0.0;
            AssertResult(node, 0);
        });
    }

    [Fact]
    public void PropertyPanelExplainsHowEachMathOutputIsCalculated()
    {
        RunSta(() =>
        {
            var panel = new XTNodePropertyGrid();
            var types = typeof(MathNode).Assembly.GetTypes().Where(type => typeof(MathNode).IsAssignableFrom(type) && !type.IsAbstract);
            foreach (var type in types)
            {
                var node = (MathNode)Activator.CreateInstance(type)!;
                Assert.NotEmpty(node.Output.Description);
                panel.SetNode(node);
                var text = EnumerateText(panel).ToArray();
                Assert.Contains("输出", text);
                Assert.Contains(text, value => value.Contains(node.Output.Description) && value.Contains("Double") && value.Contains(node.Output.Text));
            }
        });
    }

    private static IEnumerable<string> EnumerateText(DependencyObject parent)
    {
        if (parent is TextBlock text) yield return text.Text;
        foreach (var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())
            foreach (var value in EnumerateText(child))
                yield return value;
    }

    private static void AssertResult(MathNode node, double expected)
    {
        var result = node.Execute(new());
        Assert.True(result.IsSuccess, result.Message);
        Assert.Same(node.Output, Assert.Single(result.ActiveOutputs));
        Assert.Equal(expected, Assert.IsType<double>(node.Output.Data));
        Assert.NotEmpty(node.RuntimeText);
    }

    private static void AssertFailure(MathNode node)
    {
        var result = node.Execute(new());
        Assert.False(result.IsSuccess);
        Assert.NotEmpty(result.Message);
        Assert.Empty(result.ActiveOutputs);
        Assert.Null(node.Output.Data);
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
