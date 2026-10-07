using RestSharp;

namespace Machine.ModuleLoad.Region;

public interface ILoadingBar
{
    Result<Unit, Exception> Loading(Action action);

    Task<Result<Unit, Exception>> LoadingAsync(Func<Task> func);
}
