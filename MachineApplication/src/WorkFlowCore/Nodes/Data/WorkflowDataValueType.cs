namespace WorkFlowCore.Nodes.Data;

/// <summary>工作流数据节点支持的基础值类型，类型选择会同步到节点端口。</summary>
public enum WorkflowDataValueType
{
    String,
    Int32,
    Int64,
    Double,
    Decimal,
    Boolean,
    DateTime,
    Object
}
