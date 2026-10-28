using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using Machine.ModuleLoad.Mvvm;
using MachineApplication.Entrance.Components;
using MaterialDesignThemes.Wpf;
using Xunit;

namespace MachineApplication.Entrance.Tests;

public class SnackTests
{
    [Fact]
    public void LongMessagesWrapWithoutTrimmingOrHeightLimit()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var snack = new Snack();
                var text = string.Concat(Enumerable.Repeat("配置已保存，开机自启动设置已更新。", 20));
                var message = new SnackbarMessage { Content = text };
                snack.Message = message;
                Assert.Equal(double.PositiveInfinity, SnackbarMessage.GetContentMaxHeight(snack));
                var content = Assert.IsType<TextBlock>(message.ContentTemplate.LoadContent());
                content.DataContext = text;
                Assert.Equal(TextWrapping.Wrap, content.TextWrapping);
                Assert.Equal(TextTrimming.None, content.TextTrimming);
                content.Measure(new Size(280, double.PositiveInfinity));
                var narrowHeight = content.DesiredSize.Height;
                Assert.True(narrowHeight > 36);
                Assert.True(content.DesiredSize.Width <= 280);
                content.Measure(new Size(500, double.PositiveInfinity));
                Assert.True(content.DesiredSize.Height < narrowHeight);
                snack.Message = new SnackbarMessage { Content = "短消息" };
                Assert.NotNull(snack.Message.ContentTemplate);
            }
            catch (Exception error) { failure = error; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(15)));
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
