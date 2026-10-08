using System.Windows.Controls;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Machine.ModuleLoad.Region;
using Microsoft.Win32;
using ST.Library.UI.NodeEditor;
using WorkFlowCore.Nodes;
using WorkFlowCore.Nodes.Data;
using WorkFlowCore.Nodes.Flow;
using WorkFlowCore.Nodes.Math;
using WorkFlowCore.Nodes.Operation;
using WorkFlowCore.Nodes.Script;
using WorkFlowCore.Nodes.Str;
using WorkFlowCore.ViewModels;
using WorkFlowCore.Services;

namespace WorkFlowCore.Views;

/// <summary>承载节点编辑器，并处理依赖 WPF 视图的文件操作、焦点提交和离开确认。</summary>
public partial class WorkFlowPage : Page, IConfirmNavigationRequest
{
    private const string CanvasFileFilter = "工作流文件 (*.workflow.json)|*.workflow.json|JSON 文件 (*.json)|*.json";
    private readonly WorkFlowViewModel _viewModel;
    private bool _isReplacingDocument;
    private Window? _ownerWindow;

    public static readonly RoutedUICommand AddStartNodeCommand = new("添加开始节点", nameof(AddStartNodeCommand), typeof(WorkFlowPage));

    /// <summary>初始化页面并登记开始节点类型，使目录添加和文件反序列化使用相同白名单。</summary>
    public WorkFlowPage(WorkFlowViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
        EditorPanel.AddXTNode(typeof(StartNode));
        EditorPanel.AddXTNode(typeof(DelayNode));
        EditorPanel.AddXTNode(typeof(EmptyBeatNode));
        EditorPanel.AddXTNode(typeof(ScriptClassNode));
        EditorPanel.AddXTNode(typeof(ScriptMethodNode));
        EditorPanel.AddXTNode(typeof(ConstDataNode));
        EditorPanel.AddXTNode(typeof(GlobalDataNode));
        EditorPanel.AddXTNode(typeof(AddNode));
        EditorPanel.AddXTNode(typeof(SubNode));
        EditorPanel.AddXTNode(typeof(MulNode));
        EditorPanel.AddXTNode(typeof(DivNode));
        EditorPanel.AddXTNode(typeof(ModNode));
        EditorPanel.AddXTNode(typeof(AbsNode));
        EditorPanel.AddXTNode(typeof(ClampNode));
        EditorPanel.AddXTNode(typeof(RandomNode));
        EditorPanel.AddXTNode(typeof(RoundNode));
        EditorPanel.AddXTNode(typeof(SignNode));
        EditorPanel.AddXTNode(typeof(FloorNode));
        EditorPanel.AddXTNode(typeof(CeilingNode));
        EditorPanel.AddXTNode(typeof(SqrtNode));
        EditorPanel.AddXTNode(typeof(SinNode));
        EditorPanel.AddXTNode(typeof(CosNode));
        EditorPanel.AddXTNode(typeof(TanNode));
        EditorPanel.AddXTNode(typeof(StrLenNode));
        EditorPanel.AddXTNode(typeof(StrConcatNode));
        EditorPanel.AddXTNode(typeof(StrJoinNode));
        EditorPanel.AddXTNode(typeof(StrSplitNode));
        EditorPanel.AddXTNode(typeof(StrSubstringNode));
        EditorPanel.AddXTNode(typeof(StrReplaceNode));
        EditorPanel.AddXTNode(typeof(StrTrimNode));
        EditorPanel.AddXTNode(typeof(StrToUpperLowerNode));
        EditorPanel.AddXTNode(typeof(StrContainsNode));
        EditorPanel.AddXTNode(typeof(StrStartsWithNode));
        EditorPanel.AddXTNode(typeof(StrEndsWithNode));
        EditorPanel.AddXTNode(typeof(StrIndexOfNode));
        EditorPanel.AddXTNode(typeof(StrLastIndexOfNode));
        EditorPanel.AddXTNode(typeof(StrCompareNode));
        EditorPanel.AddXTNode(typeof(StrIsEmptyNode));
        EditorPanel.AddXTNode(typeof(StrIsWhiteSpaceNode));
        EditorPanel.AddXTNode(typeof(StrPadLeftNode));
        EditorPanel.AddXTNode(typeof(StrPadRightNode));
        OperationNodeCatalog.Register(EditorPanel);
        EditorPanel.Editor.NodeAdded += OnNodeAdded;
        EditorPanel.Editor.NodeRemoved += OnNodeRemoved;
        EditorPanel.Editor.OptionConnected += OnConnectionChanged;
        EditorPanel.Editor.OptionDisConnected += OnConnectionChanged;
        EditorPanel.Editor.CanvasMoved += OnDocumentChanged;
        EditorPanel.Editor.CanvasScaled += OnDocumentChanged;
        EditorPanel.Editor.PortOrientationChanged += OnDocumentChanged;
        EditorPanel.PropertyGrid.PropertyValueChanged += OnDocumentChanged;
        Loaded += OnPageLoaded;
        Unloaded += OnPageUnloaded;
        CreateDocument();
        if (File.Exists(WorkflowPackageService.DefaultFilePath))
            LoadDocument(WorkflowPackageService.DefaultFilePath);
    }

