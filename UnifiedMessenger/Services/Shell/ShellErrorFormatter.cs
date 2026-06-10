namespace UnifiedMessenger.Services.Shell;

internal static class ShellErrorFormatter
{
    public static string Format(Exception ex)
    {
        if (ex is AggregateException aggregate)
        {
            var parts = aggregate.Flatten().InnerExceptions
                .Select(static inner => inner.Message?.Trim())
                .Where(static part => !string.IsNullOrWhiteSpace(part))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (parts.Count > 0)
            {
                return string.Join(Environment.NewLine, parts);
            }
        }

        var message = ex.Message?.Trim();
        if (ex.InnerException is { } inner)
        {
            var innerMessage = inner.Message?.Trim();
            if (!string.IsNullOrWhiteSpace(innerMessage) &&
                !string.Equals(message, innerMessage, StringComparison.Ordinal))
            {
                return $"{message}{Environment.NewLine}{innerMessage}";
            }
        }

        return string.IsNullOrWhiteSpace(message) ? ex.GetType().Name : message;
    }
}
