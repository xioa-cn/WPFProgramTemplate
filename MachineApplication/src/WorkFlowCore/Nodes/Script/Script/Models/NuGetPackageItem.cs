namespace CsxPad.Wpf.Models;

public sealed record NuGetPackageItem(
    string Id,
    string Version,
    string Description,
    string Authors,
    long Downloads);

public sealed record InstalledPackageItem(string Id, string Version);
