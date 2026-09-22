namespace Machine.ModuleLoad;

/// <summary>登录视图与框架之间的契约，不依赖具体主页类型。</summary>
public interface ILoginWindow
{
    /// <summary>框架在首次登录时允许自动登录，切换账号时禁用。</summary>
    bool AllowAutoLogin { get; set; }

    /// <summary>认证成功后同步通知框架；切换失败的异常交回登录页显示。</summary>
    event EventHandler? LoginSucceeded;
}
