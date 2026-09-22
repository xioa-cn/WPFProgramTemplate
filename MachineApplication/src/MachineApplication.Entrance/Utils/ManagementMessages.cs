using MachineApplication.Entrance.Resources;
using MachineApplication.Entrance.ViewModels;

namespace MachineApplication.Entrance.Utils;

/// <summary>界面错误消息转换入口，将已知权限异常映射为当前语言文案。</summary>
internal static class ManagementMessages
{
    /// <summary>提取最内层异常消息，翻译已知业务错误，保留未知异常的诊断信息。</summary>
    public static string Error(Exception exception)
    {
        var message = exception.GetBaseException().Message;
        EntranceLang lang = ViewModelLocator.EntranceLang;
        // 仅转换已知业务异常，未知异常保留原始诊断文本，避免掩盖真实原因。
        return message switch
        {
            "该等级仍有账号使用，请先调整账号等级。" => lang.Management_LevelInUse,
            "请先登录。" => lang.Management_SignInRequired,
            "仅最高权限账号可以管理权限。" => lang.Management_AdminOnly,
            "请选择权限等级。" => lang.Management_ChooseLevel,
            "最高权限账号不能重命名。" => lang.Management_CannotRenameAdmin,
            "不能删除最高权限账号。" => lang.Management_CannotDeleteAdmin,
            _ when exception is UnauthorizedAccessException => lang.Management_AccessDenied,
            _ => message
        };
    }
}
