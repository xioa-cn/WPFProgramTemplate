using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using Machine.ModuleLoad.Logger;
using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Extensions.TaskLists;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using MarkdownBlock = Markdig.Syntax.Block;
using MarkdownInline = Markdig.Syntax.Inlines.Inline;
using DocumentTable = System.Windows.Documents.Table;
using DocumentTableCell = System.Windows.Documents.TableCell;
using DocumentTableRow = System.Windows.Documents.TableRow;

namespace AIAgentFrameworkModule.Views.Controls;

/// <summary>
/// 将 Markdown 语法树转换为原生 WPF 文档，不使用浏览器、不解释 HTML、不执行脚本。
/// 生成中只增量追加纯文本，结束后一次性渲染 Markdown 和代码编辑器，避免反复重建界面。
/// </summary>
public sealed class MarkdownMessageView : RichTextBox
{
    public static readonly DependencyProperty CompactTypographyProperty = DependencyProperty.Register(
        nameof(CompactTypography), typeof(bool), typeof(MarkdownMessageView), new PropertyMetadata(false, OnThemeChanged));

    public bool CompactTypography
    {
        get => (bool)GetValue(CompactTypographyProperty);
        set => SetValue(CompactTypographyProperty, value);
    }

    /// <summary>绑定原始 Markdown；依赖属性让 WPF 能接收流式属性通知。</summary>
    public static readonly DependencyProperty MarkdownTextProperty = DependencyProperty.Register(
        nameof(MarkdownText), typeof(string), typeof(MarkdownMessageView),
        new PropertyMetadata("", OnMarkdownChanged));

    /// <summary>绑定消息生成状态，生成期间不用高成本的完整 Markdown 排版。</summary>
    public static readonly DependencyProperty IsGeneratingProperty = DependencyProperty.Register(
        nameof(IsGenerating), typeof(bool), typeof(MarkdownMessageView), new PropertyMetadata(false, OnMarkdownChanged));

    /// <summary>绑定主题背景画刷；主题切换会替换画刷实例，依赖属性随之变化并触发重新排版。</summary>
    private static readonly DependencyProperty ThemeWatcherProperty = DependencyProperty.Register(
        "ThemeWatcher", typeof(Brush), typeof(MarkdownMessageView), new PropertyMetadata(null, OnThemeChanged));

    // MarkdownPipeline 是 Markdig 的解析配置，只启用这里实际渲染的扩展。
    // UseEmojiAndSmiley 识别 :smile: 等短码，不会更改围栏代码和行内代码中的字符。
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UsePipeTables().UseTaskLists().UseEmphasisExtras().UseEmojiAndSmiley().UseAutoLinks().Build();
    // 颜色统一引用宿主主题资源，明暗基色与主色切换时由 WPF 更新，不在模块内固定深浅。
    private const string ThemeBackgroundKey = "MaterialDesign.Brush.Background";
    private const string ForegroundKey = "MaterialDesign.Brush.Foreground";
    private const string MutedKey = "MaterialDesign.Brush.ForegroundLight";
    private const string CardKey = "MaterialDesign.Brush.Card.Background";
    private const string SeparatorKey = "MaterialDesign.Brush.Separator.Background";
    private const string AccentKey = "MaterialDesign.Brush.Primary";
    private const string HighlightKey = "MaterialDesign.Brush.Chip.Background";
    private const string CodeSurfaceKey = "MaterialDesign.Brush.TextBox.FilledBackground";
    private DispatcherOperation? pendingRender;
    private string renderedText = "";
    private bool showingPreview;
    private bool hasRendered;

    /// <summary>为 true 时追加轻量文本；切换为 false 后完成富文本排版。</summary>
    public bool IsGenerating
    {
        get => (bool)GetValue(IsGeneratingProperty);
        set => SetValue(IsGeneratingProperty, value);
    }

    /// <summary>用于显示和复制的原始内容；渲染不会改写模型实际返回的文字。</summary>
    public string MarkdownText
    {
        get => (string)GetValue(MarkdownTextProperty);
        set => SetValue(MarkdownTextProperty, value);
    }

