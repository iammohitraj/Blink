// Blink · mohit

namespace Blink;

/// <summary>Outcome of one import pass, so the UI can report what happened without dialogs.</summary>
internal sealed class WallpaperImportResult
{
    public List<WallpaperItem> Added { get; } = [];

    public List<string> Skipped { get; } = [];

    public List<string> Errors { get; } = [];

    public int ProblemCount => Skipped.Count + Errors.Count;
}
