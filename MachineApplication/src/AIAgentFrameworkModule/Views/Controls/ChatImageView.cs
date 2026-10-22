using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using AIAgentFrameworkModule.Services;

namespace AIAgentFrameworkModule.Views.Controls;

internal sealed class ChatImageView : Border
{
    private readonly string source;
    private readonly Image image = new() { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, MaxHeight = 420 };
    private readonly TextBlock status = new() { Text = "正在加载图片…", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(8) };
    private readonly Button retry = new() { Content = "重新加载", HorizontalAlignment = HorizontalAlignment.Left, Visibility = Visibility.Collapsed };
    private CancellationTokenSource? loading;

    internal ChatImageView(string source, string description)
    {
        this.source = source;
        Width = 640;
        Margin = new Thickness(0, 8, 0, 8);
        CornerRadius = new CornerRadius(8);
        ClipToBounds = true;
        SetResourceReference(BackgroundProperty, "MaterialDesign.Brush.Card.Background");
        status.SetResourceReference(TextBlock.ForegroundProperty, "MaterialDesign.Brush.ForegroundLight");
        AutomationProperties.SetName(image, string.IsNullOrWhiteSpace(description) ? "助手返回的图片" : description);
        StackPanel panel = new();
        panel.Children.Add(image);
        panel.Children.Add(status);
        panel.Children.Add(retry);
        if (!string.IsNullOrWhiteSpace(description))
        {
            TextBlock caption = new() { Text = description, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0), FontSize = 12 };
            caption.SetResourceReference(TextBlock.ForegroundProperty, "MaterialDesign.Brush.ForegroundLight");
            panel.Children.Add(caption);
        }
        Child = panel;
        Loaded += async (_, _) => await LoadAsync();
        Unloaded += (_, _) =>
        {
            CancellationTokenSource? previous = loading;
            loading = null;
            previous?.Cancel();
        };
        retry.Click += async (_, args) =>
        {
            args.Handled = true;
            await LoadAsync();
        };
    }

    private async Task LoadAsync()
    {
        if (loading is not null || image.Source is not null || !IsLoaded) return;
        using CancellationTokenSource current = new();
        loading = current;
        retry.Visibility = Visibility.Collapsed;
        status.Text = "正在加载图片…";
        status.Visibility = Visibility.Visible;
        try
        {
            var bitmap = await ChatImageLoader.LoadAsync(source, current.Token);
            if (!ReferenceEquals(loading, current) || !IsLoaded) return;
            image.Source = bitmap;
            status.Visibility = Visibility.Collapsed;
        }
        catch (OperationCanceledException) when (current.IsCancellationRequested)
        {
        }
        catch (Exception)
        {
            if (!ReferenceEquals(loading, current) || !IsLoaded) return;
            status.Text = "图片加载失败：链接可能失效、格式不支持、超过大小限制或无法访问。";
            retry.Visibility = Visibility.Visible;
        }
        finally
        {
            if (ReferenceEquals(loading, current)) loading = null;
        }
    }
}
