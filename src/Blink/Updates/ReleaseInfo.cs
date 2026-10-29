// Blink · mohit

using Blink.Core;

namespace Blink;

/// <summary>A downloadable file attached to a GitHub release.</summary>
internal sealed record ReleaseAsset(string Name, long SizeBytes, string DownloadUrl);

/// <summary>The parts of a GitHub release that Blink needs in order to update itself.</summary>
internal sealed record ReleaseInfo(
    SemanticVersion Version,
    string TagName,
    string PageUrl,
    string Notes,
    ReleaseAsset? Installer)
{
    public bool IsNewerThan(SemanticVersion current) => Version > current;
}
