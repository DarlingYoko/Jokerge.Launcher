using Gml.Launcher.Core.Diagnostics;

namespace Gml.Launcher.Tests.Diagnostics;

/// <summary>
///     The RAM the launcher gives the game; the same number the pre-launch check and the report use.
///     With «автоматически» the panel's recommendation is only a target: the PC's total memory minus an
///     OS reserve and the memory free right now cap it.
/// </summary>
public class RamSelectionTests
{
    #region Manual («автоматически» off)

    [Test]
    public void Manual_UsesSettingsValue_CappedByPhysicalRam()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Allocated(false, 6144, 4096, total: 16384, available: 1000), Is.EqualTo(6144));
            Assert.That(Allocated(false, 32768, 4096, total: 16384, available: null), Is.EqualTo(16384));
        });
    }

    [Test]
    public void Manual_ZeroValue_FallsBackTo1024()
    {
        Assert.That(Allocated(false, 0, 4096, total: 16384, available: null), Is.EqualTo(1024));
    }

    [Test]
    public void Manual_IsNeverReportedAsBelowRecommendation()
    {
        Assert.That(RamSelection.Plan(false, 2048, 8192, 16384, 1000).IsBelowRecommendation, Is.False);
    }

    #endregion

    #region Automatic

    [Test]
    public void Auto_PlentyOfMemory_GivesExactRecommendation()
    {
        var plan = RamSelection.Plan(true, 0, profileRecommendedMb: 6000, totalRamMb: 32768, availableRamMb: 20000);

        Assert.Multiple(() =>
        {
            Assert.That(plan.AllocatedMb, Is.EqualTo(6000), "no rounding when nothing limits it");
            Assert.That(plan.RecommendedMb, Is.EqualTo(6000));
            Assert.That(plan.IsBelowRecommendation, Is.False);
        });
    }

    [Test]
    public void Auto_NoRecommendation_Targets1024()
    {
        Assert.That(Allocated(true, 6144, null, total: 16384, available: 8000), Is.EqualTo(1024));
        Assert.That(Allocated(true, 6144, 0, total: 16384, available: 8000), Is.EqualTo(1024));
    }

    [Test]
    public void Auto_8GbPc_With8GbRecommendation_LeavesReserveForOs()
    {
        // 8192 - max(2048, 25% = 2048) = 6144
        var plan = RamSelection.Plan(true, 0, 8192, totalRamMb: 8192, availableRamMb: 7000);

        Assert.Multiple(() =>
        {
            Assert.That(plan.AllocatedMb, Is.EqualTo(6144));
            Assert.That(plan.IsBelowRecommendation, Is.True);
        });
    }

    [Test]
    public void Auto_BigPc_ReservesQuarter()
    {
        // 32768 - 25% (8192) = 24576
        Assert.That(Allocated(true, 0, 30000, total: 32768, available: 32000), Is.EqualTo(24576));
    }

    [Test]
    public void Auto_FreeMemory_CapsWithHeadroom_RoundedDownTo256()
    {
        // available 5000 - 512 headroom = 4488 → 4352 (17 × 256)
        Assert.That(Allocated(true, 0, 8192, total: 16384, available: 5000), Is.EqualTo(4352));
    }

    [Test]
    public void Auto_AvailableExactlyRecommendationPlusHeadroom_GivesFullRecommendation()
    {
        var plan = RamSelection.Plan(true, 0, 4096, totalRamMb: 16384,
            availableRamMb: 4096 + RamSelection.FreeHeadroomMb);

        Assert.Multiple(() =>
        {
            Assert.That(plan.AllocatedMb, Is.EqualTo(4096));
            Assert.That(plan.IsBelowRecommendation, Is.False);
        });
    }

    [Test]
    public void Auto_AlmostNoFreeMemory_KeepsFloor()
    {
        Assert.That(Allocated(true, 0, 8192, total: 16384, available: 600), Is.EqualTo(RamSelection.MinimumMb));
    }

    [Test]
    public void Auto_UnknownFreeMemory_UsesOnlyTotalReserve()
    {
        Assert.That(Allocated(true, 0, 8192, total: 16384, available: null), Is.EqualTo(8192));
    }

    [Test]
    public void Auto_UnknownTotal_UsesRecommendation()
    {
        Assert.That(Allocated(true, 0, 8192, total: 0, available: null), Is.EqualTo(8192));
    }

    [TestCase(4096, 4096 - 256, false)]
    [TestCase(4096, 4096 - 512, true)]
    public void Auto_BelowRecommendation_OnlyWhenNoticeablyLower(int recommended, int availableAfterHeadroom,
        bool expected)
    {
        var plan = RamSelection.Plan(true, 0, recommended, 65536, availableAfterHeadroom + RamSelection.FreeHeadroomMb);

        Assert.That(plan.IsBelowRecommendation, Is.EqualTo(expected), $"allocated {plan.AllocatedMb}");
    }

    #endregion

    private static int Allocated(bool dynamic, double settingsRam, int? recommended, long total, long? available) =>
        RamSelection.Plan(dynamic, settingsRam, recommended, total, available).AllocatedMb;
}
