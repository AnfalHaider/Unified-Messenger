using System.Text;
using System.Text.Json;

namespace UnifiedMessenger.Services;

/// <summary>One reusable answer, with the facets it is offered for.</summary>
public sealed class SavedReply
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Short name shown in the menu — what the owner scans for.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>The text itself, which may contain placeholders. See <see cref="SavedReplyText"/>.</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>
    /// Facets this reply is suggested for. Empty means "always offered", which is right for a greeting.
    /// </summary>
    public List<QueueFacet> Facets { get; set; } = [];

    /// <summary>How many times it has been copied. Sorts the menu by what the owner actually uses.</summary>
    public int UseCount { get; set; }
}

/// <summary>
/// Fills the placeholders in a saved reply.
/// </summary>
/// <remarks>
/// Deliberately tiny and deliberately forgiving. Every unknown placeholder is left exactly as written
/// rather than blanked, because a reply that silently loses a word is worse than one that visibly still has
/// <c>{whatever}</c> in it — the owner reads the text before sending either way, and one of those two
/// mistakes is catchable at a glance.
/// </remarks>
public static class SavedReplyText
{
    /// <summary>Placeholders the owner can use, with a description, for the editor's help text.</summary>
    public static readonly (string Token, string Meaning)[] Placeholders =
    [
        ("{name}", "the customer's name as WhatsApp has it"),
        ("{first}", "just their first name"),
        ("{branch}", "the branch this conversation belongs to"),
        ("{account}", "the account name the message arrived on")
    ];

    public static string Fill(string? body, string? customerName, string? branch, string? account)
    {
        if (string.IsNullOrEmpty(body))
        {
            return string.Empty;
        }

        var name = Clean(customerName);
        var result = new StringBuilder(body);

        // A number rather than a name is what WhatsApp gives for an unsaved contact, and "Hi +923001234567"
        // reads worse than no name at all — so a numeric name falls back to a neutral greeting word.
        var usableName = LooksLikeAPhoneNumber(name) ? string.Empty : name;
        var first = FirstWord(usableName);

        Replace(result, "{name}", usableName.Length > 0 ? usableName : "there");
        Replace(result, "{first}", first.Length > 0 ? first : "there");
        Replace(result, "{branch}", Clean(branch));
        Replace(result, "{account}", Clean(account));

        return result.ToString().Trim();
    }

    private static void Replace(StringBuilder sb, string token, string value) => sb.Replace(token, value);

    private static string Clean(string? value) => (value ?? string.Empty).Trim();

    private static string FirstWord(string value)
    {
        var space = value.IndexOf(' ');
        return space > 0 ? value[..space] : value;
    }

    /// <summary>True when the "name" is really a phone number — an unsaved contact.</summary>
    internal static bool LooksLikeAPhoneNumber(string value)
    {
        if (value.Length == 0)
        {
            return false;
        }

        var digits = 0;
        foreach (var c in value)
        {
            if (char.IsDigit(c))
            {
                digits++;
            }
            else if (c is not ('+' or '-' or ' ' or '(' or ')'))
            {
                return false;
            }
        }

        return digits >= 6;
    }
}

/// <summary>
/// The owner's library of reusable answers, persisted locally.
///
/// <para>
/// <b>Why this earns its place.</b> Measured on real traffic, the enquiries are dominated by four
/// questions — what do you charge, what services do you have, what are your timings, where are you. The
/// answers never change and were being retyped by hand dozens of times a week.
/// </para>
/// <para>
/// <b>It copies; it never sends.</b> The app's standing rule is that it never auto-sends, and automation is
/// read-only. A saved reply therefore ends at the clipboard: the owner pastes it into WhatsApp, reads it,
/// and presses send themselves. That is not a limitation to work around — a canned message fired at a
/// customer without a human reading it first is exactly the failure this product exists to prevent.
/// </para>
/// </summary>
public sealed class SavedReplyStore
{
    private const string FileName = "saved-replies.json";

    private static readonly Lazy<SavedReplyStore> LazyInstance = new(() => new SavedReplyStore());

    public static SavedReplyStore Instance => LazyInstance.Value;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _storePath;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private List<SavedReply> _replies = [];
    private bool _isLoaded;

    /// <summary>Raised after any change, so open surfaces can re-render.</summary>
    public event EventHandler? Changed;

    private SavedReplyStore()
        : this(Path.Combine(ApplicationPaths.UserDataRoot, FileName))
    {
    }

    internal SavedReplyStore(string storePath) => _storePath = storePath;

    public IReadOnlyList<SavedReply> All => _replies;

    /// <summary>
    /// The replies to offer for a row, most-used first. Facet-specific ones come before the general ones,
    /// so the price answer is at the top of a pricing enquiry rather than three items down.
    /// </summary>
    public IReadOnlyList<SavedReply> ForFacet(QueueFacet facet) =>
        _replies
            .Where(r => r.Facets.Count == 0 || r.Facets.Contains(facet))
            .OrderByDescending(r => r.Facets.Contains(facet))
            .ThenByDescending(r => r.UseCount)
            .ThenBy(r => r.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_isLoaded)
            {
                return;
            }

            _isLoaded = true;

            if (!File.Exists(_storePath))
            {
                // Seeded, not empty. An empty library is a feature the owner has to build before it does
                // anything, and these four are the questions the data says they answer most.
                _replies = BuildDefaults();
                await SaveCoreAsync(cancellationToken).ConfigureAwait(false);
                return;
            }

