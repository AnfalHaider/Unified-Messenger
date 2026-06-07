using System.Runtime.CompilerServices;
using System.Text;

namespace UnifiedMessenger.Services.Ollama;

internal static class OllamaJsonStreamReader
{
    public static async IAsyncEnumerable<T> ReadNdjsonAsync<T>(
        Stream stream,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);

        while (true)
        {
            string? line;
            try
            {
                line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }

            if (line is null)
            {
                yield break;
            }

            if (OllamaJson.TryDeserialize<T>(line.AsSpan(), out var item) && item is not null)
            {
                yield return item;
            }
        }
    }
}
