using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using CsxPad.Wpf.ViewModels;
using ICSharpCode.AvalonEdit.Editing;

namespace CsxPad.Wpf.Controls;

public partial class EditorWorkspace : UserControl
{
    private BreakpointMargin? _breakpointMargin;
    private LineNumberMargin? _lineNumberMargin;
    private MainWindowViewModel? _viewModel;
    private bool _isTrackingViewport;

    public EditorWorkspace()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        DataContextChanged += (_, _) => AttachViewModel(DataContext as MainWindowViewModel);
        CodeEditor.PreviewKeyDown += OnEditorPreviewKeyDown;
    }

    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_breakpointMargin is null)
        {
            _breakpointMargin = new BreakpointMargin(
                CodeEditor,
                () => _viewModel?.Breakpoints ?? [],
                () => _viewModel?.CurrentDebugLine,
                line => _viewModel?.ToggleBreakpointCommand.Execute(line));
            CodeEditor.TextArea.LeftMargins.Insert(0, _breakpointMargin);
            CodeEditor.TextArea.TextView.BackgroundRenderers.Add(_breakpointMargin);
            _lineNumberMargin = CodeEditor.TextArea.LeftMargins.OfType<LineNumberMargin>().FirstOrDefault();
            if (_lineNumberMargin is not null)
            {
                _lineNumberMargin.Cursor = Cursors.Hand;
                _lineNumberMargin.ToolTip = "Toggle breakpoint (F9)";
                _lineNumberMargin.PreviewMouseLeftButtonDown += OnLineNumberMouseLeftButtonDown;
            }
        }

        AttachViewportTracking();
        AttachViewModel(DataContext as MainWindowViewModel);
        CodeEditor.Focus();
    }

    private void OnUnloaded(object sender, System.Windows.RoutedEventArgs e)
    {
        DetachViewportTracking();
        AttachViewModel(null);
        if (_lineNumberMargin is not null)
        {
            _lineNumberMargin.PreviewMouseLeftButtonDown -= OnLineNumberMouseLeftButtonDown;
            _lineNumberMargin = null;
        }
    }

    private void OnLineNumberMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel is null || _breakpointMargin is null || sender is not LineNumberMargin margin)
        {
            return;
        }

        var line = _breakpointMargin.GetLineFromViewportY(e.GetPosition(margin).Y);
        if (!line.HasValue)
        {
            return;
        }

        _viewModel.ToggleBreakpointCommand.Execute(line.Value);
        e.Handled = true;
    }

    private void AttachViewModel(MainWindowViewModel? viewModel)
    {
        if (ReferenceEquals(_viewModel, viewModel))
        {
            return;
        }

        if (_viewModel is not null)
        {
            _viewModel.Breakpoints.CollectionChanged -= OnBreakpointsChanged;
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = viewModel;
        if (_viewModel is not null)
        {
            _viewModel.Breakpoints.CollectionChanged += OnBreakpointsChanged;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        _breakpointMargin?.Refresh();
    }

    private void OnBreakpointsChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        _breakpointMargin?.Refresh();

    private void AttachViewportTracking()
    {
        if (_isTrackingViewport)
        {
            return;
        }

        var textView = CodeEditor.TextArea.TextView;
        textView.ScrollOffsetChanged += OnEditorViewportChanged;
        textView.VisualLinesChanged += OnEditorViewportChanged;
        _isTrackingViewport = true;
    }

    private void DetachViewportTracking()
    {
        if (!_isTrackingViewport)
        {
            return;
        }

        var textView = CodeEditor.TextArea.TextView;
        textView.ScrollOffsetChanged -= OnEditorViewportChanged;
        textView.VisualLinesChanged -= OnEditorViewportChanged;
        _isTrackingViewport = false;
    }

    private void OnEditorViewportChanged(object? sender, EventArgs e) =>
        _breakpointMargin?.RefreshViewport();

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainWindowViewModel.CurrentDebugLine))
        {
            return;
        }

        _breakpointMargin?.Refresh();
        if (_viewModel?.CurrentDebugLine is { } line)
        {
            CodeEditor.ScrollToLine(line);
        }
    }

    private void OnEditorPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.F9 || _viewModel is null)
        {
            return;
        }

        _viewModel.ToggleBreakpointCommand.Execute(CodeEditor.TextArea.Caret.Line);
        e.Handled = true;
    }

    private void ResultGrid_AutoGeneratingColumn(object? sender, DataGridAutoGeneratingColumnEventArgs e)
    {
        if (e.Column is not DataGridTextColumn column)
        {
            return;
        }

        column.MinWidth = e.PropertyName == "Member" ? 72 : 56;
        column.MaxWidth = 520;
        column.Width = new DataGridLength(1, DataGridLengthUnitType.Auto);
        column.ElementStyle = (Style)FindResource(
            e.PropertyName == "Member"
                ? "IdeResultMemberTextStyle"
                : "IdeResultTextStyle");
        if (e.PropertyName == "Member")
        {
            column.CellStyle = (Style)FindResource("IdeResultMemberCellStyle");
        }
    }

    private void BottomPaneSplitter_DragDelta(object sender, DragDeltaEventArgs e)
    {
        var maximum = Math.Max(BottomPaneHost.MinHeight, ActualHeight - 220);
        BottomPaneHost.Height = Math.Clamp(
            BottomPaneHost.ActualHeight - e.VerticalChange,
            BottomPaneHost.MinHeight,
            maximum);
    }
}