            try
            {
                await using var stream = File.OpenRead(_storePath);
                _replies = await JsonSerializer
                    .DeserializeAsync<List<SavedReply>>(stream, JsonOptions, cancellationToken)
                    .ConfigureAwait(false) ?? [];
            }
            catch (Exception ex)
            {
                // A corrupt library must not cost the owner their whole set silently — the same mistake the
                // settings file made before F-DURA-01. Keep the bad file and start from the defaults.
                AppLogger.LogWarning("SavedReplies", $"Could not read the saved replies: {ex.Message}");
                TryBackupCorruptFile();
                _replies = BuildDefaults();
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task UpsertAsync(SavedReply reply, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reply);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var existing = _replies.FindIndex(r =>
                string.Equals(r.Id, reply.Id, StringComparison.OrdinalIgnoreCase));
            if (existing >= 0)
            {
                _replies[existing] = reply;
            }
            else
            {
                _replies.Add(reply);
            }

            await SaveCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _replies.RemoveAll(r => string.Equals(r.Id, id, StringComparison.OrdinalIgnoreCase));
            await SaveCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Records a use so the menu reorders toward what the owner actually reaches for.</summary>
    public void RecordUse(string id)
    {
        var reply = _replies.FirstOrDefault(r =>
            string.Equals(r.Id, id, StringComparison.OrdinalIgnoreCase));
        if (reply is null)
        {
            return;
        }

        reply.UseCount++;
        _ = PersistQuietlyAsync();
    }

    private async Task PersistQuietlyAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            await SaveCoreAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // A use count is not worth surfacing a failure for, but it IS worth a line — a silently
            // failing write is how a store quietly stops persisting.
            AppLogger.LogWarning("SavedReplies", $"Could not record a use count: {ex.Message}");
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task SaveCoreAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_storePath)!);

        // Write-then-move, so an interrupted save cannot leave a half-written library behind.
        var temp = _storePath + ".tmp";
        await using (var stream = File.Create(temp))
        {
            await JsonSerializer.SerializeAsync(stream, _replies, JsonOptions, cancellationToken)
                .ConfigureAwait(false);
        }

        File.Move(temp, _storePath, overwrite: true);
    }

    private void TryBackupCorruptFile()
    {
        try
        {
            File.Move(_storePath, _storePath + ".corrupt", overwrite: true);
        }
        catch (Exception ex)
        {
            AppLogger.LogWarning("SavedReplies", $"Could not set the corrupt library aside: {ex.Message}");
        }
    }

    /// <summary>
    /// The starting library. Written for a salon because that is the business this ships to, and phrased so
    /// the owner edits rather than starts from nothing — the bracketed parts are obvious prompts to fill in.
    /// </summary>
    internal static List<SavedReply> BuildDefaults() =>
    [
        new()
        {
            Title = "Price list",
            Body = "Hi {first}, thank you for getting in touch with {branch}.\n\n"
                 + "Our current rates:\n"
                 + "• [service] — Rs [price]\n"
                 + "• [service] — Rs [price]\n"
                 + "• [service] — Rs [price]\n\n"
                 + "Would you like me to book you in?",
            Facets = [QueueFacet.Enquiry]
        },
        new()
        {
            Title = "Opening times",
            Body = "Hi {first}, we are open [days] from [time] to [time] at {branch}.\n\n"
                 + "Walk-ins are welcome, though booking ahead means no waiting.",
            Facets = [QueueFacet.Enquiry]
        },
        new()
        {
            Title = "Address & directions",
            Body = "Hi {first}, you will find us at [address].\n\n"
                 + "Here is the location: [map link]\n\n"
                 + "Parking is [parking note]. See you soon!",
            Facets = [QueueFacet.Enquiry]
        },
        new()
        {
            Title = "Confirm a booking",
            Body = "Hi {first}, you are booked in at {branch} for [service] on [day] at [time].\n\n"
                 + "Please let us know if anything changes. Looking forward to seeing you.",
            Facets = [QueueFacet.Booking]
        },
        new()
        {
            Title = "Apology & make it right",
            Body = "Hi {first}, I am really sorry about this — that is not the standard we want at "
                 + "{branch}, and I would like to put it right.\n\n"
                 + "Could you tell me which day you visited and who looked after you? I will look into it "
                 + "today and come back to you personally.",
            Facets = [QueueFacet.AtRisk]
        },
        new()
        {
            Title = "Missed your call",
            Body = "Hi {first}, sorry we missed your call at {branch}. "
                 + "How can we help? You can reply here or call us back on [number].",
            Facets = [QueueFacet.MissedCall]
        },
        new()
        {
            Title = "Ask what the photo is for",
            Body = "Hi {first}, thanks for the photo! Is this the look you would like us to do?\n\n"
                 + "Let me know and I will tell you the price and how long it takes.",
            Facets = [QueueFacet.Media]
        },
        new()
        {
            Title = "Jobs — how to apply",
            Body = "Thank you for your interest in working with us. Please send your CV to [email] "
                 + "and our team will get back to you if there is a suitable opening.",
            Facets = [QueueFacet.JobApplicant]
        },
        new()
        {
            Title = "Holding reply",
            Body = "Hi {first}, thanks for your message — let me check and get straight back to you.",
            Facets = []
        }
    ];
}
