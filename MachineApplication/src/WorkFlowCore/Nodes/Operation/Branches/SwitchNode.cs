using System.ComponentModel;
using System.Text.Json;
using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.Operation;

[DisplayName("多路分支")]
[XTNode("流程控制", "xioa", "", "", "按 JSON 分支值匹配，仅激活一个出口；未匹配走默认分支。修改分支列表前请断开出口。")]
public sealed class SwitchNode : UnaryValueNode
{
    private string _casesJson = "[0,1,2]";

    public SwitchNode() : base("多路分支", typeof(object))
    {
        Output.Text = "默认";
        RebuildCases(ParseCases(_casesJson));
    }

    [XTNodeProperty("分支值（JSON 数组）", "最多 32 个唯一标量值，例如 [0,1,\"ok\",null]，按排列顺序对应出口。")]
    public string CasesJson
    {
        get => _casesJson;
        set
        {
            VerifyAccess();
            if (_casesJson == value) return;
            var cases = ParseCases(value);
            if (GetOutputOptions().Any(port => port.ConnectionCount > 0))
                throw new InvalidOperationException("请先断开分支出口再修改分支值，避免连线指向错误分支。");
            _casesJson = value;
            RebuildCases(cases);
        }
    }

    public XTNodeOption DefaultOutput => Output;
    public IReadOnlyList<XTNodeOption> CaseOutputs => GetOutputOptions().Skip(1).ToArray();

    private static object?[] ParseCases(string json)
    {
        var cases = OperationValues.Array(OperationValues.Parse(json));
        if (cases.Length > 32) throw new ArgumentException("最多支持 32 个分支。");
        for (var index = 0; index < cases.Length; index++)
        {
            var value = cases[index];
            if (value is not null and not string and not bool && !OperationValues.IsNumber(value))
                throw new ArgumentException("分支值只能是数字、文本、Boolean 或 null。");
            if (cases.Take(index).Any(previous => OperationValues.Equal(previous, value)))
                throw new ArgumentException("分支值不能重复。");
        }
        return cases;
    }

    private void RebuildCases(object?[] cases)
    {
        while (OutputOptions.Count > 1) OutputOptions.RemoveAt(OutputOptions.Count - 1);
        foreach (var value in cases)
        {
            var label = JsonSerializer.Serialize(value);
            var port = OutputOptions.Add(label.Length > 28 ? label[..28] + "…" : label, typeof(object), false);
            port.Description = label;
        }
    }

    protected override object? Evaluate(object?[] values, EditorExecutionContext context, bool preview) =>
        Array.FindIndex(ParseCases(CasesJson), candidate => OperationValues.Equal(candidate, values[0]));

    protected override XTNodeOption[] Publish(object? value, EditorExecutionContext context)
    {
        var index = (int)value!;
        var selected = index < 0 ? DefaultOutput : CaseOutputs[index];
        selected.TransferData(new EditorFlowSignal(context.ExecutionId));
        return [selected];
    }
}
