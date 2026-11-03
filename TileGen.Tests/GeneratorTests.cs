using TileGen.Core;
using Xunit;

namespace TileGen.Tests;

public class GeneratorTests
{
    [Theory]
    [InlineData(64)]
    [InlineData(128)]
    [InlineData(256)]
    public void Render_ReturnsSizeBySizeRgb(int size)
    {
        var preset = ShippedPresets.Load("lava_cave");
        var request = TileRequest.WithDefaults(preset, TileKind.Floor, ShippedPresets.TestSeed, size);

        byte[] pixels = Generator.Render(request);

        Assert.Equal(size * size * 3, pixels.Length);
    }

    [Theory]
    [MemberData(nameof(ShippedPresets.IdsAndKinds), MemberType = typeof(ShippedPresets))]
    public void Render_SameRequestTwice_GivesIdenticalBytes(string presetId, TileKind kind)
    {
        var preset = ShippedPresets.Load(presetId);
        var request = TileRequest.WithDefaults(preset, kind, ShippedPresets.TestSeed, ShippedPresets.TestSize);

        byte[] first = Generator.Render(request);
        byte[] second = Generator.Render(request);

        Assert.Equal(first, second);
    }
}
