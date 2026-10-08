using System.IO;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Markup;
using System.Xml.Linq;
using Xunit;

namespace WorkFlowCore.Tests;

public sealed class ScriptWindowXamlTests
{
    [Fact]
    public void MainWindowMarkupLoadsAndLaysOutWithoutApplicationResources()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            Window? window = null;
            try
            {
                var directory = new DirectoryInfo(AppContext.BaseDirectory);
                while (directory is not null && !File.Exists(Path.Combine(directory.FullName,
                           "src/WorkFlowCore/Nodes/Script/Script/MainWindow.xaml"))) directory = directory.Parent;
                Assert.NotNull(directory);
                var document = XDocument.Load(Path.Combine(directory.FullName,
                    "src/WorkFlowCore/Nodes/Script/Script/MainWindow.xaml"));
                document.Root!.Attribute(XName.Get("Class", "http://schemas.microsoft.com/winfx/2006/xaml"))!.Remove();
                document.Root.SetAttributeValue(XNamespace.Xmlns + "controls", "clr-namespace:CsxPad.Wpf.Controls;assembly=WorkFlowCore");
                foreach (var element in document.Descendants().Where(element =>
                             element.Name.NamespaceName == "clr-namespace:CsxPad.Wpf.Controls"))
                    element.Name = XName.Get(element.Name.LocalName, "clr-namespace:CsxPad.Wpf.Controls;assembly=WorkFlowCore");
                foreach (var attribute in document.Descendants().Attributes().Where(attribute =>
                             attribute.Name.LocalName is "PreviewKeyDown" or "DragDelta").ToArray()) attribute.Remove();
                _ = System.IO.Packaging.PackUriHelper.Create(new Uri("http://localhost/"));
                var context = new ParserContext
                {
                    BaseUri = new Uri("pack://application:,,,/WorkFlowCore;component/Nodes/Script/Script/MainWindow.xaml")
                };
                window = (Window)XamlReader.Parse(document.ToString(), context);
                window.Measure(new Size(1480, 900));
                window.Arrange(new Rect(0, 0, 1480, 900));
                window.UpdateLayout();
                Assert.NotNull(window.FindResource("IdeGlyphStyle"));
                Assert.NotNull(window.Content);
            }
            catch (Exception exception) { failure = exception; }
            finally { window?.Close(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "XAML 加载测试超时。");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
