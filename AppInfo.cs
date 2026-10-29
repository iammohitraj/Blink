namespace Blink;

/// <summary>Static brand, version and distribution metadata for Blink.</summary>
internal static class AppInfo
{
    public const string Name = "Blink";

    public const string Tagline = "Desktop icons, one blink away.";

    public const string HotKeyText = "Ctrl + Alt + D";

    /// <summary>GitHub account that publishes Blink releases.</summary>
    public const string RepositoryOwner = "iammohitraj";

    /// <summary>GitHub repository that publishes Blink releases.</summary>
    public const string RepositoryName = "Blink";

    public static string RepositoryUrl => $"https://github.com/{RepositoryOwner}/{RepositoryName}";

    public static string ReleasesPageUrl => $"{RepositoryUrl}/releases";

    public static string LatestReleaseApiUrl =>
        $"https://api.github.com/repos/{RepositoryOwner}/{RepositoryName}/releases/latest";

    public static string Version
    {
        get
        {
            string version = Application.ProductVersion;
            int metadataIndex = version.IndexOf('+');
            return metadataIndex > 0 ? version[..metadataIndex] : version;
        }
    }

    /// <summary>The current version as a comparable value, used by the update check.</summary>
    public static System.Version ParsedVersion =>
        System.Version.TryParse(Version, out System.Version? parsed) ? parsed : new System.Version(0, 0);
}
