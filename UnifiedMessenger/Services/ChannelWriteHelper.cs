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

        if (reader.TryRead(out _))
        {
            System.Diagnostics.Debug.WriteLine(
                $"[{channelName}] channel full — dropped oldest item to enqueue new work.");
            return writer.TryWrite(item);
        }

        System.Diagnostics.Debug.WriteLine($"[{channelName}] channel full — enqueue rejected.");
        return false;
    }
}
