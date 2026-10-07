using System.Collections.Immutable;
using System.Reflection;
using I18nExtensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace I18n.LangsGenerator.Tests;

public class LangGeneratorTests
{
    private const string Source = "public partial class TestLang : I18nExtensions.LangBase { }";
    private static readonly CSharpParseOptions ParseOptions = new(LanguageVersion.Latest);
    private static readonly ImmutableArray<MetadataReference> References =
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
        .Append(typeof(LangBase).Assembly.Location).Distinct()
        .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path)).ToImmutableArray();

    [Fact]
    public void InitializesNestedTranslationsAndReadsChangedCulture()
    {
        var result = Run(Source,
            ("Resources/lang.zh.json", "{\"Global\":{\"AppName\":\"\\u684c\\u9762\",\"Menu\":{\"Title\":\"Menu\"}}}"),
            ("Resources/lang.en.json", """{"Global":{"AppName":"Desktop \"app\"\nC:\\temp"},"OnlyEnglish":"Yes"}"""));
        AssertValid(result);
        var generated = result.Driver.GetRunResult().Results.Single().GeneratedSources.Single().SourceText.ToString();
        Assert.Equal("TestLang.g.cs", result.Driver.GetRunResult().Results.Single().GeneratedSources.Single().HintName);
        Assert.Contains("public string Global_AppName => GetValue(nameof(Global_AppName));", generated);
        Assert.Contains("Global_Menu_Title", generated);
        Assert.Contains("OnlyEnglish", generated);
        using var stream = new MemoryStream();
        var emitted = result.Output.Emit(stream);
        Assert.True(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics));
        var assembly = Assembly.Load(stream.ToArray());
        LanguageManager.CreateInstance("zh");
        var instance = (LangBase)Activator.CreateInstance(assembly.GetType("TestLang")!)!;
        var property = instance.GetType().GetProperty("Global_AppName")!;
        Assert.Equal("\u684c\u9762", property.GetValue(instance));
        LanguageManager.Instance.ChangeLang("en");
        Assert.Equal("Desktop \"app\"\nC:\\temp", property.GetValue(instance));
        instance.GetType().GetMethod("Initialize")!.Invoke(instance, null);
        Assert.Equal("Yes", instance.GetValue("OnlyEnglish"));
        LanguageManager.Instance.ChangeLang("zh");
        Assert.Equal("OnlyEnglish", instance.GetValue("OnlyEnglish"));
    }

    [Theory]
    [InlineData("{", "LANG001")]
    [InlineData("{\"Name\":null}", "LANG001")]
    [InlineData("{\"Name\":12}", "LANG001")]
    [InlineData("{\"Name\":[]}", "LANG001")]
    [InlineData("{\"Name\":\"a\",\"Name\":\"b\"}", "LANG001")]
    [InlineData("{\"A_B\":\"a\",\"A\":{\"B\":\"b\"}}", "LANG001")]
    [InlineData("{\"bad-key\":\"a\"}", "LANG003")]
    [InlineData("{\"Initialize\":\"a\"}", "LANG003")]
    [InlineData("{\"GetValue\":\"a\"}", "LANG003")]
    public void ReportsInvalidInput(string json, string diagnosticId)
    {
        var result = Run(Source, ("lang.en.json", json));
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == diagnosticId);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id == "CS8785");
    }

    [Theory]
    [InlineData("public class Outer { public partial class TestLang : I18nExtensions.LangBase { } }")]
    [InlineData("public partial class TestLang : I18nExtensions.LangBase { protected override void Initialized() {} }")]
    [InlineData("public partial class TestLang : I18nExtensions.LangBase { public void Initialize() {} }")]
    public void ReportsInvalidClass(string source)
    {
        var result = Run(source, ("lang.en.json", "{\"Name\":\"a\"}"));
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "LANG002");
    }

    [Fact]
    public void GeneratesNestedGenericAndIndirectDerivedClasses()
    {
        var result = Run("""
            namespace Example;
            public abstract class Intermediate : I18nExtensions.LangBase { }
            public partial class Outer<T> where T : class
            {
                public partial class Inner<U> : Intermediate where U : new() { }
            }
            """, ("lang.en.json", "{\"class\":\"keyword\"}"));
        // The non-partial intermediate class is not a generation target.
        AssertValid(result);
        Assert.Contains("@class", result.Driver.GetRunResult().Results.Single().GeneratedSources.Single().SourceText.ToString());
    }

    [Fact]
    public void GeneratesOnceForMultipleDeclarationsAndSeparatesNamespaces()
    {
        var result = Run("""
            namespace A {
                public partial class Text : I18nExtensions.LangBase { }
                public partial class Text : I18nExtensions.LangBase { }
            }
            namespace B { public partial class Text : I18nExtensions.LangBase { } }
            """, ("lang.en.json", "{\"Name\":\"a\"}"));
        AssertValid(result);
        Assert.Equal(new[] { "A.Text.g.cs", "B.Text.g.cs" },
            result.Driver.GetRunResult().Results.Single().GeneratedSources.Select(source => source.HintName).Order());
    }

    [Fact]
    public void IgnoresUnrelatedFilesAndClasses()
    {
        Assert.Empty(Run(Source, ("settings.json", "{}")).Driver.GetRunResult().GeneratedTrees);
        Assert.Empty(Run(Source, ("lang.json", "{}")).Driver.GetRunResult().GeneratedTrees);
        Assert.Empty(Run("public partial class Other {}", ("lang.en.json", "{}")).Driver.GetRunResult().GeneratedTrees);
    }

    [Fact]
    public void ReportsDuplicateOrEmptyCultures()
    {
        Assert.Contains(Run(Source, ("lang..json", "{}")).Diagnostics, diagnostic => diagnostic.Id == "LANG001");
        Assert.Contains(Run(Source, ("a/lang.en.json", "{}"), ("b/lang.en.json", "{}")).Diagnostics,
            diagnostic => diagnostic.Id == "LANG001");
    }

    [Fact]
    public void UpdatesPropertiesWhenAdditionalFileChanges()
    {
        var result = Run(Source, ("lang.en.json", "{\"Old\":\"a\"}"));
        var driver = result.Driver.ReplaceAdditionalTexts(ImmutableArray.Create<AdditionalText>(
            new TextFile("lang.en.json", "{\"New\":\"b\"}")));
        driver = driver.RunGeneratorsAndUpdateCompilation(result.Input, out var output, out var diagnostics);
        Assert.Empty(diagnostics);
        Assert.DoesNotContain(output.GetDiagnostics(), diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        var generated = driver.GetRunResult().GeneratedTrees.Single().ToString();
        Assert.Contains("public string New", generated);
        Assert.DoesNotContain("public string Old", generated);
    }

    private static void AssertValid(Result result)
    {
        Assert.Empty(result.Diagnostics);
        Assert.DoesNotContain(result.Output.GetDiagnostics(), diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    private static Result Run(string source, params (string Path, string Json)[] files)
    {
        var compilation = CSharpCompilation.Create("Generated_" + Guid.NewGuid().ToString("N"),
            new[] { CSharpSyntaxTree.ParseText(source, ParseOptions) }, References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new[] { new LangGenerator().AsSourceGenerator() },
            files.Select(file => new TextFile(file.Path, file.Json)), ParseOptions);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
        return new Result(driver, compilation, output, diagnostics);
    }

    private sealed record Result(GeneratorDriver Driver, Compilation Input, Compilation Output,
        ImmutableArray<Diagnostic> Diagnostics);
    private sealed class TextFile(string path, string content) : AdditionalText
    {
        public override string Path => path;
        public override SourceText GetText(CancellationToken cancellationToken = default) => SourceText.From(content);
    }
}
