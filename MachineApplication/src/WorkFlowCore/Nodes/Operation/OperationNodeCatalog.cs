using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.Operation;

public static class OperationNodeCatalog
{
    public static IReadOnlyList<Type> NodeTypes { get; } = Array.AsReadOnly<Type>(
    [
        typeof(AndNode), typeof(OrNode), typeof(NotNode), typeof(EqualToNode), typeof(NotEqualToNode), typeof(SizeComparisonNode),
        typeof(IfNode), typeof(SwitchNode),
        typeof(ArrayCreateNode), typeof(ArrayAddNode), typeof(ArrayClearNode), typeof(ArrayContainsNode), typeof(ArrayCountNode),
        typeof(ArrayGetNode), typeof(ArrayIndexOfNode), typeof(ArrayRemoveAtNode), typeof(ArrayReverseNode), typeof(ArraySetNode), typeof(ArraySliceNode),
        typeof(DicCreateNode), typeof(DicSetNode), typeof(DicGetNode), typeof(DicRemoveNode), typeof(DicClearNode),
        typeof(DicContainsKeyNode), typeof(DicContainsValueNode), typeof(DicGetKeysNode), typeof(DicGetValuesNode),
        typeof(IsNullNode), typeof(IsNotNullNode), typeof(GetTypeNode), typeof(TypeConverterNode),
        typeof(GetVarialbeNode), typeof(SetVarialbeNode)
    ]);

    public static void Register(XTNodeEditorPannel panel)
    {
        ArgumentNullException.ThrowIfNull(panel);
        foreach (var type in NodeTypes) panel.AddXTNode(type);
    }
}