    /// <summary>
    /// 每次把文字排进文档后触发（流式追加和最终排版都会触发）。RichTextBox 换文档、追加文本
    /// 都不会通知布局系统，需要按内容重新量宽度的外层容器（气泡宽度收缩器）订阅它来让测量失效。
    /// </summary>
    public event EventHandler? ContentRendered;

    public MarkdownMessageView()
    {
        IsReadOnly = true;
        IsUndoEnabled = false;
        IsDocumentEnabled = true;
        Background = Brushes.Transparent;
        SetResourceReference(ForegroundProperty, ForegroundKey);
        // 订阅主题背景，主题切换后按新配色重新排版代码与链接。
        SetResourceReference(ThemeWatcherProperty, ThemeBackgroundKey);
        BorderThickness = new Thickness(0);
        Padding = new Thickness(0);
        VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    /// <summary>按当前主题底色的亮度选择代码配色，浅色主题改用深色字色，避免代码看不清。</summary>
    private static bool IsDarkSurface()
    {
        if (Application.Current?.TryFindResource(ThemeBackgroundKey) is SolidColorBrush background)
        {
            Color color = background.Color;
            return (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255 < 0.5;
        }

        return true;
    }

    /// <summary>只安排一次待执行的刷新，连续 token 共享同一个刷新周期。</summary>
    private static void OnMarkdownChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        MarkdownMessageView view = (MarkdownMessageView)sender;
        view.ScheduleRender();
    }

    /// <summary>主题切换后强制重新解析，让代码块、行内代码和链接使用当前主题的颜色。</summary>
    private static void OnThemeChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        MarkdownMessageView view = (MarkdownMessageView)sender;
        view.hasRendered = false;
        view.ScheduleRender();
    }

