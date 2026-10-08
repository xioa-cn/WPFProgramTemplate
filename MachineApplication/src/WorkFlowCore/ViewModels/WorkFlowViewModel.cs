namespace WorkFlowCore.ViewModels;

using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;

/// <summary>保存当前流程文档的显示状态；画布和文件对话框由页面管理。</summary>
public class WorkFlowViewModel : ObservableObject
{
    private string? _filePath;
    private bool _isDirty;
    private string _statusMessage = "从左侧拖入节点，或点击“添加开始节点”。";

    public string? FilePath => _filePath;
    public bool IsDirty => _isDirty;
    public string DocumentTitle => $"{(_filePath is null ? "未命名流程" : Path.GetFileName(_filePath))}{(_isDirty ? " *" : "")}";
    public string FileDescription => _filePath ?? "尚未保存到文件";

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    /// <summary>在新建、加载或保存成功后同步文件路径，并清除修改标记。</summary>
    public void AcceptDocument(string? filePath, string message)
    {
        SetProperty(ref _filePath, filePath, nameof(FilePath));
        SetProperty(ref _isDirty, false, nameof(IsDirty));
        OnPropertyChanged(nameof(DocumentTitle));
        OnPropertyChanged(nameof(FileDescription));
        StatusMessage = message;
    }

    /// <summary>标记画布内容或视口已改变，提醒用户保存当前文档。</summary>
    public void MarkModified()
    {
        if (SetProperty(ref _isDirty, true, nameof(IsDirty)))
            OnPropertyChanged(nameof(DocumentTitle));
    }
}
