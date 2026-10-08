using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.NodeSystem;

[DisplayName("生成 GUID")]
[XTNode("GUID 操作", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/", "生成 GUID；使用固定文化，输入无效时返回节点错误。")]
public sealed class GuidNewGuidNode : SystemNode
{
    public GuidNewGuidNode() : base("生成 GUID", typeof(Guid))
    {

    }


    protected override object? Run(object?[] values, CancellationToken cancellationToken)
    {
        return System.Guid.NewGuid();
    }
}
