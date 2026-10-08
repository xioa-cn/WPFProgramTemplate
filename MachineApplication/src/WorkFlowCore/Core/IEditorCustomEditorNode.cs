using System.Windows;

namespace ST.Library.UI.NodeEditor;

public interface IEditorCustomEditorNode
{
    string EditorButtonText { get; }
    bool OpenEditor(Window? owner);
}
