using System.Threading.Channels;

namespace UnifiedMessenger.Services;

internal static class ChannelWriteHelper
{
    public static bool TryWriteWithDropLog<T>(
        ChannelWriter<T> writer,
        T item,
        string channelName)
    {
        if (writer.TryWrite(item))
        {
            return true;
        }

        System.Diagnostics.Debug.WriteLine(
            $"[{channelName}] channel full — dropped oldest item to enqueue new work.");

        return writer.TryWrite(item);
    }
}
