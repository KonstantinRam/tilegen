using TileGen.Core;
using TileGen.IO;

namespace TileGen.Tests;

/// <summary>
/// The presets the tool ships, read from the copies next to the test binary. A new preset is added to the list
/// in the same change that adds the file, so the tileability and coverage tests cover it from its first render.
/// </summary>
public static class ShippedPresets
{
    public static readonly string[] Ids = { "lava_cave", "greek_ruins", "ice_cave", "cursed_lands", "dungeon", "medieval", "swamp", "temple", "enchanted", "magic_cave", "crystal_cave",
        "tavern", "forgotten_castle", "wizard_tower", "elven_hall", "dwarven_forge", "catacombs", "throne_room", "dragon_lair", "haunted_library", "frozen_fortress", "sunken_temple", "mushroom_grotto" };

    public const int TestSize = 256;
    public const ulong TestSeed = 1234;

    public static Preset Load(string id)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "presets", id + ".json");
        return PresetFile.Load(path);
    }

    public static IEnumerable<object[]> IdsAndKinds()
    {
        foreach (string id in Ids)
        {
            yield return new object[] { id, TileKind.Floor };
            yield return new object[] { id, TileKind.Wall };
        }
    }

    public static IEnumerable<object[]> IdsKindsAndCoverages()
    {
        foreach (string id in Ids)
        {
            foreach (TileKind kind in new[] { TileKind.Floor, TileKind.Wall })
            {
                foreach (double coverage in new[] { 0.0, 0.25, 0.5, 0.75, 1.0 })
                {
                    yield return new object[] { id, kind, coverage };
                }
            }
        }
    }
}
