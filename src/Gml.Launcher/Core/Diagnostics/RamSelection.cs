using System;

namespace Gml.Launcher.Core.Diagnostics;

/// <param name="AllocatedMb">What the launcher asks the server to give the game.</param>
/// <param name="RecommendedMb">The target: the panel's recommendation in auto mode, the slider value otherwise.</param>
/// <param name="IsBelowRecommendation">Auto mode had to give noticeably less than recommended.</param>
public sealed record RamPlan(int AllocatedMb, int RecommendedMb, bool IsBelowRecommendation);

/// <summary>
///     How much RAM (MB) the game gets. Manual mode uses the slider value (capped by physical RAM).
///     «Автоматически» treats the profile's recommendation from the panel as a target and fits it to
///     this PC: leave the OS max(2 GB, 25%) of total memory, and never take more than is free right now
///     minus a little headroom, so an 8 GB PC with an 8 GB recommendation does not go into swap.
/// </summary>
public static class RamSelection
{
    public const int Fallback = 1024;
    public const int MinimumMb = 1024;
    public const int StepMb = 256;
    public const int OsReserveMinimumMb = 2048;
    public const double OsReserveShare = 0.25;
    public const int FreeHeadroomMb = 512;
    public const int BelowRecommendationThresholdMb = 512;

    public static RamPlan Plan(bool isDynamicRam, double settingsRamMb, int? profileRecommendedMb,
        long totalRamMb, long? availableRamMb)
    {
        return isDynamicRam
            ? PlanAutomatic(profileRecommendedMb is > 0 ? profileRecommendedMb.Value : Fallback, totalRamMb,
                availableRamMb)
            : PlanManual(settingsRamMb, totalRamMb);
    }

    private static RamPlan PlanManual(double settingsRamMb, long totalRamMb)
    {
        var wanted = Convert.ToInt32(settingsRamMb);
        var allocated = totalRamMb > 0 ? (int)Math.Min(wanted, totalRamMb) : wanted;
        if (allocated <= 0) allocated = Fallback;

        return new RamPlan(allocated, wanted, false);
    }

    private static RamPlan PlanAutomatic(int targetMb, long totalRamMb, long? availableRamMb)
    {
        long cap = targetMb;

        if (totalRamMb > 0)
        {
            var osReserve = Math.Max(OsReserveMinimumMb, (long)(totalRamMb * OsReserveShare));
            cap = Math.Min(cap, totalRamMb - osReserve);
        }

        if (availableRamMb is { } available)
            cap = Math.Min(cap, available - FreeHeadroomMb);

        // Round only when a limit applied: the admin's exact recommendation stays as set.
        var allocated = cap < targetMb ? cap - cap % StepMb : targetMb;
        allocated = Math.Max(allocated, MinimumMb);

        return new RamPlan((int)allocated, targetMb, targetMb - allocated >= BelowRecommendationThresholdMb);
    }
}