    /// <summary>确认未保存修改后，新建含一个开始节点的流程。</summary>
    private void OnNewExecuted(object sender, ExecutedRoutedEventArgs args)
    {
        args.Handled = true;
        if (ConfirmUnsavedChanges()) CreateDocument();
    }

    /// <summary>选择并加载流程文件；只有加载成功才替换当前文件路径和修改状态。</summary>
    private void OnOpenExecuted(object sender, ExecutedRoutedEventArgs args)
    {
        args.Handled = true;
        var dialog = new OpenFileDialog { Title = "打开工作流", Filter = CanvasFileFilter, CheckFileExists = true,
            InitialDirectory = Directory.Exists(WorkflowPackageService.DefaultDirectory) ? WorkflowPackageService.DefaultDirectory : AppContext.BaseDirectory };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true || !ConfirmUnsavedChanges()) return;
        LoadDocument(dialog.FileName);
    }

    private void LoadDocument(string filePath)
    {
        try
        {
            _isReplacingDocument = true;
            // 底层先在临时画布校验节点及连接，损坏文件不会提前清空当前画布。
            WorkflowPackageService.Load(EditorPanel.Editor, filePath);
            EditorPanel.PropertyGrid.SetNode(null);
            _viewModel.AcceptDocument(filePath, $"已加载流程及脚本依赖，共 {EditorPanel.Editor.Nodes.Count} 个节点。");
        }
        catch (Exception exception)
        {
            ShowFileError("打开", exception);
        }
        finally
        {
            _isReplacingDocument = false;
        }
    }

    /// <summary>保存完整工作流；首次保存默认写入应用目录下的 workflow。</summary>
    private void OnSaveExecuted(object sender, ExecutedRoutedEventArgs args)
    {
        args.Handled = true;
        SaveDocument(false);
    }

    /// <summary>将当前画布另存到用户选择的新文件。</summary>
    private void OnSaveAsExecuted(object sender, ExecutedRoutedEventArgs args)
    {
        args.Handled = true;
        SaveDocument(true);
    }

    /// <summary>提交属性编辑并保存画布；取消或保存失败时保留原路径和未保存标记。</summary>
    private bool SaveDocument(bool saveAs)
    {
        // 快捷键可能来自属性文本框，先移出焦点以触发属性面板提交输入。
        EditorPanel.Editor.Focus();
        try
        {
            var filePath = _viewModel.FilePath;
            if (saveAs)
            {
                var dialog = new SaveFileDialog
                {
                    Title = "保存工作流",
                    Filter = CanvasFileFilter,
                    DefaultExt = ".workflow.json",
                    AddExtension = true,
                    OverwritePrompt = true,
                    FileName = filePath is null ? "未命名流程.workflow.json" : Path.GetFileName(filePath),
                    InitialDirectory = filePath is null ? AppContext.BaseDirectory : Path.GetDirectoryName(filePath) ?? ""
                };
                if (dialog.ShowDialog(Window.GetWindow(this)) != true) return false;
                filePath = dialog.FileName;
            }

            // 复用编辑器的临时文件替换机制，成功写盘后才清除脏标记。
            filePath ??= WorkflowPackageService.CreateDefaultFilePath();
            WorkflowPackageService.Save(EditorPanel.Editor, filePath);
            _viewModel.AcceptDocument(filePath, "流程、脚本及依赖已保存。");
            return true;
        }
        catch (Exception exception)
        {
            ShowFileError("保存", exception);
            return false;
        }
    }

    /// <summary>清空原流程，恢复上下端口布局、复位视口并放置开始节点；批量修改期间不产生脏标记。</summary>
    private void CreateDocument()
    {
        _isReplacingDocument = true;
        try
        {
            var editor = EditorPanel.Editor;
            editor.Nodes.Clear();
            editor.SetCurrentValue(XTNodeEditor.VerticalPortsProperty, true);
            editor.ScaleCanvas(1, 0, 0);
            editor.MoveCanvas(0, 0, false, CanvasMoveArgs.All);
            var node = new StartNode { Location = new Point(80, 80) };
            editor.Nodes.Add(node);
            editor.SetActiveNode(node);
            node.IsSelected = true;
            _viewModel.AcceptDocument(null, "已创建流程，可选中开始节点编辑属性。当前仅支持编辑与保存，尚未接入执行引擎。");
        }
        finally
        {
            _isReplacingDocument = false;
        }
    }

    /// <summary>在可见画布中心添加开始节点，并切换属性面板到该节点。</summary>
    private void OnAddStartNodeExecuted(object sender, ExecutedRoutedEventArgs args)
    {
        args.Handled = true;
        var editor = EditorPanel.Editor;
        editor.Focus();
        foreach (var selected in editor.GetSelectedNode()) selected.IsSelected = false;
        var node = new StartNode
        {
            Location = editor.ControlToCanvas(new Point(editor.ActualWidth / 2, editor.ActualHeight / 2))
        };
        editor.Nodes.Add(node);
        editor.SetActiveNode(node);
        node.IsSelected = true;
        _viewModel.StatusMessage = "已添加开始节点。";
    }

    /// <summary>订阅新增节点的位置及属性变化，覆盖拖入、双击和按钮添加场景。</summary>
    private void OnNodeAdded(object sender, XTNodeEditorEventArgs args)
    {
        args.Node.PropertyChanged += OnNodePropertyChanged;
        OnDocumentChanged(sender, args);
    }

    /// <summary>删除节点时解除订阅，避免旧节点继续影响当前文档状态。</summary>
    private void OnNodeRemoved(object sender, XTNodeEditorEventArgs args)
    {
        args.Node.PropertyChanged -= OnNodePropertyChanged;
        OnDocumentChanged(sender, args);
    }

    /// <summary>只记录持久化属性的变化，选择和运行高亮不算文档修改。</summary>
    private void OnNodePropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(XTNode.Location) or nameof(XTNode.Title) or nameof(XTNode.Mark))
            OnDocumentChanged(sender, args);
    }

    /// <summary>仅在连接成功建立或断开时标记修改，失败的连接操作不污染状态。</summary>
    private void OnConnectionChanged(object sender, XTNodeEditorOptionEventArgs args)
    {
        if (args.Status is ConnectionStatus.Connected or ConnectionStatus.DisConnected)
            OnDocumentChanged(sender, args);
    }

    /// <summary>统一记录节点、属性、连线及视口变化，忽略新建和加载期间的内部事件。</summary>
    private void OnDocumentChanged(object? sender, EventArgs args)
    {
        if (!_isReplacingDocument) _viewModel.MarkModified();
    }

    /// <summary>替换文档或离开页面前提供保存、放弃和取消三种选择。</summary>
    private bool ConfirmUnsavedChanges()
    {
        EditorPanel.Editor.Focus();
        if (!_viewModel.IsDirty) return true;
        var result = MessageBox.Show(Window.GetWindow(this), "当前流程有未保存的修改，是否先保存？", "工作流",
            MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        return result == MessageBoxResult.No || result == MessageBoxResult.Yes && SaveDocument(false);
    }

    /// <summary>显示文件操作错误，不将失败操作当作成功保存或加载。</summary>
    private void ShowFileError(string operation, Exception exception)
    {
        _viewModel.StatusMessage = $"{operation}失败：{exception.Message}";
        MessageBox.Show(Window.GetWindow(this), _viewModel.StatusMessage, "工作流", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    /// <summary>页面显示时订阅宿主关闭事件，避免直接关闭窗口丢失编辑内容。</summary>
    private void OnPageLoaded(object sender, RoutedEventArgs args)
    {
        if (_ownerWindow is not null) _ownerWindow.Closing -= OnOwnerWindowClosing;
        _ownerWindow = Window.GetWindow(this);
        if (_ownerWindow is not null) _ownerWindow.Closing += OnOwnerWindowClosing;
    }

    /// <summary>页面卸载时解除宿主订阅，浮动或重新挂载后由加载事件重新绑定。</summary>
    private void OnPageUnloaded(object sender, RoutedEventArgs args)
    {
        if (_ownerWindow is not null) _ownerWindow.Closing -= OnOwnerWindowClosing;
        _ownerWindow = null;
    }

    /// <summary>关闭宿主窗口时允许用户保存修改或取消关闭。</summary>
    private void OnOwnerWindowClosing(object? sender, CancelEventArgs args)
    {
        if (!args.Cancel && !ConfirmUnsavedChanges()) args.Cancel = true;
    }

    /// <summary>导航返回时复用当前编辑器，保留画布和文件状态。</summary>
    public bool IsNavigationTarget(RegionNavigationContext context) => true;

    /// <summary>页面恢复导航时不重新初始化文档，避免覆盖已编辑内容。</summary>
    public void OnNavigatedTo(RegionNavigationContext context)
    {
    }

    /// <summary>通过区域导航协议确认未保存修改，取消保存时阻止离开。</summary>
    public void ConfirmNavigationRequest(RegionNavigationContext navigationContext, Action<bool> continuationCallback)
    {
        continuationCallback(ConfirmUnsavedChanges());
    }
}
