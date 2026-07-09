using System.Collections.ObjectModel;

namespace UnifiedMessenger.Services;

internal static class ObservableCollectionSyncHelper
{
    public static void Sync<T>(
        ObservableCollection<T> target,
        IReadOnlyList<T> source,
        Func<T, string> keySelector,
        Func<T, T, bool> contentEquals)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(keySelector);
        ArgumentNullException.ThrowIfNull(contentEquals);

        for (var index = source.Count; index < target.Count; index++)
        {
            target.RemoveAt(target.Count - 1);
        }

        for (var index = 0; index < source.Count; index++)
        {
            var incoming = source[index];
            if (index >= target.Count)
            {
                target.Add(incoming);
                continue;
            }

            var existing = target[index];
            if (keySelector(existing).Equals(keySelector(incoming), StringComparison.Ordinal) &&
                contentEquals(existing, incoming))
            {
                continue;
            }

            target[index] = incoming;
        }
    }
}
