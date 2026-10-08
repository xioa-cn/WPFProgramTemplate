using System.Collections.ObjectModel;

namespace ST.Library.UI.NodeEditor;

/// <summary>节点集合与 WPF 视觉树保持同步，删除节点时一并释放端口关系。</summary>
public sealed class XTNodeCollection(XTNodeEditor owner) : Collection<XTNode>
{
    public void AddRange(XTNode[] nodes)
    {
        foreach (var node in nodes) Add(node);
    }

    protected override void InsertItem(int index, XTNode item)
    {
        owner.VerifyAccess();
        ArgumentNullException.ThrowIfNull(item);
        if (item.Owner is not null || item.Parent is not null) throw new InvalidOperationException("节点已加入其他容器。");
        base.InsertItem(index, item);
        owner.AttachNode(item);
    }

    protected override void RemoveItem(int index)
    {
        owner.VerifyAccess();
        var node = this[index];
        base.RemoveItem(index);
        owner.DetachNode(node);
    }

    protected override void ClearItems()
    {
        while (Count > 0) RemoveAt(Count - 1);
    }

    protected override void SetItem(int index, XTNode item) => throw new InvalidOperationException("请先移除旧节点再添加新节点。");
}