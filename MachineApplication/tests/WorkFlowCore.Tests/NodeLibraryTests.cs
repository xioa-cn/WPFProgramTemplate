using System.IO;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ST.Library.UI.NodeEditor;
using WorkFlowCore.Nodes.Data;
using WorkFlowCore.Nodes.Flow;
using WorkFlowCore.Nodes.Math;
using WorkFlowCore.Nodes.Script;
using WorkFlowCore.Nodes.Str;
using Xunit;

namespace WorkFlowCore.Tests;

public sealed class NodeLibraryTests
{
    [Fact]
    public void CategoryCountsTrackRegistrationRemovalAndSearch()
    {
        RunSta(() =>
        {
            var library = CreateLibrary();
            var tree = FindTree(library);
            var category = FindCategory(tree, "字符串");
            Assert.Equal(18, category.Items.Count);
            Assert.Equal("18", GetCount(category));
            Assert.False(library.AddNode(typeof(StrContainsNode)));
            Assert.True(library.RemoveNode(typeof(StrLenNode)));
            Assert.Equal("17", GetCount(FindCategory(tree, "字符串")));
            library.Search("StrContainsNode");
            category = Assert.Single(tree.Items.OfType<TreeViewItem>());
            Assert.Equal("1 / 17", GetCount(category));
            Assert.Contains("匹配 1 个，共 17 个节点", AutomationProperties.GetName(category));
            Assert.Equal(typeof(StrContainsNode), Assert.Single(category.Items.OfType<TreeViewItem>()).Tag);
            library.Search("不存在的节点");
            Assert.Empty(tree.Items);
            library.Search("");
            Assert.Equal("17", GetCount(FindCategory(tree, "字符串")));
            library.Clear();
            Assert.Empty(tree.Items);
        });
    }

    [Fact]
    public void SearchAndRegistrationPreserveCategoryExpansionAndNodeSelection()
    {
        RunSta(() =>
        {
            var library = CreateLibrary();
            var tree = FindTree(library);
            FindCategory(tree, "字符串").IsExpanded = false;
            library.Search("StrContainsNode");
            var category = FindCategory(tree, "字符串");
            Assert.True(category.IsExpanded);
            Assert.Single(category.Items.OfType<TreeViewItem>()).IsSelected = true;
            library.Search("");
            Assert.False(FindCategory(tree, "字符串").IsExpanded);
            Assert.Equal(typeof(StrContainsNode), library.SelectedNodeType);
            library.AddNode(typeof(StartNode));
            Assert.False(FindCategory(tree, "字符串").IsExpanded);
            Assert.Equal(typeof(StrContainsNode), library.SelectedNodeType);
        });
    }

    [Fact]
    public void CompactTemplatesRenderAtSidebarWidth()
    {
        RunSta(() =>
        {
            var library = CreateLibrary();
            library.Resources["MaterialDesign.Brush.Card.Background"] = new SolidColorBrush(Color.FromRgb(32, 40, 57));
            library.Resources["MaterialDesign.Brush.Foreground"] = new SolidColorBrush(Color.FromRgb(228, 234, 245));
            library.AddNode(typeof(ConstDataNode));
            library.AddNode(typeof(GlobalDataNode));
            library.AddNode(typeof(StartNode));
            library.AddNode(typeof(DelayNode));
            library.AddNode(typeof(EmptyBeatNode));
            library.AddNode(typeof(AddNode));
            library.AddNode(typeof(ScriptClassNode));
            library.AddNode(typeof(ScriptMethodNode));
            var tree = FindTree(library);
            foreach (var group in tree.Items.OfType<TreeViewItem>()) group.IsExpanded = Equals(group.Tag, "字符串");
            var category = FindCategory(tree, "字符串");
            var selected = category.Items.OfType<TreeViewItem>().First();
            selected.IsSelected = true;
            library.Measure(new Size(260, 700));
            library.Arrange(new Rect(0, 0, 260, 700));
            library.UpdateLayout();
            Assert.NotNull(tree.Template);
            Assert.NotNull(selected.Template.FindName("PART_Header", selected));
            var row = Assert.IsType<Border>(selected.Template.FindName("Row", selected));
            Assert.InRange(row.ActualHeight, 44, 46);
            Assert.True(row.ActualWidth > 160);
            var marker = Assert.IsType<Border>(selected.Template.FindName("SelectionMark", selected));
            Assert.Equal(Visibility.Visible, marker.Visibility);
            Assert.All(tree.Items.OfType<TreeViewItem>(), group => Assert.NotEmpty(GetCount(group)));
            var bitmap = new RenderTargetBitmap(260, 700, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(library);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(Path.Combine(Path.GetTempPath(), "MachineApplication-node-library.png"));
            encoder.Save(stream);
        });
    }

    private static XTNodeTreeView CreateLibrary()
    {
        var library = new XTNodeTreeView();
        foreach (var type in typeof(StringNode).Assembly.GetTypes().Where(type => typeof(StringNode).IsAssignableFrom(type) && !type.IsAbstract))
            Assert.True(library.AddNode(type));
        return library;
    }

    private static TreeView FindTree(XTNodeTreeView library) => Assert.Single(((DockPanel)library.Content).Children.OfType<TreeView>());
    private static TreeViewItem FindCategory(TreeView tree, string name) => tree.Items.OfType<TreeViewItem>().Single(group => Equals(group.Tag, name));
    private static string GetCount(TreeViewItem category) => ((TextBlock)((Grid)category.Header).Children.OfType<Border>().Single().Child).Text;

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
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "节点库测试超时。");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
