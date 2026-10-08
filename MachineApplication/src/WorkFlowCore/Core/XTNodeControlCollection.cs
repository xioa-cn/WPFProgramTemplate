using System.Collections.ObjectModel;

namespace ST.Library.UI.NodeEditor;

/// <summary>维护节点内部控件的 WPF 父子关系，控件输入事件由 WPF 正常路由。</summary>
public sealed class XTNodeControlCollection(XTNode owner) : Collection<XTNodeControl>
{
    public void AddRange(XTNodeControl[] controls)
    {
        foreach (var control in controls) Add(control);
    }
    protected override void InsertItem(int index, XTNodeControl item)
    {
        owner.VerifyAccess();
        if (item.Owner is not null || item.Parent is not null) throw new InvalidOperationException("控件已有所属节点。");
        base.InsertItem(index, item);
        item.Owner = owner;
        owner.Children.Add(item);
        item.UpdateLocation();
    }
    protected override void RemoveItem(int index)
    {
        var item = this[index];
        owner.Children.Remove(item);
        item.Owner = null;
        base.RemoveItem(index);
    }
    protected override void ClearItems() { while (Count > 0) RemoveAt(Count - 1); }
    protected override void SetItem(int index, XTNodeControl item) => throw new InvalidOperationException("请先移除旧控件。");
}
