using System.Diagnostics;
using System.Net;
using System.Text.Json;

namespace Blink;

/// <summary>
/// Talks to the GitHub releases API: finds the newest published release and downloads
/// the installer attached to it.
/// </summary>
internal static class UpdateService
{
    private const string InstallerAssetPrefix = "BlinkSetup";

    private static readonly HttpClient Http = CreateClient();

    public static async Task<ReleaseInfo?> GetLatestAsync(CancellationToken token)
    {
        using HttpResponseMessage response =
            await Http.GetAsync(AppInfo.LatestReleaseApiUrl, token).ConfigureAwait(false);

        // No releases yet (or the repository is not reachable): nothing to offer.
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        await using Stream stream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
        using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: token)
            .ConfigureAwait(false);

        JsonElement root = document.RootElement;
        string tag = ReadString(root, "tag_name").TrimStart('v', 'V');
        if (!System.Version.TryParse(tag, out System.Version? version))
        {
            return null;
        }

        return new ReleaseInfo(
            version,
            ReadString(root, "tag_name"),
            ReadString(root, "html_url"),
            ReadString(root, "body"),
            FindInstaller(root, version));
    }

    /// <summary>Downloads an asset, reporting progress from 0 to 1.</summary>
    public static async Task DownloadAsync(
        ReleaseAsset asset,
        string targetPath,
        IProgress<double>? progress,
        CancellationToken token)
    {
        string? directory = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using HttpResponseMessage response = await Http
            .GetAsync(asset.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, token)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        long total = response.Content.Headers.ContentLength ?? asset.SizeBytes;

        await using Stream source = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
        await using FileStream destination = new(
            targetPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true);

        byte[] buffer = new byte[81920];
        long written = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, token).ConfigureAwait(false)) > 0)
        {
            await destination.WriteAsync(buffer.AsMemory(0, read), token).ConfigureAwait(false);
            written += read;
            if (total > 0)
            {
                progress?.Report(Math.Clamp((double)written / total, 0d, 1d));
            }
        }
    }

    /// <summary>Starts the freshly downloaded installer in its silent mode.</summary>
    public static bool LaunchInstaller(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path, "--silent")
            {
                UseShellExecute = true
            });
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Returns an empty download slot outside the install directory, so the installer is
    /// never deleted while it is still running.
    /// </summary>
    public static string CreateDownloadSlot(string fileName)
    {
        string directory = Path.Combine(Path.GetTempPath(), "Blink-Update");
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch
        {
            // A leftover download only wastes disk space: carry on.
        }

        Directory.CreateDirectory(directory);
        return Path.Combine(directory, fileName);
    }

    private static HttpClient CreateClient()
    {
        HttpClient client = new() { Timeout = TimeSpan.FromSeconds(45) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"{AppInfo.Name}/{AppInfo.Version}");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        return client;
    }

    private static ReleaseAsset? FindInstaller(JsonElement root, System.Version version)
    {
        if (!root.TryGetProperty("assets", out JsonElement assets) ||
            assets.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        string preferred = $"{InstallerAssetPrefix}-{version}.exe";
        ReleaseAsset? fallback = null;

        foreach (JsonElement asset in assets.EnumerateArray())
        {
            string name = ReadString(asset, "name");
            if (!name.StartsWith(InstallerAssetPrefix, StringComparison.OrdinalIgnoreCase) ||
                !name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string url = ReadString(asset, "browser_download_url");
            if (url.Length == 0)
            {
                continue;
            }

            long size = asset.TryGetProperty("size", out JsonElement sizeElement) &&
                        sizeElement.TryGetInt64(out long value)
                ? value
                : 0;

            if (string.Equals(name, preferred, StringComparison.OrdinalIgnoreCase))
            {
                return new ReleaseAsset(name, size, url);
            }

            fallback ??= new ReleaseAsset(name, size, url);
        }

        return fallback;
    }

    private static string ReadString(JsonElement element, string property) =>
        element.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
}
