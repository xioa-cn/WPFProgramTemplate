using System.Text;
using System.Windows.Media;

namespace ST.Library.UI.NodeEditor;

/// <summary>动态中转节点，首次连接确定行的数据类型，并保留一行空端口供扩展。</summary>
public class XTNodeHub : XTNode
{
    private bool _single;
    private string _inputText = "IN", _outputText = "OUT";
    private bool _updating;
    public XTNodeHub() : this(false, "IN", "OUT") { }
    public XTNodeHub(bool single) : this(single, "IN", "OUT") { }
    public XTNodeHub(bool single, string inputText, string outputText)
    {
        _single = single; _inputText = inputText; _outputText = outputText;
        Title = "HUB";
        TitleColor = Colors.DarkOrange;
        AddRow();
    }
    private void AddRow()
    {
        var input = new XTNodeHubOption(_inputText, typeof(object), _single);
        var output = new XTNodeHubOption(_outputText, typeof(object), false);
        InputOptions.Add(input); OutputOptions.Add(output);
        input.Connected += (_, args) => Connected(input, output, args);
        output.Connected += (_, args) => Connected(input, output, args);
        input.DisConnected += (_, _) => RemoveUnused(input, output);
        output.DisConnected += (_, _) => RemoveUnused(input, output);
        input.DataTransfer += (_, args) =>
        {
            if (!OutputOptions.Contains(output)) return;
            output.TransferData(args.Status == ConnectionStatus.Connected ? args.TargetOption.Data : null);
        };
    }
    private void Connected(XTNodeOption input, XTNodeOption output, XTNodeOptionEventArgs args)
    {
        if (input.DataType == typeof(object)) input.DataType = output.DataType = args.TargetOption.DataType;
        if (!_updating && !InputOptions.Any(option => option.ConnectionCount == 0 && OutputOptions[InputOptions.IndexOf(option)].ConnectionCount == 0)) AddRow();
    }
    private void RemoveUnused(XTNodeOption input, XTNodeOption output)
    {
        if (_updating || Owner is null || input.ConnectionCount != 0 || output.ConnectionCount != 0 || !InputOptions.Contains(input)) return;
        _updating = true;
        try
        {
            InputOptions.Remove(input); OutputOptions.Remove(output);
            if (InputOptions.Count == 0) AddRow();
        }
        finally { _updating = false; }
    }
    protected override void OnSaveNode(Dictionary<string, byte[]> values)
    {
        values["count"] = BitConverter.GetBytes(InputOptions.Count);
        values["single"] = [_single ? (byte)1 : (byte)0];
        values["inputText"] = Encoding.UTF8.GetBytes(_inputText);
        values["outputText"] = Encoding.UTF8.GetBytes(_outputText);
    }
    protected internal override void OnLoadNode(Dictionary<string, byte[]> values)
    {
        base.OnLoadNode(values);
        var count = values.TryGetValue("count", out var bytes) && bytes.Length == 4 ? BitConverter.ToInt32(bytes) : 1;
        if (count is < 1 or > 10000) throw new System.IO.InvalidDataException("HUB 端口数量无效。");
        _single = values.TryGetValue("single", out var single) && single.Length == 1 && single[0] == 1;
        if (values.TryGetValue("inputText", out var input)) _inputText = Encoding.UTF8.GetString(input);
        if (values.TryGetValue("outputText", out var output)) _outputText = Encoding.UTF8.GetString(output);
        _updating = true;
        try
        {
            InputOptions.Clear(); OutputOptions.Clear();
            for (var index = 0; index < count; index++) AddRow();
        }
        finally { _updating = false; }
    }
    public class XTNodeHubOption(string text, Type type, bool single) : XTNodeOption(text, type, single)
    {
        internal override Type? ConnectionType(Type? otherType) => DataType == typeof(object) ? otherType : DataType;
    }
}
