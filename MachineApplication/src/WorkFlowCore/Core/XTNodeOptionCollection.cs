using System.Collections.ObjectModel;

namespace ST.Library.UI.NodeEditor;

/// <summary>端口集合负责所有权及布局；Empty 仅占行，不绑定任何节点。</summary>
public sealed class XTNodeOptionCollection(XTNode owner, bool isInput) : Collection<XTNodeOption>
{
    public XTNodeOption Add(string text, Type type, bool single)
    {
        var option = new XTNodeOption(text, type, single);
        Add(option);
        return option;
    }
    public void AddRange(XTNodeOption[] options)
    {
        foreach (var option in options) Add(option);
    }
    protected override void InsertItem(int index, XTNodeOption item)
    {
        owner.VerifyAccess();
        ArgumentNullException.ThrowIfNull(item);
        if (item != XTNodeOption.Empty)
        {
            if (item.Owner is not null) throw new InvalidOperationException("端口已经属于一个节点，不能重复添加。");
            item.Owner = owner;
            item.IsInput = isInput;
        }
        base.InsertItem(index, item);
        owner.BuildSize(true, true, true);
    }
    protected override void RemoveItem(int index)
    {
        owner.VerifyAccess();
        var item = this[index];
        base.RemoveItem(index);
        if (item != XTNodeOption.Empty) { item.Detach(); item.Owner = null; }
        owner.BuildSize(true, true, true);
    }
    protected override void SetItem(int index, XTNodeOption item) => throw new InvalidOperationException("请先移除旧端口再添加新端口。");
    protected override void ClearItems()
    {
        while (Count > 0) RemoveAt(Count - 1);
    }
}
