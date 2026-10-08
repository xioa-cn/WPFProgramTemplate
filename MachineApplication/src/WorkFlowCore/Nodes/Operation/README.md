# 运算节点

所有 34 个节点通过 `OperationNodeCatalog.Register` 注册到页面目录及反序列化白名单。源码按用途分目录，保留原 `WorkFlowCore.Nodes.Operation` 命名空间和类名（包括原有 `Varialbe` 拼写），避免破坏类型标识。页面使用单层分类。

| 目录 | 页面分类 | 节点 |
| --- | --- | --- |
| Logic | 逻辑比较 | And、Or、Not、EqualTo、NotEqualTo、SizeComparison |
| Branches | 流程控制 | If、Switch |
| Arrays | 数组操作 | Create、Add、Clear、Contains、Count、Get、IndexOf、RemoveAt、Reverse、Set、Slice |
| Dictionaries | 字典操作 | Create、Set、Get、Remove、Clear、ContainsKey、ContainsValue、GetKeys、GetValues |
| Values | 类型操作 | IsNull、IsNotNull、GetType、TypeConverter |
| Variables | 变量操作 | GetVarialbe、SetVarialbe |

## 输入与集合

- 未连接的端口使用属性默认值。通用值采用 JSON，文本需带双引号，如 `"abc"`；数组 `[1,2,null]`，字典 `{"key":1}`。
- 已连接但未收到值的端口不会退回默认值；上游显式传递的 `null` 是有效数据。
- 逻辑运算严格接受 Boolean，两侧都必须就绪，不会短路跳过上游执行。比较的数字可跨基础数值类型；文本区分大小写，复杂对象按自身 Equals 语义比较。
- 数组接受一维数组及 IList，输出 object[]；增删改、反转和截取均输出浅副本，不修改上游集合。索引从 0 开始，越界报错；切片长度 -1 表示到末尾。嵌套对象不深复制。
- 字典接受字符串键的 IDictionary，输出 Dictionary<string, object?>；键使用区分大小写的序号比较。缺失键读取失败，删除忽略。所有修改输出浅副本。
- GetType 输出 System.Type，null 返回 null。转换支持项目已有基础类型，失败时不保留上次结果。

## 分支与变量

- If 只向“真”或“假”出口传递当前执行 ID 的流程信号。
- Switch 的 `CasesJson` 配置至多 32 个不重复标量分支，支持数字、字符串、Boolean、null。出口依次为默认分支及配置分支。变更分支需先断开出口；恢复 JSON 时先恢复属性再恢复连接。
- 分支只返回所选出口作为 ActiveOutputs，未选中出口不广播。节点失败或取消前清空自身输出缓存。
- 变量 Context 作用域依附传入的 EditorExecutionContext；Global 作用域与 GlobalDataStore 共享。变量名不区分大小写，显式 null 与缺失不同。读变量提供触发端口，可连接写变量的“完成”出口，保证顺序。
- 就绪检查、构造和反序列化不写变量。工作流文件只保存配置与连线，不保存变量运行值；全局值不跨进程保存。

这些节点提供现有 IEditorExecutableNode 执行接口；页面尚未接入完整执行调度器时，节点注册不等同于新增整图运行引擎。
