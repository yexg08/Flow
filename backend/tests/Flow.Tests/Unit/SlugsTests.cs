using Flow.Application.Common;

namespace Flow.Tests.Unit;

public class SlugsTests
{
    [Theory]
    [InlineData("Peluquería Blue", "peluqueria-blue")]
    [InlineData("  Café & Té  ", "cafe-te")]
    [InlineData("Dra. María Núñez", "dra-maria-nunez")]
    [InlineData("Taller 24/7", "taller-24-7")]
    public void FromName_builds_url_friendly_slugs(string name, string expected) =>
        Assert.Equal(expected, Slugs.FromName(name));

    [Theory]
    [InlineData("peluqueria-blue", true)]
    [InlineData("abc", true)]
    [InlineData("ab", false)]
    [InlineData("con--doble", false)]
    [InlineData("-inicio", false)]
    [InlineData("Mayus", false)]
    [InlineData("con espacio", false)]
    public void HasValidFormat(string slug, bool expected) => Assert.Equal(expected, Slugs.HasValidFormat(slug));
}
