namespace Machine.ModuleLoad.Region;

public static class NavigationExtensions
{
    public static void RequestNavigate(this INavigateAsync service, string target,
        Action<NavigationResult>? callback = null, NavigationParameters? parameters = null)
        => service.RequestNavigate(new Uri(target, UriKind.RelativeOrAbsolute), callback, parameters);

    public static void RequestNavigate(this INavigateAsync service, string target, NavigationParameters parameters,
        Action<NavigationResult>? callback = null) => service.RequestNavigate(target, callback, parameters);

    public static void RequestNavigate(this IRegionManager manager, string regionName, string target,
        Action<NavigationResult>? callback = null, NavigationParameters? parameters = null)
        => manager.RequestNavigate(regionName, new Uri(target, UriKind.RelativeOrAbsolute), callback, parameters);

    public static void RequestNavigate(this IRegionManager manager, string regionName, string target,
        NavigationParameters parameters, Action<NavigationResult>? callback = null)
        => manager.RequestNavigate(regionName, target, callback, parameters);

    public static Task<NavigationResult> RequestNavigateAsync(this INavigateAsync service, Uri target,
        NavigationParameters? parameters = null)
    {
        var completion = new TaskCompletionSource<NavigationResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        service.RequestNavigate(target, result => completion.TrySetResult(result), parameters);
        return completion.Task;
    }

    public static Task<NavigationResult> RequestNavigateAsync(this IRegionManager manager, string regionName,
        string target, NavigationParameters? parameters = null)
    {
        var completion = new TaskCompletionSource<NavigationResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        manager.RequestNavigate(regionName, target, result => completion.TrySetResult(result), parameters);
        return completion.Task;
    }
}
