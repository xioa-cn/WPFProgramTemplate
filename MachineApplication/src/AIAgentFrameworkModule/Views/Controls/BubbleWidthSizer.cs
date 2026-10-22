using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace AIAgentFrameworkModule.Views.Controls;

/// <summary>
/// 让聊天气泡按内容自然宽度收缩。富文本控件默认按可用宽度排版，直接放进 Border 里会永远占满宽度，
/// 短回复就变成一整条空白卡片；只有出现表格、代码块等必须占宽的区块时才回退到可用宽度。
/// </summary>
public sealed class BubbleWidthSizer : Decorator
{
    /// <summary>气泡最小宽度，避免一两个字的气泡窄到看不清。</summary>
    private const double MinimumWidth = 120;

    /// <summary>估算余量，抵消行内加粗、行内代码比正文略宽的部分。</summary>
    private const double ExtraWidth = 8;

    /// <summary>列表标记和引用左边线占用的宽度，估算正文宽度时补回来。</summary>
    private const double ListIndent = 40;

    /// <summary>引用块左边线和内边距占用的宽度。</summary>
    private const double QuoteIndent = 16;

    /// <summary>流式期间长正文两次量宽度之间至少新增的字符数；短正文不受此限制，直接精确贴合。</summary>
    private const int MeasureInterval = 256;

    /// <summary>正文不超过这么多字符时每次都精确量宽度，量一次很便宜，气泡可以紧贴内容。</summary>
    private const int ShortTextLength = 512;

    /// <summary>流式期间宽度的量化步长，宽度变化次数越少，整篇重新断行的次数越少。</summary>
    private const double WidthStep = 32;

    /// <summary>正在监听排版事件的子控件；收缩器和它同生共死，只保留最新一个。</summary>
    private MarkdownMessageView? watched;

    /// <summary>流式期间已经用过的宽度，只增不减，避免文字每多一段就重排一次换行。</summary>
    private double streamingWidth;

