using System.Threading.Channels;

namespace UnifiedMessenger.Services;

internal static class ChannelWriteHelper
{
    public static bool TryWriteWithDropOldest<T>(
        ChannelReader<T> reader,
        ChannelWriter<T> writer,
        T item,
        string channelName)
    {
        if (writer.TryWrite(item))
        {
            return true;
        }

        // Both branches mean work was thrown away. Throttled per channel because a saturated channel
        // saturates by definition — one line is a hiccup, "and 900 more since" is the finding.
        if (reader.TryRead(out _))
        {
            AppLogger.LogWarningThrottled(
                "Channel",
                $"'{channelName}' is full; dropped the oldest item to make room.",
                $"channel.dropoldest.{channelName}");
            return writer.TryWrite(item);
        }

        AppLogger.LogWarningThrottled(
            "Channel",
            $"'{channelName}' is full; rejected new work.",
            $"channel.reject.{channelName}");
        return false;
    }
}
