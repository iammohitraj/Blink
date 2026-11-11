// Blink · mohit

using System.Drawing;
using System.Drawing.Imaging;

namespace Blink.Tests;

public sealed class WallpaperLibraryTests : IDisposable
{
    private readonly string root;
    private readonly WallpaperLibrary library;

    public WallpaperLibraryTests()
    {
        root = Path.Combine(Path.GetTempPath(), "Blink.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        library = new WallpaperLibrary(root);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(root, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    [Fact]
    public void Import_CopiesTheFileIntoTheLibrary()
    {
        string source = CreateImage(Path.Combine(root, "sample.png"));

        WallpaperImportResult result = library.Import([source]);

        WallpaperItem item = Assert.Single(result.Added);
        Assert.Equal("sample", item.DisplayName);
        Assert.Equal("sample.png", item.Id);
        Assert.False(item.IsAnimated);
        Assert.True(File.Exists(item.Path));
        Assert.StartsWith(Path.Combine(root, "wallpapers"), item.Path, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(result.Skipped);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Import_GeneratesAThumbnail()
    {
        WallpaperItem item = Import(CreateImage(Path.Combine(root, "sample.png")));

        string thumbnail = library.GetThumbnail(item);

        Assert.NotEqual(item.Path, thumbnail);
        Assert.True(File.Exists(thumbnail));
        Size size = ReadSize(thumbnail);
        Assert.Equal(256, size.Width);
        Assert.Equal(144, size.Height);
    }

    [Fact]
    public void Import_KeepsBothCopiesOfTheSameFileName()
    {
        string source = CreateImage(Path.Combine(root, "sample.png"));

        library.Import([source]);
        library.Import([source]);

        string[] names = library.List().Select(item => item.DisplayName).ToArray();
        Assert.Equal(["sample", "sample (2)"], names);
    }

    [Fact]
    public void Import_SkipsUnsupportedFiles()
    {
        string source = Path.Combine(root, "notes.txt");
        File.WriteAllText(source, "not an image");

        WallpaperImportResult result = library.Import([source]);

        Assert.Empty(result.Added);
        Assert.Single(result.Skipped);
        Assert.Contains("unsupported format", result.Skipped[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Import_RejectsAFileThatOnlyLooksLikeAnImage()
    {
        string source = Path.Combine(root, "broken.png");
        File.WriteAllBytes(source, [0x89, 0x50, 0x4E, 0x47, 0x00, 0x01, 0x02, 0x03]);

        WallpaperImportResult result = library.Import([source]);

        Assert.Empty(result.Added);
        Assert.Equal(1, result.ProblemCount);
        Assert.Empty(library.List());
    }

    [Fact]
    public void Import_SkipsAFileThatIsAlreadyInTheLibrary()
    {
        WallpaperItem item = Import(CreateImage(Path.Combine(root, "sample.png")));

        WallpaperImportResult result = library.Import([item.Path]);

        Assert.Empty(result.Added);
        Assert.Contains("already in the library", result.Skipped[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Import_ReportsAMissingFile()
    {
        WallpaperImportResult result = library.Import([Path.Combine(root, "missing.png")]);

        Assert.Empty(result.Added);
        Assert.Contains("not found", result.Skipped[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Remove_DeletesTheImageAndItsCachedFiles()
    {
        WallpaperItem item = Import(CreateImage(Path.Combine(root, "sample.png")));
        string thumbnail = library.GetThumbnail(item);
        Assert.True(File.Exists(thumbnail));

        Assert.True(library.Remove(item));

        Assert.False(File.Exists(item.Path));
        Assert.False(File.Exists(thumbnail));
        Assert.Empty(library.List());
    }

    [Fact]
    public void PrepareDesktopImage_ConvertsAGifToACachedStill()
    {
        WallpaperItem item = Import(CreateImage(Path.Combine(root, "loop.gif"), format: ImageFormat.Gif));
        Assert.True(item.IsAnimated);

        string still = library.PrepareDesktopImage(item);

        Assert.NotEqual(item.Path, still);
        Assert.EndsWith(".png", still, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(library.StaticFramesDirectory, still, StringComparison.OrdinalIgnoreCase);
        byte[] signature = File.ReadAllBytes(still)[..8];
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, signature);
    }

    [Fact]
    public void PrepareDesktopImage_ReusesTheCachedStill()
    {
        WallpaperItem item = Import(CreateImage(Path.Combine(root, "loop.gif"), format: ImageFormat.Gif));

        string first = library.PrepareDesktopImage(item);
        string second = library.PrepareDesktopImage(item);

        Assert.Equal(first, second);
    }

    [Fact]
    public void PrepareDesktopImage_LeavesStillImagesUntouched()
    {
        WallpaperItem item = Import(CreateImage(Path.Combine(root, "sample.png")));

        Assert.Equal(item.Path, library.PrepareDesktopImage(item));
    }

    [Fact]
    public void Find_RejectsPathsThatEscapeTheLibrary()
    {
        Assert.Null(library.Find(null));
        Assert.Null(library.Find("..\\outside.png"));
        Assert.Null(library.Find("nested/outside.png"));
        Assert.Null(library.Find("missing.png"));
    }

    [Fact]
    public void Find_ReturnsTheImportedItem()
    {
        WallpaperItem item = Import(CreateImage(Path.Combine(root, "sample.png")));

        WallpaperItem? found = library.Find(item.Id);

        Assert.NotNull(found);
        Assert.Equal(item.DisplayName, found.DisplayName);
        Assert.Equal(item.Path, found.Path);
    }

    [Fact]
    public void List_SortsByDisplayName()
    {
        Import(CreateImage(Path.Combine(root, "zeta.png")));
        Import(CreateImage(Path.Combine(root, "alpha.png")));

        string[] names = library.List().Select(item => item.DisplayName).ToArray();

        Assert.Equal(["alpha", "zeta"], names);
    }

    [Fact]
    public void IsSupported_AcceptsTheDocumentedFormats()
    {
        Assert.True(WallpaperLibrary.IsSupported("photo.jpg"));
        Assert.True(WallpaperLibrary.IsSupported("photo.JPEG"));
        Assert.True(WallpaperLibrary.IsSupported("photo.jfif"));
        Assert.True(WallpaperLibrary.IsSupported("photo.png"));
        Assert.True(WallpaperLibrary.IsSupported("photo.bmp"));
        Assert.True(WallpaperLibrary.IsSupported("loop.GIF"));
        Assert.True(WallpaperLibrary.IsSupported("photo.webp"));
        Assert.True(WallpaperLibrary.IsSupported("logo.svg"));
        Assert.True(WallpaperLibrary.IsSupported("app.ico"));
        Assert.True(WallpaperLibrary.IsSupported("document.tiff"));
        Assert.False(WallpaperLibrary.IsSupported("video.mp4"));
        Assert.False(WallpaperLibrary.IsSupported("document.pdf"));
    }

    [Fact]
    public void Import_SupportsSvgVectorFiles()
    {
        string svgPath = Path.Combine(root, "test-logo.svg");
        File.WriteAllText(svgPath, "<svg viewBox=\"0 0 100 100\" xmlns=\"http://www.w3.org/2000/svg\"><circle cx=\"50\" cy=\"50\" r=\"40\" fill=\"red\" /></svg>");

        WallpaperItem item = Import(svgPath);

        Assert.Equal("test-logo", item.DisplayName);
        Assert.Equal("test-logo.svg", item.Id);
        Assert.True(File.Exists(item.Path));
        string thumbnail = library.GetThumbnail(item);
        Assert.True(File.Exists(thumbnail));
    }

    [Fact]
    public void Rasterize_SupportsComplexVectorGraphics()
    {
        string emblemPath = @"C:\Users\KIIT\AppData\Roaming\Blink\Wallpapers\Emblem_of_India (1)-cropped.svg";
        if (File.Exists(emblemPath))
        {
            using Bitmap? bmp = SvgRasterizer.TryRasterize(emblemPath);
            Assert.NotNull(bmp);
            Assert.True(bmp.Width > 0);
            Assert.True(bmp.Height > 0);
            // Verify crisp dimension
            Assert.True(bmp.Width >= 500 || bmp.Height >= 500);
        }
    }

    private WallpaperItem Import(string source)
    {
        WallpaperImportResult result = library.Import([source]);
        return Assert.Single(result.Added);
    }

    private static string CreateImage(string path, int width = 64, int height = 40, ImageFormat? format = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using Bitmap bitmap = new(width, height, PixelFormat.Format32bppArgb);
        using (Graphics graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.CornflowerBlue);
            graphics.FillEllipse(Brushes.Gold, 4, 4, width / 2, height / 2);
        }

        bitmap.Save(path, format ?? ImageFormat.Png);
        return path;
    }

    private static Size ReadSize(string path)
    {
        using FileStream stream = File.OpenRead(path);
        using Image image = Image.FromStream(stream, useEmbeddedColorManagement: false, validateImageData: true);
        return image.Size;
    }
}
