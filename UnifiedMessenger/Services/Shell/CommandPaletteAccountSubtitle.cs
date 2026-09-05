namespace UnifiedMessenger.Services;

/// <summary>
/// The one line under an account's name in the command palette (mockup §12).
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this needed changing.</b> The palette listed every account as
/// <c>"WhatsApp · Professional"</c> — its channel and its workspace, and nothing about whether the app can
/// see it. Ctrl+K is a jump list read at speed, and an account that looks identical to its neighbours
/// reads as being in the same state as its neighbours. A signed-out account appearing normal in the one
/// surface the owner uses to move between accounts is the quiet version of the same false calm the
/// dashboard cards were fixed for.
/// </para>
/// <para>
/// <b>Signed out replaces the waiting count rather than joining it.</b> There is no honest waiting figure
/// for an account nothing has read, and printing "0 waiting" beside "signed out" would answer the
/// question the owner is actually asking — <i>does this need me?</i> — with a number that means nothing.
/// </para>
/// </remarks>
public static class CommandPaletteAccountSubtitle
{
    public static string Build(string channelName, string category, bool signedOut, int awaitingCount)
    {
        var head = string.IsNullOrWhiteSpace(category)
            ? channelName
            : $"{channelName} · {category}";

        if (signedOut)
        {
            return $"{head} · Signed out";
        }

        // Silent at zero. A count on every row is noise, and the palette is scanned rather than read — the
        // rows worth stopping on should be the ones carrying a number.
        return awaitingCount <= 0
            ? head
            : $"{head} · {(awaitingCount == 1 ? "1 waiting" : $"{awaitingCount} waiting")}";
    }
}