    /// <summary>页面重新显示时只处理有变化的文字，不重建已经完成的历史消息。</summary>
    private void OnLoaded(object sender, RoutedEventArgs args) => RenderMessage();

    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        pendingRender?.Abort();
        pendingRender = null;
    }

    private void ScheduleRender()
    {
        if (!IsLoaded || pendingRender is not null) return;
        pendingRender = Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            pendingRender = null;
            if (IsLoaded) RenderMessage();
        }));
    }

    /// <summary>解析 Markdown 语法树。解析或展示失败时回退纯文本，不中断聊天请求。</summary>
    private void RenderMessage()
    {
        string text = MarkdownText ?? "";
        if (hasRendered && renderedText == text && showingPreview == IsGenerating) return;
        if (IsGenerating)
        {
            // 复用当前 FlowDocument，只追加新增文本；不解析语法树，也不创建 AvalonEdit。
            if (!showingPreview || !text.StartsWith(renderedText, StringComparison.Ordinal))
            {
                Document = CreateMessageDocument();
                Document.Blocks.Add(new Paragraph { Margin = new Thickness(0) });
                renderedText = "";
            }
            AppendText(text.Substring(renderedText.Length));
            renderedText = text;
            showingPreview = true;
            hasRendered = true;
            ContentRendered?.Invoke(this, EventArgs.Empty);
            return;
        }

        FlowDocument document = CreateMessageDocument();
        try
        {
            // 仅在生成结束（包括取消、失败）后进行一次完整解析。
            MarkdownDocument parsed = Markdown.Parse(text, Pipeline);
            AddBlocks(parsed, document.Blocks, 0);
            if (CompactTypography && document.Blocks.LastBlock is { } lastBlock)
                lastBlock.Margin = new Thickness(lastBlock.Margin.Left, lastBlock.Margin.Top, lastBlock.Margin.Right, 0);
        }
        catch (Exception exception)
        {
            ReportRenderFailure("Markdown 文档解析", exception);
            document.Blocks.Clear();
            document.Blocks.Add(new Paragraph(new Run(text)));
        }
        Document = document;
        renderedText = text;
        showingPreview = false;
        hasRendered = true;
        ContentRendered?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>统一创建预览和最终排版的基础文档，避免生成结束时字号变化。</summary>
    private FlowDocument CreateMessageDocument()
    {
        FlowDocument document = new FlowDocument();
        document.PagePadding = new Thickness(0);
        document.FontFamily = new FontFamily(CompactTypography ? "Microsoft YaHei UI" : "Segoe UI, Microsoft YaHei UI, Segoe UI Emoji");
        document.FontSize = CompactTypography ? 14 : 15;
        document.LineHeight = CompactTypography ? 23 : 25;
        document.SetResourceReference(TextElement.ForegroundProperty, ForegroundKey);
        document.Background = Brushes.Transparent;
        return document;
    }

    /// <summary>按块生成标题、段落、列表、引用、表格和代码；限制递归层数避免恶意嵌套。</summary>
    private void AddBlocks(ContainerBlock source, BlockCollection destination, int depth)
    {
        if (depth > 32) return;
        foreach (MarkdownBlock block in source)
        {
            try
            {
                AddBlock(block, destination, depth);
            }
            catch (Exception exception)
            {
                // 只降级当前区块，不能因为一个代码框或表格失败就抹掉整条回答的排版。
                ReportRenderFailure("区块渲染：" + block.GetType().Name, exception);
                string sourceText = MarkdownText ?? "";
                int start = block.Span.Start;
                int end = Math.Min(block.Span.End, sourceText.Length - 1);
                string fallback = start >= 0 && end >= start ? sourceText.Substring(start, end - start + 1) : "（此区块暂时无法显示，可复制原始回答查看）";
                destination.Add(new Paragraph(new Run(fallback)) { Margin = new Thickness(0, 0, 0, 10) });
            }
        }
    }

    /// <summary>渲染一个独立语法区块；异常由外层按区块处理，不影响后续内容。</summary>
    private void AddBlock(MarkdownBlock block, BlockCollection destination, int depth)
    {
            if (block is CodeBlock code)
            {
                string language = code is FencedCodeBlock fenced ? fenced.Info ?? "" : "";
                destination.Add(new BlockUIContainer(CreateCodeBlock(code.Lines.ToString(), language)) { Margin = new Thickness(0, 10, 0, 14) });
            }
            else if (block is Markdig.Extensions.Tables.Table table)
            {
                destination.Add(CreateTable(table, depth + 1));
            }
            else if (block is ListBlock list)
            {
                System.Windows.Documents.List result = new System.Windows.Documents.List();
                result.MarkerStyle = list.IsOrdered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc;
                if (list.IsOrdered && int.TryParse(list.OrderedStart, out int start) && start > 0) result.StartIndex = start;
                result.Padding = new Thickness(22, 0, 0, 0);
                result.Margin = new Thickness(0, 4, 0, 10);
                foreach (MarkdownBlock item in list)
                {
                    if (item is not ContainerBlock content) continue;
                    System.Windows.Documents.ListItem entry = new System.Windows.Documents.ListItem();
                    AddBlocks(content, entry.Blocks, depth + 1);
                    result.ListItems.Add(entry);
                }
                destination.Add(result);
            }
            else if (block is QuoteBlock quote)
            {
                Section section = new Section();
                section.SetResourceReference(System.Windows.Documents.Block.BorderBrushProperty, AccentKey);
                section.BorderThickness = new Thickness(3, 0, 0, 0);
                section.Padding = new Thickness(14, 4, 0, 4);
                section.Margin = new Thickness(0, 8, 0, 12);
                AddBlocks(quote, section.Blocks, depth + 1);
                destination.Add(section);
            }
            else if (block is ThematicBreakBlock)
            {
                Paragraph separator = new Paragraph { BorderThickness = new Thickness(0, 0, 0, 1), Margin = new Thickness(0, 12, 0, 12) };
                separator.SetResourceReference(System.Windows.Documents.Block.BorderBrushProperty, SeparatorKey);
                destination.Add(separator);
            }
            else if (block is LeafBlock leaf)
            {
                Paragraph paragraph = new Paragraph { Margin = new Thickness(0, 0, 0, 10) };
                if (block is HeadingBlock heading)
                {
                    paragraph.FontSize = heading.Level == 1 ? 25 : heading.Level == 2 ? 21 : 17;
                    paragraph.FontWeight = FontWeights.SemiBold;
                    paragraph.Margin = new Thickness(0, 14, 0, 10);
                }
                if (leaf.Inline != null) AddInlines(leaf.Inline, paragraph.Inlines, depth + 1);
                else paragraph.Inlines.Add(new Run(leaf.Lines.ToString()));
                destination.Add(paragraph);
            }
            else if (block is ContainerBlock nested)
            {
                AddBlocks(nested, destination, depth + 1);
            }
    }

    private void AddInlines(ContainerInline source, InlineCollection destination, int depth)
    {
        if (depth > 32) return;
        for (MarkdownInline? inline = source.FirstChild; inline != null; inline = inline.NextSibling)
        {
            if (inline is LiteralInline literal)
                destination.Add(new Run(literal.Content.ToString()));
            else if (inline is HtmlEntityInline entity)
                destination.Add(new Run(entity.Transcoded.ToString()));
            else if (inline is CodeInline code)
            {
                Run codeRun = new Run(code.Content) { FontFamily = new FontFamily("Cascadia Mono, Consolas") };
                codeRun.SetResourceReference(TextElement.BackgroundProperty, CardKey);
                codeRun.SetResourceReference(TextElement.ForegroundProperty, ForegroundKey);
                destination.Add(codeRun);
            }
            else if (inline is LineBreakInline)
                destination.Add(new LineBreak());
            else if (inline is TaskList task)
            {
                Run check = new Run(task.Checked ? "☑ " : "☐ ");
                check.SetResourceReference(TextElement.ForegroundProperty, AccentKey);
                destination.Add(check);
            }
            else if (inline is EmphasisInline emphasis)
            {
                Span span = new Span();
                if (emphasis.DelimiterChar == '~' && emphasis.DelimiterCount >= 2) span.TextDecorations = TextDecorations.Strikethrough;
                else if (emphasis.DelimiterChar == '~') span.BaselineAlignment = BaselineAlignment.Subscript;
                else if (emphasis.DelimiterChar == '^') span.BaselineAlignment = BaselineAlignment.Superscript;
                else if (emphasis.DelimiterChar == '=')
                {
                    span.SetResourceReference(TextElement.BackgroundProperty, HighlightKey);
                    span.SetResourceReference(TextElement.ForegroundProperty, ForegroundKey);
                }
                else if (emphasis.DelimiterChar == '+') span.TextDecorations = TextDecorations.Underline;
                else if (emphasis.DelimiterCount >= 2) span.FontWeight = FontWeights.Bold;
                else span.FontStyle = FontStyles.Italic;
                AddInlines(emphasis, span.Inlines, depth + 1);
                destination.Add(span);
            }
            else if (inline is LinkInline link)
            {
                if (link.IsImage)
                {
                    Span description = new();
                    AddInlines(link, description.Inlines, depth + 1);
                    string label = new TextRange(description.ContentStart, description.ContentEnd).Text;
                    ChatImageView image = new(link.Url ?? "", label);
                    image.SetBinding(FrameworkElement.MaxWidthProperty, new Binding(nameof(ActualWidth)) { Source = this });
                    destination.Add(new InlineUIContainer(image) { BaselineAlignment = BaselineAlignment.Center });
                }
                else
                {
                    Span label = CreateLink(link.Url);
                    AddInlines(link, label.Inlines, depth + 1);
                    destination.Add(label);
                }
            }
            else if (inline is AutolinkInline autoLink)
            {
                Span label = CreateLink(autoLink.Url);
                label.Inlines.Add(new Run(autoLink.Url));
                destination.Add(label);
            }
            else if (inline is HtmlInline html)
                destination.Add(new Run(html.Tag));
            else if (inline is ContainerInline nested)
                AddInlines(nested, destination, depth + 1);
        }
    }

    /// <summary>仅允许 HTTP/HTTPS，点击后先确认目标地址；永不自动启动链接。</summary>
    private static Span CreateLink(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? address) || address.UserInfo.Length > 0 ||
            (address.Scheme != Uri.UriSchemeHttps && address.Scheme != Uri.UriSchemeHttp)) return new Span();
        Hyperlink link = new Hyperlink { ToolTip = address.AbsoluteUri };
        link.SetResourceReference(TextElement.ForegroundProperty, AccentKey);
        link.Click += (sender, args) =>
        {
            args.Handled = true;
            if (MessageBox.Show("是否在浏览器打开此地址？\n\n" + address.AbsoluteUri, "打开外部链接", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            try
            {
                Process.Start(new ProcessStartInfo(address.AbsoluteUri) { UseShellExecute = true });
            }
            catch (Exception)
            {
                MessageBox.Show("无法打开浏览器，请检查系统设置。", "打开失败");
            }
        };
        return link;
    }

    /// <summary>转换 Markdown 管道表格，支持表头和单元格中的行内 Markdown。</summary>
    private DocumentTable CreateTable(Markdig.Extensions.Tables.Table source, int depth)
    {
        // WPF 表格要求每行单元格数量一致，多出来的单元格会让布局阶段抛异常并整块降级为纯文本。
        // PipeTable 的列定义来自分隔行，但 Markdig 会把所有行补齐到最宽的一行，所以列数取两者的较大值。
        int columnCount = source.ColumnDefinitions.Count;
        foreach (MarkdownBlock block in source)
        {
            if (block is not Markdig.Extensions.Tables.TableRow row) continue;
            columnCount = Math.Max(columnCount, CountCells(row));
        }
        if (columnCount <= 0) columnCount = 1;

        DocumentTable table = new DocumentTable { CellSpacing = 0, Margin = new Thickness(0, 10, 0, 14) };
        for (int column = 0; column < columnCount; column++) table.Columns.Add(new TableColumn());
        TableRowGroup group = new TableRowGroup();
        table.RowGroups.Add(group);
        foreach (MarkdownBlock block in source)
        {
            if (block is not Markdig.Extensions.Tables.TableRow row) continue;
            DocumentTableRow result = new DocumentTableRow();
            int columnIndex = 0;
            foreach (MarkdownBlock child in row)
            {
                if (child is not Markdig.Extensions.Tables.TableCell cell) continue;
                DocumentTableCell target = CreateTableCell(row.IsHeader);
                // PipeTable 的 ColumnDefinitions 保存 :---、:---:、---: 指定的对齐方式。
                // Markdig 不填充 TableCell.ColumnIndex（始终为 -1），只能按单元格在行内的顺序取对齐方式。
                if (columnIndex < source.ColumnDefinitions.Count)
                {
                    TableColumnAlign? alignment = source.ColumnDefinitions[columnIndex].Alignment;
                    if (alignment == TableColumnAlign.Center) target.TextAlignment = TextAlignment.Center;
                    else if (alignment == TableColumnAlign.Right) target.TextAlignment = TextAlignment.Right;
                }
                AddBlocks(cell, target.Blocks, depth + 1);
                result.Cells.Add(target);
                columnIndex++;
            }
            // 单元格少于列数时补空单元格；行与列数量不一致时 WPF 无法布局整张表格。
            while (columnIndex < columnCount)
            {
                result.Cells.Add(CreateTableCell(false));
                columnIndex++;
            }
            group.Rows.Add(result);
        }
        return table;
    }

    /// <summary>统计一行里的单元格数量，用于推算表格的实际列数。</summary>
    private static int CountCells(Markdig.Extensions.Tables.TableRow row)
    {
        int count = 0;
        foreach (MarkdownBlock child in row)
        {
            if (child is Markdig.Extensions.Tables.TableCell) count++;
        }
        return count;
    }

    /// <summary>创建统一留白和主题边框的单元格，表头额外使用卡片底色。</summary>
    private static DocumentTableCell CreateTableCell(bool isHeader)
    {
        DocumentTableCell cell = new DocumentTableCell
        {
            Padding = new Thickness(10, 7, 10, 7), BorderThickness = new Thickness(0.5),
            Background = Brushes.Transparent,
            FontWeight = isHeader ? FontWeights.SemiBold : FontWeights.Normal
        };
        cell.SetResourceReference(DocumentTableCell.BorderBrushProperty, SeparatorKey);
        // 表头使用主题卡片底色，正文单元格保持透明，明暗主题都不需要单独配色。
        if (isHeader) cell.SetResourceReference(TextElement.BackgroundProperty, CardKey);
        return cell;
    }

    /// <summary>代码块使用 AvalonEdit 原生编辑器展示高亮和行号；复制的是未经转换的代码。</summary>
    private static UIElement CreateCodeBlock(string code, string info)
    {
        string language = info.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "text";
        FrameworkElement codeContent;
        try
        {
            // 只去掉 Markdown 在结尾附带的一个换行符；复制按钮仍使用完整原文。
            string displayCode = code;
            if (displayCode.EndsWith("\r\n", StringComparison.Ordinal)) displayCode = displayCode[..^2];
            else if (displayCode.EndsWith('\n') || displayCode.EndsWith('\r')) displayCode = displayCode[..^1];
            codeContent = CreateCodeEditor(displayCode, language);
        }
        catch (Exception exception)
        {
            // AvalonEdit 创建失败时仍保留代码框、语言标签和复制按钮，不丢弃整个 Markdown 文档。
            ReportRenderFailure("代码编辑器创建", exception);
            TextBox fallback = new TextBox
            {
                Text = code, IsReadOnly = true, AcceptsReturn = true,
                FontFamily = new FontFamily("Consolas"), FontSize = 13,
                BorderThickness = new Thickness(0), Padding = new Thickness(12),
                MaxHeight = 440, MinHeight = 48,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            fallback.SetResourceReference(ForegroundProperty, ForegroundKey);
            fallback.SetResourceReference(BackgroundProperty, CodeSurfaceKey);
            codeContent = fallback;
        }

        Button copy = new Button { Content = "复制代码", Padding = new Thickness(10, 4, 10, 4), Margin = new Thickness(8, 0, 0, 0) };
        copy.SetResourceReference(StyleProperty, "AI.Button");
        copy.Click += (sender, args) => CopyText(code, copy);
        DockPanel header = new DockPanel { Margin = new Thickness(12, 4, 8, 4), LastChildFill = true };
        DockPanel.SetDock(copy, Dock.Right);
        header.Children.Add(copy);
        TextBlock languageLabel = new TextBlock { Text = language, VerticalAlignment = VerticalAlignment.Center, FontSize = 12 };
        languageLabel.SetResourceReference(TextBlock.ForegroundProperty, MutedKey);
        header.Children.Add(languageLabel);
        StackPanel body = new StackPanel();
        body.Children.Add(header);
        body.Children.Add(codeContent);
        Border codeBlock = new Border { Child = body, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(9), ClipToBounds = true };
        codeBlock.SetResourceReference(Border.BackgroundProperty, CardKey);
        codeBlock.SetResourceReference(Border.BorderBrushProperty, SeparatorKey);
        // RequestBringIntoView 是控件请求“把自己滚入可见区域”的冒泡事件。
        // 点击代码、选择文字或复制按钮获得焦点时，只允许代码框内部处理滚动，
        // 到达代码块边界后设置 Handled，避免外层聊天列表也跟着跳到焦点位置。
        // 不拦截鼠标和键盘事件，保留选中、复制及代码框内部滚动能力。
        codeBlock.RequestBringIntoView += (sender, args) => args.Handled = true;
        return codeBlock;
    }

    /// <summary>创建只读代码编辑器，高亮失败时只取消配色，仍显示原始代码。</summary>
    private static TextEditor CreateCodeEditor(string code, string language)
    {
        TextEditor editor = new TextEditor();
        // TextEditor.Text 保存原始代码，IsReadOnly 禁止修改，SyntaxHighlighting 只影响颜色。
        editor.Text = code;
        editor.IsReadOnly = true;
        // Options 是 AvalonEdit 的编辑器设置；关闭自动链接和文档底部额外滚动空间。
        editor.Options.EnableHyperlinks = false;
        editor.Options.EnableEmailHyperlinks = false;
        editor.Options.AllowScrollBelowDocument = false;
        editor.ShowLineNumbers = true;
        editor.FontFamily = new FontFamily("Cascadia Mono, Consolas");
        editor.FontSize = 13;
        editor.SetResourceReference(BackgroundProperty, CodeSurfaceKey);
        editor.SetResourceReference(ForegroundProperty, ForegroundKey);
        editor.SetResourceReference(TextEditor.LineNumbersForegroundProperty, MutedKey);
        try
        {
            // 代码底色跟随主题，语法配色按当前底色在深色与浅色两套之间切换。
            editor.SyntaxHighlighting = CodeHighlighting.Find(language, IsDarkSurface());
        }
        catch (Exception exception)
        {
            ReportRenderFailure("代码高亮初始化", exception);
            editor.SyntaxHighlighting = null;
        }
        editor.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
        editor.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        editor.WordWrap = false;
        editor.Padding = new Thickness(10, 4, 10, 4);

        // 未进入可视树时只估算高度，不提前请求 TextView 的字体测量。
        UpdateCodeEditorHeight(editor);
        // Loaded 后再读取真实行高，字体测量失败时保留估算值。
        editor.Loaded += (sender, args) => UpdateCodeEditorHeight(editor);
        // SizeChanged 只在控件尺寸改变时触发；这里只关注宽度变化（例如调整窗口大小）。
        // 不再监听 ScrollChanged：光标滚动和内部范围变化不应反复修改外层消息高度。
        editor.SizeChanged += (sender, args) =>
        {
            if (!args.WidthChanged) return;
            // BeginInvoke 将测量延后到当前布局完成后，确保读到更新后的可视宽度。
            editor.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                if (editor.IsLoaded) UpdateCodeEditorHeight(editor);
            }));
        };
        return editor;
    }

    /// <summary>按实际行数、字体行高和内边距计算高度，超过 440 DIP 时使用内部滚动。</summary>
    private static void UpdateCodeEditorHeight(TextEditor editor)
    {
        // Document.LineCount 来自 AvalonEdit 文档，正确处理不同换行符，不改动原始代码。
        // TextView.DefaultLineHeight 是当前字体的默认行高，与 WPF 布局使用相同的 DIP 单位。
        double lineHeight = editor.FontSize * 1.5;
        if (editor.IsLoaded)
        {
            try
            {
                double measuredHeight = editor.TextArea.TextView.DefaultLineHeight;
                if (double.IsFinite(measuredHeight) && measuredHeight > 0) lineHeight = measuredHeight;
            }
            catch (Exception exception)
            {
                ReportRenderFailure("代码行高测量", exception);
            }
        }

        // 不再无条件预留水平滚动条高度，短行代码不需要这段空白。
        double scrollBarHeight = editor.ViewportWidth > 0 && editor.ExtentWidth > editor.ViewportWidth + 1
            ? SystemParameters.HorizontalScrollBarHeight : 0;
        double contentHeight = editor.Document.LineCount * lineHeight +
            editor.Padding.Top + editor.Padding.Bottom + scrollBarHeight + 2;
        double desiredHeight = Math.Clamp(Math.Ceiling(contentHeight), 30, 440);
        if (double.IsNaN(editor.Height) || Math.Abs(editor.Height - desiredHeight) > 0.5)
            editor.Height = desiredHeight;
    }

    /// <summary>记录失败阶段、异常类型和调用栈，不记录回答正文、密钥或异常消息。</summary>
    private static void ReportRenderFailure(string stage, Exception exception)
    {
        // 单个区块渲染失败会被降级成纯文本，必须写进宿主日志，否则只能看到排版错乱却查不到原因。
        GlobalLogger.Warn($"[AI.Markdown] {stage}：{exception.GetType().FullName}{Environment.NewLine}{exception.StackTrace}");
    }

    /// <summary>剪贴板被占用时给出反馈，不让复制操作导致界面崩溃。</summary>
    public static void CopyText(string text, Button button)
    {
        try
        {
            Clipboard.SetText(text);
            button.ToolTip = "已复制";
            if (button.Content is string) button.Content = "已复制";
        }
        catch (ExternalException)
        {
            button.ToolTip = "剪贴板正被占用，请重试";
        }
    }
}
