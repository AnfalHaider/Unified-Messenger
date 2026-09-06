using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

/// <summary>
/// The short line under a dashboard KPI saying what that figure covers (mockup §02).
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a KPI needs its own line when the cards below already have chips.</b> The chips say what an
/// <i>account</i> can supply; a KPI is an aggregate across accounts, and the two answers differ per
/// figure. "Response time" and "Messages / day" are WhatsApp-only even on a dashboard where Instagram
/// contributes to "Backlog" — so a single notice at the bottom of the page cannot tell the owner which of
/// the six numbers in front of them is narrower than it looks.
/// </para>
/// <para>
/// <b>Empty when it covers everything.</b> A caption on every tile is furniture, and the tiles that need
/// the disclosure stop standing out. This returns an empty string rather than "all accounts".
/// </para>
/// </remarks>
public static class KpiCoverage
{
    /// <summary>Figures built from conversation snapshots — awaiting, caught-up, backlog.</summary>
    public static string ForConversations(IEnumerable<MessengerInstance>? instances) =>
        Describe(instances, capabilities => capabilities.ContributesConversationMetrics);

    /// <summary>Figures built from reply timing — response time, SLA met.</summary>
    public static string ForReplyTiming(IEnumerable<MessengerInstance>? instances) =>
        Describe(instances, capabilities => capabilities.SupportsFrt);

    /// <summary>Figures built from per-message analytics — messages per day, busiest window.</summary>
    public static string ForMessageAnalytics(IEnumerable<MessengerInstance>? instances) =>
        Describe(instances, capabilities => capabilities.UsesWhatsAppIndexedDbPipeline);

    private static string Describe(
        IEnumerable<MessengerInstance>? instances,
        Func<PlatformCapabilities, bool> qualifies)
    {
        var all = (instances ?? [])
            .Where(instance => instance is not null && instance.IsProfessional)
            .ToList();

        if (all.Count == 0)
        {
            return string.Empty;
        }

        var contributing = all
            .Where(instance => qualifies(PlatformDefinition.CapabilitiesFor(instance.Platform)))
            .ToList();

        // Signed out is counted only among accounts that could otherwise contribute. A signed-out Google
        // account is irrelevant to a WhatsApp-only figure, and naming it would send the owner to fix
        // something that would not change the number.
        var signedOut = contributing.Count(instance => SignInGate.IsSignedOut(instance.Id));
        var live = contributing.Count - signedOut;

        if (contributing.Count == all.Count && signedOut == 0)
        {
            return string.Empty;
        }

        var channels = contributing
            .Select(instance => PlatformDefinition.FindById(instance.Platform)?.DisplayName ?? "Other")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Nothing contributes at all. Says so rather than naming an empty channel list, because "covers
        // no channels" is the one case where the number above is not a measurement of anything.
        if (contributing.Count == 0)
        {
            return "No connected channel supplies this";
        }

        var scope = channels.Count switch
        {
            1 => $"{channels[0]} only",
            2 => $"{channels[0]} and {channels[1]} only",
            _ => $"{live + signedOut} of {all.Count} accounts"
        };

        return signedOut > 0 ? $"{scope} · {signedOut} signed out" : scope;
    }
}
