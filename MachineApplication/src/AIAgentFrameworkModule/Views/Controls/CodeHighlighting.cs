using System.IO;
using System.Xml;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;

namespace AIAgentFrameworkModule.Views.Controls;

/// <summary>为常见代码提供轻量语法配色，只做显示，不编译或执行代码。</summary>
internal static class CodeHighlighting
{
    // IHighlightingDefinition 是 AvalonEdit 的高亮规则定义。
    // 按“语言族 + 底色深浅”缓存，主题切换后重新解析一次，流式显示时不再重复解析。
    private static readonly Dictionary<string, IHighlightingDefinition> Definitions = new Dictionary<string, IHighlightingDefinition>();

    /// <summary>一套语法配色；深色底色用浅色系，浅色底色用深色系，两种主题下都保持可读。</summary>
    private readonly record struct CodePalette(string Comment, string String, string Keyword, string Number, string Tag, string Attribute);

    private static readonly CodePalette DarkPalette =
        new CodePalette("#94A89B", "#D8B58A", "#C6A5E6", "#9AD5CF", "#A1C7F1", "#C6A5E6");

    private static readonly CodePalette LightPalette =
        new CodePalette("#2E7D32", "#A31515", "#1B4FD8", "#0B7285", "#8B2252", "#B45309");

    /// <summary>识别常见语言标签；未知语言返回 null，保留原始代码并按纯文本显示。</summary>
    /// <param name="language">代码块标注的语言，例如 csharp、json。</param>
    /// <param name="darkSurface">当前主题背景是否为深色，决定使用哪一套语法配色。</param>
    public static IHighlightingDefinition? Find(string language, bool darkSurface)
    {
        string family;
        switch (language.ToLowerInvariant())
        {
            case "c#": case "cs": case "csharp": case "c": case "cpp": case "c++": case "java":
            case "js": case "javascript": case "ts": case "typescript": case "jsx": case "tsx":
                family = "code";
                break;
            case "py": case "python": case "sh": case "bash": case "shell": case "powershell": case "ps1": case "yaml": case "yml":
                family = "script";
                break;
            case "json": family = "json"; break;
            case "sql": family = "sql"; break;
            case "xml": case "html": case "xaml": family = "markup"; break;
            case "css": case "scss": family = "code"; break;
            default: return null;
        }

        string cacheKey = family + (darkSurface ? "|dark" : "|light");
        if (Definitions.TryGetValue(cacheKey, out IHighlightingDefinition? cached)) return cached;

        CodePalette palette = darkSurface ? DarkPalette : LightPalette;

        // XSHD 是 AvalonEdit 的 XML 高亮规则格式，这里只拼接我们定义的内容，不使用模型返回的 XML。
        string comment = family == "script" ? "#" : family == "sql" ? "--" : "//";
        string commentRule = family == "json" ? "" : "<Span color=\"Comment\" begin=\"" + comment + "\" />";
        string blockComment = family == "code" || family == "sql"
            ? "<Span color=\"Comment\" multiline=\"true\" begin=\"/\\*\" end=\"\\*/\" />" : "";
        string definition = """
            <SyntaxDefinition name="AI Code" xmlns="http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008">
              <Color name="Comment" foreground="%COMMENT%" />
              <Color name="String" foreground="%STRING%" />
              <Color name="Keyword" foreground="%KEYWORD%" />
              <Color name="Number" foreground="%NUMBER%" />
              <RuleSet ignoreCase="true">
                <Span color="String" begin="&quot;" end="&quot;">
                  <RuleSet><Span begin="\\" end="." /></RuleSet>
                </Span>
                <Span color="String" begin="'" end="'">
                  <RuleSet><Span begin="\\" end="." /></RuleSet>
                </Span>
            """ + commentRule + blockComment + """
                <Keywords color="Keyword">
                  <Word>using</Word><Word>namespace</Word><Word>class</Word><Word>interface</Word><Word>struct</Word>
                  <Word>public</Word><Word>private</Word><Word>protected</Word><Word>internal</Word><Word>static</Word>
                  <Word>readonly</Word><Word>sealed</Word><Word>override</Word><Word>virtual</Word><Word>abstract</Word>
                  <Word>async</Word><Word>await</Word><Word>return</Word><Word>new</Word><Word>var</Word><Word>void</Word>
                  <Word>string</Word><Word>int</Word><Word>bool</Word><Word>double</Word><Word>null</Word><Word>true</Word><Word>false</Word>
                  <Word>if</Word><Word>else</Word><Word>switch</Word><Word>case</Word><Word>break</Word><Word>continue</Word>
                  <Word>for</Word><Word>foreach</Word><Word>while</Word><Word>try</Word><Word>catch</Word><Word>finally</Word><Word>throw</Word>
                  <Word>get</Word><Word>set</Word><Word>value</Word><Word>this</Word><Word>base</Word><Word>in</Word><Word>out</Word>
                  <Word>function</Word><Word>const</Word><Word>let</Word><Word>export</Word><Word>default</Word><Word>import</Word>
                  <Word>def</Word><Word>from</Word><Word>as</Word><Word>with</Word><Word>pass</Word><Word>None</Word><Word>and</Word><Word>or</Word>
                  <Word>select</Word><Word>where</Word><Word>join</Word><Word>order</Word><Word>by</Word><Word>insert</Word>
                  <Word>into</Word><Word>update</Word><Word>delete</Word><Word>table</Word><Word>create</Word><Word>values</Word>
                </Keywords>
                <Rule color="Number">\b[0-9]+(\.[0-9]+)?\b</Rule>
              </RuleSet>
            </SyntaxDefinition>
            """;
        definition = definition
            .Replace("%COMMENT%", palette.Comment)
            .Replace("%STRING%", palette.String)
            .Replace("%KEYWORD%", palette.Keyword)
            .Replace("%NUMBER%", palette.Number);

        if (family == "markup")
        {
            definition = """
                <SyntaxDefinition name="AI Markup" xmlns="http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008">
                  <Color name="Comment" foreground="%COMMENT%" />
                  <Color name="String" foreground="%STRING%" />
                  <Color name="Tag" foreground="%TAG%" />
                  <Color name="Attribute" foreground="%ATTRIBUTE%" />
                  <RuleSet>
                    <Span color="Comment" multiline="true" begin="&lt;!--" end="--&gt;" />
                    <Span color="String" begin="&quot;" end="&quot;" />
                    <Span color="String" begin="'" end="'" />
                    <Rule color="Tag">&lt;/?[\w:.-]+|/?&gt;</Rule>
                    <Rule color="Attribute">[\w:.-]+(?=\s*=)</Rule>
                  </RuleSet>
                </SyntaxDefinition>
                """;
            definition = definition
                .Replace("%COMMENT%", palette.Comment)
                .Replace("%STRING%", palette.String)
                .Replace("%TAG%", palette.Tag)
                .Replace("%ATTRIBUTE%", palette.Attribute);
        }

        using StringReader text = new StringReader(definition);
        using XmlReader reader = XmlReader.Create(text, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit });
        // HighlightingLoader.Load 将 XSHD 转换为 SyntaxHighlighting 所需的定义，不调用编译器。
        IHighlightingDefinition result = HighlightingLoader.Load(reader, HighlightingManager.Instance);
        Definitions[cacheKey] = result;
        return result;
    }
}
