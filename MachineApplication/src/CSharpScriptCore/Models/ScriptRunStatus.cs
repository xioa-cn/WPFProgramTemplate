namespace CSharpScriptCore.Models
{
    /// <summary>
    /// CSX脚本运行结果状态
    /// </summary>
    public enum ScriptRunStatus
    {
        /// <summary>
        /// 初始未执行 / 等待调度
        /// </summary>
        Idle = 0,

        /// <summary>
        /// 正在加载脚本源码文件
        /// </summary>
        LoadingSource = 1,

        /// <summary>
        /// 编译中（Roslyn动态编译）
        /// </summary>
        Compiling = 2,

        /// <summary>
        /// 编译成功，准备开始运行
        /// </summary>
        ReadyToRun = 3,

        /// <summary>
        /// 脚本正在执行中
        /// </summary>
        Running = 4,

        /// <summary>
        /// 正常执行完成（无异常，脚本跑完所有代码）
        /// </summary>
        Success = 5,

        /// <summary>
        /// 业务逻辑警告，执行完成但是存在提示，非报错
        /// </summary>
        Warning = 6,

        /// <summary>
        /// 编译失败：语法错误、缺少引用、#load找不到文件
        /// </summary>
        CompileFailed = 10,

        /// <summary>
        /// 运行时异常：空引用、数据库报错、PLC通讯失败等脚本内部抛出异常
        /// </summary>
        RuntimeException = 11,

        /// <summary>
        /// 执行超时（你开CancellationToken取消脚本）
        /// </summary>
        Timeout = 12,

        /// <summary>
        /// 被外部手动取消（用户点击停止按钮）
        /// </summary>
        Cancelled = 13,

        /// <summary>
        /// 文件错误：csx文件不存在、权限不足、读取失败
        /// </summary>
        FileLoadError = 14,

        /// <summary>
        /// #load 依赖脚本加载失败
        /// </summary>
        DependencyLoadFailed = 15,

        /// <summary>
        /// 宿主内部错误（Roslyn引擎异常、内存问题，非脚本代码问题）
        /// </summary>
        HostInternalError = 20
    }
}