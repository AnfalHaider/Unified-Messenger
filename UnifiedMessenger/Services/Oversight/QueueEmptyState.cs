namespace UnifiedMessenger.Services;

/// <summary>
/// The sentence shown when the needs-a-reply queue has no rows (mockup §14).
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this is not just a string.</b> "All caught up" is the most consequential sentence in the
/// product: it is the one the owner acts on by closing the app and going home. An empty queue over a set
/// that includes an account nothing is reading is therefore the most expensive false calm available — and
/// it read exactly the same as a genuine all-clear.
/// </para>
/// <para>
/// The qualifier is appended rather than replacing the claim, because the claim about the accounts that
/// <i>are</i> being read is still true and still worth stating. An owner who has one expired session does
/// not need the good news withheld; they need it bounded.
/// </para>
/// </remarks>
public static class QueueEmptyState
{
    public static string Describe(string? scopeLabel, int signedOutCount)
    {
        var claim = string.IsNullOrWhiteSpace(scopeLabel)
            ? "All caught up — no customers are waiting on a reply."
            : $"{scopeLabel} is all caught up — no customers waiting.";

        if (signedOutCount <= 0)
        {
            return claim;
        }

        return signedOutCount == 1
            ? $"{claim} 1 signed-out account is not included — nothing has been read from it."
            : $"{claim} {signedOutCount} signed-out accounts are not included — nothing has been read from them.";
    }
}
