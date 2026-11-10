// Blink · mohit

using System.Drawing;

namespace Blink.Tests;

public sealed class MiniWallpaperLayoutTests
{
    private static readonly Rectangle FullHd = new(0, 0, 1920, 1040);

    [Fact]
    public void Place_PutsTheOverlayInEachCorner_withTheMarginApplied()
    {
        Size size = new(240, 120);

        Assert.Equal(new Rectangle(24, 24, 240, 120), MiniWallpaperLayout.Place(MiniWallpaperCorner.TopLeft, FullHd, size, 24));
        Assert.Equal(new Rectangle(1656, 24, 240, 120), MiniWallpaperLayout.Place(MiniWallpaperCorner.TopRight, FullHd, size, 24));
        Assert.Equal(new Rectangle(24, 896, 240, 120), MiniWallpaperLayout.Place(MiniWallpaperCorner.BottomLeft, FullHd, size, 24));
        Assert.Equal(new Rectangle(1656, 896, 240, 120), MiniWallpaperLayout.Place(MiniWallpaperCorner.BottomRight, FullHd, size, 24));
    }

    [Fact]
    public void Place_RespectsAWorkingAreaThatDoesNotStartAtTheOrigin()
    {
        Rectangle area = new(100, 50, 800, 600);
        Rectangle placed = MiniWallpaperLayout.Place(MiniWallpaperCorner.TopRight, area, new Size(200, 100), 10);

        Assert.Equal(new Rectangle(690, 60, 200, 100), placed);
    }

    [Fact]
    public void Measure_KeepsTheSourceAspectRatio()
    {
        Size measured = MiniWallpaperLayout.Measure(new Size(400, 200), 240, 1f);

        Assert.Equal(new Size(240, 120), measured);
    }

    [Fact]
    public void Measure_ClampsAVeryWideImageToAShortStrip()
    {
        Size measured = MiniWallpaperLayout.Measure(new Size(1000, 100), 240, 1f);

        Assert.Equal(new Size(240, 84), measured);
    }

    [Fact]
    public void Measure_ClampsAVeryTallImageToTheMaximumHeight()
    {
        Size measured = MiniWallpaperLayout.Measure(new Size(100, 1000), 240, 1f);

        Assert.Equal(new Size(240, 300), measured);
    }

    [Fact]
    public void Measure_ScalesWithTheDisplayDpi()
    {
        Size measured = MiniWallpaperLayout.Measure(new Size(400, 400), 240, 2f);

        Assert.Equal(new Size(480, 480), measured);
    }

    [Fact]
    public void Measure_FallsBackToASquareForAnInvalidImage()
    {
        Assert.Equal(new Size(160, 160), MiniWallpaperLayout.Measure(Size.Empty, 160, 1f));
    }

    [Fact]
    public void BaseWidth_GrowsWithEachSize()
    {
        Assert.True(MiniWallpaperLayout.BaseWidth(MiniWallpaperSize.Small) < MiniWallpaperLayout.BaseWidth(MiniWallpaperSize.Medium));
        Assert.True(MiniWallpaperLayout.BaseWidth(MiniWallpaperSize.Medium) < MiniWallpaperLayout.BaseWidth(MiniWallpaperSize.Large));
    }

    [Fact]
    public void Fit_CentresTheImageInsideTheBounds()
    {
        Rectangle fitted = MiniWallpaperLayout.Fit(new Size(200, 100), new Rectangle(0, 0, 300, 300));

        Assert.Equal(new Rectangle(0, 75, 300, 150), fitted);
    }

    [Fact]
    public void Fit_HonoursTheBoundsOffset()
    {
        Rectangle fitted = MiniWallpaperLayout.Fit(new Size(1000, 500), new Rectangle(10, 20, 200, 200));

        Assert.Equal(new Rectangle(10, 70, 200, 100), fitted);
    }

    [Fact]
    public void Fit_ReturnsTheBoundsWhenTheImageIsInvalid()
    {
        Rectangle bounds = new(5, 6, 7, 8);

        Assert.Equal(bounds, MiniWallpaperLayout.Fit(Size.Empty, bounds));
    }

    [Fact]
    public void Cover_FillsTheBoundsAndOverflowsOnTheLongAxis()
    {
        Rectangle covered = MiniWallpaperLayout.Cover(new Size(200, 100), new Rectangle(0, 0, 300, 300));

        Assert.Equal(new Rectangle(-150, 0, 600, 300), covered);
    }

    [Fact]
    public void Cover_OverflowsVerticallyForATallImage()
    {
        Rectangle covered = MiniWallpaperLayout.Cover(new Size(100, 200), new Rectangle(0, 0, 300, 300));

        Assert.Equal(new Rectangle(0, -150, 300, 600), covered);
    }

    [Fact]
    public void PlaceCustom_PositionsOverlayAtExactCoordinates()
    {
        Rectangle placed = MiniWallpaperLayout.PlaceCustom(FullHd, 400, 300, 200, 150);
        Assert.Equal(new Rectangle(400, 300, 200, 150), placed);
    }

    [Fact]
    public void PlaceCustom_ClampsWhenOverflowingRightOrBottom()
    {
        Rectangle placed = MiniWallpaperLayout.PlaceCustom(FullHd, 2000, 1500, 200, 100);
        Assert.Equal(new Rectangle(1720, 940, 200, 100), placed);
    }

    [Fact]
    public void PlaceCustom_CentersWhenCoordinatesAreNegative()
    {
        Rectangle placed = MiniWallpaperLayout.PlaceCustom(FullHd, -1, -1, 300, 200);
        Assert.Equal(new Rectangle(810, 420, 300, 200), placed);
    }

    [Fact]
    public void PlaceCustom_EnforcesMinimumDimensions()
    {
        Rectangle placed = MiniWallpaperLayout.PlaceCustom(FullHd, 100, 100, 10, 5);
        Assert.Equal(32, placed.Width);
        Assert.Equal(32, placed.Height);
    }
}