    /// <summary>上次量宽度时的文字长度和消息对象，用于判断是否需要重新量、以及容器是否被复用到别的消息。</summary>
    private int measuredLength = -1;
    private object? measuredContext;

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Child is null) return new Size(0, 0);
        EnsureChildHook();
        if (double.IsInfinity(availableSize.Width))
        {
            Child.Measure(availableSize);
            return Child.DesiredSize;
        }

        double width = availableSize.Width;
        if (Child is MarkdownMessageView { IsGenerating: true } streaming)
        {
            width = Math.Min(StreamingWidth(streaming, width), width);
        }
        else
        {
            streamingWidth = 0;
            measuredLength = -1;
            if (Child is RichTextBox rich && TryMeasureNaturalWidth(rich, width, out double natural))
            {
                width = Math.Clamp(natural + ExtraWidth, Math.Min(MinimumWidth, width), width);
            }
        }

        Child.Measure(new Size(width, availableSize.Height));
        return new Size(width, Child.DesiredSize.Height);
    }

    /// <summary>算流式期间要用的宽度：按自然宽度量化后只增不减，量不了宽度的区块（表格、代码块）直接整宽。</summary>
    private double StreamingWidth(MarkdownMessageView markdown, double available)
    {
        string text = markdown.MarkdownText ?? "";
        // 列表容器会被复用到另一条消息上，宽度和已量长度都必须重新开始。
        if (!ReferenceEquals(measuredContext, markdown.DataContext) || text.Length < measuredLength)
        {
            measuredContext = markdown.DataContext;
            measuredLength = -1;
            streamingWidth = 0;
        }

        if (streamingWidth >= available) return available;

        // 短正文量宽度很便宜，每次都量，气泡紧贴内容；长正文按新增字符数间隔量，减少整篇重新量宽。
        bool shouldMeasure = streamingWidth <= 0 || text.Length <= ShortTextLength ||
            text.Length - measuredLength >= MeasureInterval;
        if (shouldMeasure)
        {
            measuredLength = text.Length;
            if (TryMeasureNaturalWidth(markdown, available, out double natural))
            {
                double quantized = Math.Ceiling((natural + ExtraWidth) / WidthStep) * WidthStep;
                streamingWidth = Math.Max(streamingWidth, Math.Clamp(quantized, Math.Min(MinimumWidth, available), available));
            }
            else
            {
                streamingWidth = available;
            }
        }
        return streamingWidth;
    }

    /// <summary>
    /// 订阅子控件的排版完成事件。生成结束时会用最终 Markdown 重新排版，换行宽度可能和预览不同；
    /// 而 RichTextBox 换文档不会让父级测量失效（宽度收窄后高度往往不变），必须自己重新量一次。
    /// </summary>
    private void EnsureChildHook()
    {
        if (ReferenceEquals(watched, Child)) return;
        if (watched is not null) watched.ContentRendered -= OnChildContentRendered;

        watched = Child as MarkdownMessageView;
        if (watched is null) return;
        watched.ContentRendered += OnChildContentRendered;
    }

    /// <summary>每排版一次就重新量一次宽度：流式期间宽度跟随内容增长，最终排版后收回到准确的内容宽度。</summary>
    private void OnChildContentRendered(object? sender, EventArgs args) => InvalidateMeasure();

    /// <summary>按上面量出的宽度排版，避免子元素仍然按可用宽度换行。</summary>
    protected override Size ArrangeOverride(Size finalSize)
    {
        Child?.Arrange(new Rect(finalSize));
        return finalSize;
    }

    /// <summary>
    /// 量出正文的自然宽度；遇到表格、代码块等宽内容时返回 false，交给调用方使用可用宽度。
    /// limit 是可用宽度，比它更宽的行最终都会被裁到可用宽度，量到那里就可以停，避免为超长行做无用的排版。
    /// </summary>
    private bool TryMeasureNaturalWidth(RichTextBox rich, double limit, out double width)
    {
        width = 0;
        double pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        return TryMeasureBlocks(rich.Document.Blocks, 0, pixelsPerDip, limit, ref width);
    }

    /// <summary>递归遍历区块；只有段落和列表能换算成可预估的文本宽度。</summary>
    private static bool TryMeasureBlocks(BlockCollection blocks, double indent, double pixelsPerDip, double limit, ref double width)
    {
        foreach (Block block in blocks)
        {
            switch (block)
            {
                case Paragraph paragraph:
                    if (ContainsEmbeddedContent(paragraph.Inlines)) return false;
                    width = Math.Max(width, indent + MeasureText(paragraph, pixelsPerDip, limit));
                    break;
                case System.Windows.Documents.List list:
                    foreach (ListItem listItem in list.ListItems)
                    {
                        if (!TryMeasureBlocks(listItem.Blocks, indent + ListIndent, pixelsPerDip, limit, ref width)) return false;
                    }
                    break;
                case Section section:
                    if (!TryMeasureBlocks(section.Blocks, indent + QuoteIndent, pixelsPerDip, limit, ref width)) return false;
                    break;
                case Table:
                case BlockUIContainer:
                    // 表格和代码块必须占满可用宽度，否则会被压成难读的窄条。
                    return false;
                default:
                    return false;
            }
        }
        return true;
    }

    private static bool ContainsEmbeddedContent(InlineCollection inlines)
    {
        foreach (Inline inline in inlines)
        {
            if (inline is InlineUIContainer || inline is Span span && ContainsEmbeddedContent(span.Inlines)) return true;
        }
        return false;
    }

    private static double MeasureText(Paragraph paragraph, double pixelsPerDip, double limit)
    {
        string text = new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text;
        Typeface typeface = new Typeface(paragraph.FontFamily, paragraph.FontStyle, paragraph.FontWeight, paragraph.FontStretch);
        double widest = 0;
        foreach (string rawLine in text.Split('\n'))
        {
            string line = rawLine.TrimEnd('\r');
            int probe = Math.Min(line.Length, 32);
            if (probe == 0) continue;
            while (true)
            {
                FormattedText formatted = new FormattedText(
                    line.Substring(0, probe), CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                    typeface, paragraph.FontSize, Brushes.Black, pixelsPerDip);
                double measured = formatted.WidthIncludingTrailingWhitespace;
                if (probe >= line.Length || measured >= limit)
                {
                    widest = Math.Max(widest, measured);
                    break;
                }
                probe = Math.Min(line.Length, probe * 2);
            }
        }
        return widest;
    }
}
