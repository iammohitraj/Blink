// Blink · mohit

using Blink.Core;

namespace Blink.Core.Tests;

public sealed class SemanticVersionTests
{
    [Fact]
    public void TryParse_AcceptsTagsAndBuildMetadata()
    {
        Assert.True(SemanticVersion.TryParse("v1.2.3", out SemanticVersion version));
        Assert.Equal(1, version.Major);
        Assert.Equal(2, version.Minor);
        Assert.Equal(3, version.Patch);
        Assert.Null(version.Prerelease);
        Assert.Equal("1.2.3", version.ToString());

        Assert.True(SemanticVersion.TryParse("1.2.3-rc.1+build.7", out SemanticVersion prerelease));
        Assert.Equal("rc.1", prerelease.Prerelease);

        Assert.True(SemanticVersion.TryParse("2.0", out SemanticVersion shortVersion));
        Assert.Equal(new SemanticVersion(2, 0, 0), shortVersion);
    }

    [Fact]
    public void TryParse_RejectsMalformedVersions()
    {
        Assert.False(SemanticVersion.TryParse(null, out _));
        Assert.False(SemanticVersion.TryParse(string.Empty, out _));
        Assert.False(SemanticVersion.TryParse("v", out _));
        Assert.False(SemanticVersion.TryParse("1.2.x", out _));
        Assert.False(SemanticVersion.TryParse("1..2", out _));
        Assert.False(SemanticVersion.TryParse("1.2.3-", out _));
    }

    [Fact]
    public void Comparison_FollowsSemVerPrecedence()
    {
        Assert.True(SemanticVersion.Parse("1.2.3") > SemanticVersion.Parse("1.2.3-rc.1"));
        Assert.True(SemanticVersion.Parse("1.2.3-alpha.2") > SemanticVersion.Parse("1.2.3-alpha.1"));
        Assert.True(SemanticVersion.Parse("1.2.3-alpha.1") < SemanticVersion.Parse("1.2.3-alpha.beta"));
        Assert.True(SemanticVersion.Parse("2.0.0") > SemanticVersion.Parse("1.9.9"));
        Assert.True(SemanticVersion.Parse("1.0.0-alpha") < SemanticVersion.Parse("1.0.0-alpha.1"));

        Assert.Equal(
            SemanticVersion.Parse("1.2.3+one"),
            SemanticVersion.Parse("1.2.3+two"));
    }
}
