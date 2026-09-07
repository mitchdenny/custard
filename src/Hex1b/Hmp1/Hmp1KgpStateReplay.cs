using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Hex1b;

internal static class Hmp1KgpStateReplay
{
    private const int MaximumBase64ChunkLength = 4096;
    private const int MaximumRawChunkLength = MaximumBase64ChunkLength / 4 * 3;
    private const int TargetFrameSize = 1024 * 1024;

    internal static async Task WriteAsync(
        Stream stream,
        IReadOnlyList<KgpPlacement> placements,
        IReadOnlyDictionary<uint, KgpImageData> images,
        int cursorX,
        int cursorY,
        CancellationToken ct,
        DateTimeOffset? animationTimestamp = null)
    {
        if (placements.Count == 0 || images.Count == 0)
            return;

        var frame = new StringBuilder(TargetFrameSize);
        var animations = new List<KgpAnimationPlaybackSnapshot>();

        foreach (var image in images.Values.OrderBy(image => image.ImageId))
        {
            await AppendPixelsAsync(image, image.Data, image.Format, animationGap: null).ConfigureAwait(false);
            if (image.AnimationState is not { } animation)
                continue;

            // Frames are already fully composed in the store. Overwrite preserves
            // their RGBA bytes, including RGB channels beneath transparent pixels.
            foreach (var animationFrame in animation.Frames.Skip(1))
                await AppendPixelsAsync(image, animationFrame.Data, animationFrame.Format,
                    animationFrame.GapMilliseconds).ConfigureAwait(false);

            var rootGap = animation.GetFrame(0).GapMilliseconds;
            var remainingLoops = animation.MaximumLoops > 1
                ? animation.MaximumLoops - animation.CompletedLoops : 1;
            await AppendAsync(BuildKgpSequence(FormattableString.Invariant(
                $"a=a,{BuildImageIdentity(image)},r=1,z={(rootGap == 0 ? -1 : rootGap)},c={image.CurrentFrameNumber},s=1,v={remainingLoops},q=2"),
                string.Empty)).ConfigureAwait(false);
            animations.Add(new(
                image.ImageNumber == 0 ? image.ImageId : 0,
                image.ImageNumber,
                image.CurrentFrameNumber,
                animation.PlaybackState,
                animation.MaximumLoops,
                animation.CompletedLoops,
                animation.CurrentFrameShownAt is { } shownAt && animationTimestamp is { } capturedAt
                    ? Math.Max(0, (capturedAt - shownAt).Ticks) : null));
        }

        var placementIdentityCounts = placements
            .Where(placement => placement.PlacementId > 0)
            .GroupBy(placement => (placement.ImageId, placement.PlacementId))
            .ToDictionary(group => group.Key, group => group.Count());

        foreach (var placement in placements)
        {
            if (!images.TryGetValue(placement.ImageId, out var image))
                continue;

            var preservePlacementId =
                placement.PlacementId > 0 &&
                placementIdentityCounts[(placement.ImageId, placement.PlacementId)] == 1;
            await AppendAsync(BuildPlacementSequence(
                placement,
                image,
                preservePlacementId)).ConfigureAwait(false);
        }

        await AppendAsync(FormattableString.Invariant(
            $"\x1b[{cursorY + 1};{cursorX + 1}H")).ConfigureAwait(false);
        foreach (var animation in animations)
        {
            var identity = animation.ImageNumber > 0
                ? FormattableString.Invariant($"I={animation.ImageNumber}")
                : FormattableString.Invariant($"i={animation.ImageId}");
            // A plain ANSI consumer cannot restore completed-loop counters.
            // Loading plays the last remaining pass without wrapping; Hex1b
            // consumers subsequently restore the exact state from the checkpoint.
            var playbackState = animation.PlaybackState == KgpParsedCommand.AnimationPlaybackState.Running &&
                animation.MaximumLoops > 1 && animation.CompletedLoops == animation.MaximumLoops - 1
                    ? KgpParsedCommand.AnimationPlaybackState.Loading : animation.PlaybackState;
            await AppendAsync(BuildKgpSequence(FormattableString.Invariant(
                $"a=a,{identity},s={(int)playbackState},q=2"), string.Empty)).ConfigureAwait(false);
        }
        await FlushAsync().ConfigureAwait(false);
        if (animations.Count > 0)
        {
            await Hmp1Protocol.WriteFrameAsync(stream, Hmp1FrameType.KgpAnimationState,
                JsonSerializer.SerializeToUtf8Bytes(new Hmp1KgpAnimationState(animations),
                    Hmp1JsonContext.Default.Hmp1KgpAnimationState), ct).ConfigureAwait(false);
        }

        async ValueTask AppendPixelsAsync(
            KgpImageData image, byte[] data, KgpFormat format, int? animationGap)
        {
            var action = animationGap.HasValue ? 'f' : 't';
            var offset = 0;
            var first = true;
            do
            {
                var count = Math.Min(MaximumRawChunkLength, data.Length - offset);
                var isLast = offset + count >= data.Length;
                var payload = count == 0
                    ? string.Empty
                    : Convert.ToBase64String(data, offset, count);
                string controls;
                if (first)
                {
                    controls = FormattableString.Invariant(
                        $"a={action},f={(int)format},s={image.Width},v={image.Height},{BuildImageIdentity(image)},t=d,q=2");
                    if (animationGap is { } gap)
                        controls += FormattableString.Invariant($",X=1,z={(gap == 0 ? -1 : gap)}");
                    if (!isLast)
                        controls += ",m=1";
                }
                else
                {
                    controls = (animationGap.HasValue ? "a=f," : string.Empty) +
                        FormattableString.Invariant($"m={(isLast ? 0 : 1)},q=2");
                }

                await AppendAsync(BuildKgpSequence(controls, payload)).ConfigureAwait(false);
                offset += count;
                first = false;
            }
            while (offset < data.Length);
        }

        async ValueTask AppendAsync(string sequence)
        {
            if (frame.Length > 0 && frame.Length + sequence.Length > TargetFrameSize)
                await FlushAsync().ConfigureAwait(false);
            frame.Append(sequence);
        }

        async ValueTask FlushAsync()
        {
            if (frame.Length == 0)
                return;

            var payload = Encoding.UTF8.GetBytes(frame.ToString());
            frame.Clear();
            await Hmp1Protocol.WriteFrameAsync(
                stream,
                Hmp1FrameType.Output,
                payload,
                ct).ConfigureAwait(false);
        }
    }

