namespace UnifiedMessenger.Services;

/// <summary>
/// Shared arithmetic for percentages the owner acts on.
/// </summary>
/// <remarks>
/// This exists because the same rounding defect was found independently in four places — the two
/// caught-up percentages in <see cref="OversightRollupBuilder"/>, and both SLA-compliance percentages in
/// <see cref="ResponseTimeTracker"/>. Each looked innocuous on its own; together they meant the product
/// could tell an owner they were finished when they were not, on more than one screen.
/// </remarks>
internal static class MetricMath
{
    /// <summary>
    /// Percentage that never claims more than the counts support: 100 only when nothing is outstanding,
    /// 0 only when nothing qualified.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Plain <c>Math.Round</c> turns 996/1000 (99.6%) into <b>100</b> and 1/1000 (0.1%) into <b>0</b>.
    /// On this scale those two values are not ordinary readings, they are claims — "nothing outstanding"
    /// and "nothing done" — and rounding could manufacture either from counts that did not support it.
    /// The visible symptom was a green "100% caught up" sitting directly beside "4 awaiting" on the same
    /// card, and an "SLA met 100%" beside a reply set containing a breach.
    /// </para>
    /// <para>
    /// Reserving the endpoints means the figure can under-claim by less than a point but can never
    /// over-claim. That is the correct direction to be wrong in for a number someone staffs a branch on.
    /// </para>
    /// </remarks>
    /// <param name="part">How many met the bar.</param>
    /// <param name="total">How many were measured.</param>
    public static int HonestPercent(int part, int total)
    {
        if (total <= 0)
        {
            // Nothing measured. Callers decide whether that means "100" (nothing outstanding) or should be
            // suppressed entirely; the rollup suppresses it via MeasuredCount, which is the safer route.
            return 100;
        }

        if (part >= total)
        {
            return 100;
        }

        if (part <= 0)
        {
            return 0;
        }

        return Math.Clamp((int)Math.Round((double)part / total * 100), 1, 99);
    }
}
