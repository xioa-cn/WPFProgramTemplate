using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.Math;

public abstract class BinaryMathNode : MathNode
{
    protected BinaryMathNode(string title, double rightValue = 0) : base(title)
    {
        RightValue = rightValue;
        LeftInput = AddInput("左值", () => LeftValue);
        RightInput = AddInput("右值", () => RightValue);
    }

    [XTNodeProperty("左值", "左侧输入未连接且未赋值时使用的默认数值。")]
    public double LeftValue { get; set; }

    [XTNodeProperty("右值", "右侧输入未连接且未赋值时使用的默认数值。")]
    public double RightValue { get; set; }

    public XTNodeOption LeftInput { get; }
    public XTNodeOption RightInput { get; }
}