    private static string BuildPlacementSequence(
        KgpPlacement placement,
        KgpImageData image,
        bool preservePlacementId)
    {
        var controls = new StringBuilder();
        controls.Append("a=p,");
        controls.Append(BuildImageIdentity(image));
        if (preservePlacementId)
        {
            controls.Append(",p=");
            controls.Append(placement.PlacementId.ToString(CultureInfo.InvariantCulture));
        }

        AppendNonZero(controls, 'x', placement.SourceX);
        AppendNonZero(controls, 'y', placement.SourceY);
        AppendNonZero(controls, 'w', placement.SourceWidth);
        AppendNonZero(controls, 'h', placement.SourceHeight);
        AppendNonZero(controls, 'X', placement.CellOffsetX);
        AppendNonZero(controls, 'Y', placement.CellOffsetY);
        if (!placement.UsesNativeSize)
        {
            AppendNonZero(controls, 'c', placement.DisplayColumns);
            AppendNonZero(controls, 'r', placement.DisplayRows);
        }
        if (placement.ZIndex != 0)
        {
            controls.Append(",z=");
            controls.Append(placement.ZIndex.ToString(CultureInfo.InvariantCulture));
        }
        controls.Append(",C=1,q=2");

        return FormattableString.Invariant(
            $"\x1b[{placement.Row + 1};{placement.Column + 1}H") +
            BuildKgpSequence(controls.ToString(), string.Empty);
    }

    private static string BuildImageIdentity(KgpImageData image)
        => image.ImageNumber > 0
            ? FormattableString.Invariant($"I={image.ImageNumber}")
            : FormattableString.Invariant($"i={image.ImageId}");

    private static string BuildKgpSequence(string controls, string payload)
        => payload.Length == 0
            ? $"\x1b_G{controls}\x1b\\"
            : $"\x1b_G{controls};{payload}\x1b\\";

    private static void AppendNonZero(StringBuilder builder, char key, uint value)
    {
        if (value == 0)
            return;

        builder.Append(',');
        builder.Append(key);
        builder.Append('=');
        builder.Append(value.ToString(CultureInfo.InvariantCulture));
    }
}
