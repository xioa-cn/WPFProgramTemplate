using System.Runtime.ExceptionServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using ST.Library.UI.NodeEditor;
using WorkFlowCore.Nodes.Operation;
using Xunit;

namespace WorkFlowCore.Tests;

public sealed class PropertyEnumDisplayTests
{
    [Fact]
    public void ComparisonDropdownShowsChineseAndPersistsOriginalEnumNames()
    {
        RunSta(() =>
        {
            var node = new SizeComparisonNode();
            var panel = new XTNodePropertyGrid();
            panel.SetNode(node);
            var combo = Assert.Single(Descendants<ComboBox>(panel));
            Assert.Equal(new[] { "大于", "大于等于", "小于", "小于等于" },
                combo.Items.Cast<object>().Select(item => item.GetType().GetProperty("Text")!.GetValue(item)));
            Assert.Equal(ComparisonMode.Greater, combo.SelectedValue);
            combo.SelectedValue = ComparisonMode.LessOrEqual;
            Assert.Equal(ComparisonMode.LessOrEqual, node.Mode);
            Assert.Equal(ComparisonMode.LessOrEqual, Assert.Single(Descendants<ComboBox>(panel)).SelectedValue);
            var editor = new XTNodeEditor();
            editor.RegisterNodeType(typeof(SizeComparisonNode));
            editor.Nodes.Add(node);
            var bytes = editor.GetCanvasData();
            var document = System.Text.Json.JsonSerializer.Deserialize<XTNodeEditor.CanvasDocument>(bytes)!;
            Assert.Equal("LessOrEqual", Encoding.UTF8.GetString(Assert.Single(document.Nodes).Properties["Mode"]));
            editor.LoadCanvas(bytes);
            Assert.Equal(ComparisonMode.LessOrEqual, Assert.IsType<SizeComparisonNode>(Assert.Single(editor.Nodes)).Mode);
        });
    }

    [Fact]
    public void UndescribedEnumsRetainOriginalLabelsAndSelection()
    {
        RunSta(() =>
        {
            var node = new SetVarialbeNode();
            var panel = new XTNodePropertyGrid();
            panel.SetNode(node);
            var combo = Assert.Single(Descendants<ComboBox>(panel));
            Assert.Equal(new[] { "Context", "Global" },
                combo.Items.Cast<object>().Select(item => item.GetType().GetProperty("Text")!.GetValue(item)));
            combo.SelectedValue = VariableScope.Global;
            Assert.Equal(VariableScope.Global, node.Scope);
        });
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        if (parent is T match) yield return match;
        foreach (var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())
            foreach (var item in Descendants<T>(child)) yield return item;
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
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "属性枚举测试超时。");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
