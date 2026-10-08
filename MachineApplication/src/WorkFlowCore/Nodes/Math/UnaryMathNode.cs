using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.Math;

public abstract class UnaryMathNode : MathNode
{
    protected UnaryMathNode(string title, string inputName = "数值") : base(title)
    {
        Input = AddInput(inputName, () => Value);
    }

    [XTNodeProperty("数值", "输入未连接且未赋值时使用的默认数值；三角函数使用弧度。")]
    public double Value { get; set; }

    public XTNodeOption Input { get; }
}
