using BravoBisConfigurator.Core.Profile;
using Xunit;

namespace BravoBisConfigurator.Core.Tests;

/// <summary>Ported 1:1 from internal/profile/profile_test.go.</summary>
public class ProfileTests
{
    [Fact]
    public void All_HasBothProfiles()
    {
        Assert.Equal(2, ProfileDefinition.All().Count);
    }

    [Fact]
    public void TryFind_KnownAndUnknown()
    {
        Assert.True(ProfileDefinition.TryFind("bravo", out _));
        Assert.True(ProfileDefinition.TryFind("bis", out _));
        Assert.False(ProfileDefinition.TryFind("nope", out _));
    }

    [Theory]
    [InlineData("bravo")]
    [InlineData("bis")]
    public void LoadSchema_BothProfilesLoadTheirBundledSchema(string name)
    {
        Assert.True(ProfileDefinition.TryFind(name, out var p));
        var s = p.LoadSchema();
        Assert.Equal(name, s.ProfileName);
    }
}
