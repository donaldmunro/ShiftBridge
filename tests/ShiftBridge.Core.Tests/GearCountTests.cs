using ShiftBridge.Core.Config;
using ShiftBridge.Core.Trainer;
using ShiftBridge.Platform;

namespace ShiftBridge.Core.Tests;

public class GearCountTests
{
    [Fact]
    public void A_table_of_24_gears_is_the_default_table()
    {
        Assert.Equal(VirtualShiftingModel.DefaultRatios, VirtualShiftingModel.BuildRatios(24));
    }

    [Theory]
    [InlineData(8)]
    [InlineData(12)]
    [InlineData(17)]
    [InlineData(20)]
    [InlineData(23)]
    public void Any_gear_count_keeps_the_same_range_and_increases_strictly(int count)
    {
        var table = VirtualShiftingModel.BuildRatios(count);
        Assert.Equal(count, table.Length);
        Assert.Equal(0.75, table[0]);
        Assert.Equal(5.49, table[^1]);
        for (int i = 1; i < table.Length; i++) Assert.True(table[i] > table[i - 1], $"rapport {i} ≤ rapport {i - 1}");
    }

    [Fact]
    public void A_shorter_table_follows_the_same_curve_as_the_default_one()
    {
        var table = VirtualShiftingModel.BuildRatios(12);
        var full = VirtualShiftingModel.DefaultRatios;
        for (int i = 0; i < table.Length; i++)
        {
            // Chaque rapport est pris à la même position relative de l'échelle : entre les deux rapports voisins de la table complète.
            double position = (double)i / (table.Length - 1) * (full.Length - 1);
            double lo = full[(int)Math.Floor(position)], hi = full[(int)Math.Ceiling(position)];
            Assert.InRange(table[i], lo, hi);
        }
    }

    [Fact]
    public void Model_brings_the_current_gear_back_into_range_when_the_count_shrinks()
    {
        var model = new VirtualShiftingModel { Gear = 24 };
        model.SetGearCount(10);
        Assert.Equal(10, model.Ratios.Length);
        Assert.Equal(10, model.Gear);
        Assert.Equal(5.49, model.RatioFor(99)); // borné, jamais hors table
    }

    [Fact]
    public async Task Bridge_keeps_the_same_place_on_the_scale_when_the_count_changes()
    {
        await using var bridge = new BridgeService(new AppConfig { GearCount = 24, LastGear = 24 });
        bridge.SetGearCount(12);
        Assert.Equal(12, bridge.Gear.GearCount);
        Assert.Equal(12, bridge.Gear.Gear);             // la dernière vitesse reste la dernière
        Assert.Equal(5.49, bridge.GearRatio);
        Assert.Equal(12, bridge.Config.GearCount);

        bridge.SetGearCount(24);
        Assert.Equal(24, bridge.Gear.Gear);             // et le retour la retrouve
    }

    [Fact]
    public async Task Bridge_moves_a_middle_gear_proportionally()
    {
        await using var bridge = new BridgeService(new AppConfig { GearCount = 24, LastGear = 12 });
        bridge.SetGearCount(12);
        Assert.Equal(6, bridge.Gear.Gear); // round(11 × 11 / 23) + 1
    }

    [Theory]
    [InlineData(3, 8)]
    [InlineData(99, 24)]
    public async Task Bridge_limits_the_gear_count_to_the_supported_range(int asked, int expected)
    {
        await using var bridge = new BridgeService(new AppConfig { GearCount = 16 });
        bridge.SetGearCount(asked);
        Assert.Equal(expected, bridge.Gear.GearCount);
    }

    [Fact]
    public async Task Bridge_built_with_a_saved_count_uses_a_table_of_that_size()
    {
        await using var bridge = new BridgeService(new AppConfig { GearCount = 12, LastGear = 12 });
        Assert.Equal(5.49, bridge.GearRatio); // dernier rapport d'une table de 12, pas le 12e d'une table de 24 (2,40)
    }

    [Fact]
    public async Task A_gear_count_change_raises_the_gear_event_without_a_flash()
    {
        await using var bridge = new BridgeService(new AppConfig { GearCount = 24, LastGear = 5 });
        int raised = 0;
        bridge.GearChanged += () => raised++;
        bridge.SetGearCount(16);
        Assert.True(raised >= 1);
        Assert.Equal(ShiftBridge.Core.Gears.GearChange.None, bridge.LastGearChange);
    }
}
