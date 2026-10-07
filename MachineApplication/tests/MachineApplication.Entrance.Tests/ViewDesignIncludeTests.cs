using System.ComponentModel;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Machine.ModuleLoad.Utils;
using Xunit;

namespace MachineApplication.Entrance.Tests;

public class ViewDesignIncludeTests
{
    [Fact]
    public void DotLoadsCompiledSharedStylesForProxyWithoutPageIdentity()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var proxy = new UserControl();
                DesignerProperties.SetIsInDesignMode(proxy, true);
                ViewDesignInclude.SetSource(proxy, ".");
                var brush = Assert.IsType<SolidColorBrush>(proxy.FindResource("DotSourceProbe"));
                Assert.Equal(Color.FromRgb(0x12, 0x34, 0x56), brush.Color);
                var runtime = new UserControl();
                ViewDesignInclude.SetSource(runtime, ".");
                Assert.Empty(runtime.Resources.MergedDictionaries);
            }
            catch (Exception error) { failure = error; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}