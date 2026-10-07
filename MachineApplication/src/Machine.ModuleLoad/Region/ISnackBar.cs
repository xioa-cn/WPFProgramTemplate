namespace Machine.ModuleLoad.Region;

public interface ISnackBar
{
    /// <summary>将消息加入提示队列。</summary>
    /// <param name="message">非空消息文本。</param>
    /// <param name="duration">显示时长，单位为毫秒，必须大于零。</param>
    void SendMessage(string message, int duration);
}
