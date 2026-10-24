using System.IO;
using System.Text;
using System.Windows;
using System.Xml;
using System.Xml.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Machine.ModuleLoad.Region;
using Machine.ModuleLoad.Mvvm;
using Machine.ModuleLoad.Utils;

namespace MachineApplication.Entrance.ViewModels;

public partial class ModulesConfigViewModel : MachineViewModelBase, IConfirmNavigationRequest {
    private string _savedText = string.Empty;
    private bool _initialized;
    private bool _loaded;

    public string FilePath { get; } = Path.Combine(AppContext.BaseDirectory, "modules.xml");

    [ObservableProperty] private string _xmlText = string.Empty;
    [ObservableProperty] private string _status = "尚未加载。";
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ReloadCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private bool _isBusy;

    partial void OnXmlTextChanged(string value) {
        Status = value == _savedText ? "未修改。" : "有未保存的修改。";
    }

    public async Task InitializeAsync() {
        if (_initialized) return;
        _initialized = true;
        await ReloadAsync();
    }

    private bool CanReload() => !IsBusy;
    private bool CanSave() => !IsBusy && _loaded;

    [RelayCommand(CanExecute = nameof(CanReload))]
    private async Task ReloadAsync() {
        if (XmlText != _savedText && XMessageBox.Show("重新加载将丢弃未保存的修改，是否继续？",
                "重新加载", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;

        IsBusy = true;
        try {
            var text = await File.ReadAllTextAsync(FilePath);
            _savedText = text;
            XmlText = text;
            _loaded = true;
            Status = "已加载。";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) {
            ShowError($"加载失败：{exception.Message}");
        }
        finally {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync() {
        IsBusy = true;
        string? temporaryPath = null;
        try {
            var text = XmlText;
            using var reader = XmlReader.Create(new StringReader(text), new XmlReaderSettings {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            });
            var document = XDocument.Load(reader);
            if (document.Root?.Name != "wpfModules") {
                ShowError("保存失败：根节点必须为 <wpfModules>。");
                return;
            }

            var declaredEncoding = document.Declaration?.Encoding;
            if (!string.IsNullOrEmpty(declaredEncoding) &&
                !string.Equals(declaredEncoding, "utf-8", StringComparison.OrdinalIgnoreCase)) {
                ShowError("保存失败：请将 XML 声明中的 encoding 设置为 utf-8。");
                return;
            }

            if (await File.ReadAllTextAsync(FilePath) != _savedText) {
                ShowError("文件已被其他程序修改，请重新加载后再编辑，避免覆盖外部修改。");
                return;
            }

            temporaryPath = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            await File.WriteAllTextAsync(temporaryPath, text, new UTF8Encoding(false));
            File.Replace(temporaryPath, FilePath, FilePath + ".bak");
            temporaryPath = null;
            _savedText = text;
            Status = "保存成功，重启应用后生效。";
            Growl.SuccessGlobal(Status);
        }
        catch (XmlException exception) {
            ShowError($"XML 错误（第 {exception.LineNumber} 行，第 {exception.LinePosition} 列）：{exception.Message}");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) {
            ShowError($"保存失败：{exception.Message}");
        }
        finally {
            if (temporaryPath != null) {
                try {
                    File.Delete(temporaryPath);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) {
                    Status += $" 临时文件清理失败：{exception.Message}";
                    Growl.WarningGlobal(Status);
                }
            }

            IsBusy = false;
        }
    }

    private void ShowError(string message) {
        Status = message;
        Growl.ErrorGlobal(message);
    }

    public bool IsNavigationTarget(RegionNavigationContext context) => true;

    public void OnNavigatedTo(RegionNavigationContext context) {
    }

    public void ConfirmNavigationRequest(RegionNavigationContext navigationContext, Action<bool> continuationCallback) {
        ArgumentNullException.ThrowIfNull(continuationCallback);
        if (IsBusy) {
            Growl.WarningGlobal("正在读取或保存模块配置，请稍后再离开。");
            continuationCallback(false);
            return;
        }

        if (XmlText == _savedText) {
            continuationCallback(true);
            return;
        }

        var discard = XMessageBox.Show("modules.xml 有未保存的修改，是否放弃修改并离开？\n选择“否”返回编辑器继续编辑或保存。",
            "未保存的修改", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (discard == MessageBoxResult.Yes) {
            XmlText = _savedText;
        }

        continuationCallback(discard == MessageBoxResult.Yes);
    }
}
