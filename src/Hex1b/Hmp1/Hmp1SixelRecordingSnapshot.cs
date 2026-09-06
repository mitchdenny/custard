using System.Text;

namespace Hex1b;

/// <summary>
/// A decoded, versioned Sixel state recording: sufficient to reconstruct image
/// definitions, placements, damage, source crops, geometry-only outcomes, and
/// metrics without a live upstream terminal.
/// </summary>
internal sealed class Hmp1SixelRecordingSnapshot(
    int version,
    IReadOnlyList<Hmp1SixelRecordedImage> images,
    IReadOnlyList<Hmp1SixelRecordedPlacement> placements)
{
    /// <summary>The format version this recording was written with.</summary>
    public int Version { get; } = version;

    /// <summary>The recording's distinct image table, ordered by first appearance.</summary>
    public IReadOnlyList<Hmp1SixelRecordedImage> Images { get; } = images;

    /// <summary>The recording's placements, in their original creation order.</summary>
    public IReadOnlyList<Hmp1SixelRecordedPlacement> Placements { get; } = placements;

    /// <summary>
    /// Builds the cursor-position + Sixel DCS escape sequence text needed to
    /// reconstruct every placement in this recording on a fresh terminal, in
    /// <see cref="Hmp1SixelRecordedPlacement.Sequence"/> order. Feeding the result
    /// through the same tokenizer/apply path a live terminal uses for incoming
    /// output (rather than a bespoke reconstruction) is what lets replay be
    /// verified against the same authoritative parser/raster invariants used by
    /// live terminal processing.
    /// </summary>
    /// <param name="cancellationToken">Stops validation or construction before completion.</param>
    /// <exception cref="Hmp1SixelRecordingException">
    /// The expanded replay would exceed the internal aggregate replay limit.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was cancelled.
    /// </exception>
    public string BuildReplayEscapeSequence(CancellationToken cancellationToken = default)
    {
        var orderedPlacements = Placements.OrderBy(p => p.Sequence).ToArray();
        long totalBytes = 0;
        foreach (var placement in orderedPlacements)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var image = Images[placement.ImageIndex];
            var cursor = FormattableString.Invariant(
                $"\x1b[{placement.Row + 1};{placement.Column + 1}H");
            var payloadBytes = Encoding.UTF8.GetByteCount(image.Payload);
            if (image.IsGeometryOnly && !Hmp1SixelStateReplay.HasDcsFraming(image.Payload))
                payloadBytes = checked(payloadBytes + 4);

            totalBytes = checked(totalBytes + Encoding.UTF8.GetByteCount(cursor) + payloadBytes);
            if (totalBytes > Hmp1SixelLimits.MaximumTotalPayloadBytes)
            {
                throw new Hmp1SixelRecordingException(
                    Hmp1SixelRecordingFailureReason.ResourceLimitExceeded,
                    $"Expanded replay payload exceeds the limit of {Hmp1SixelLimits.MaximumTotalPayloadBytes} bytes.");
            }
        }

        var sb = new StringBuilder((int)totalBytes);
        foreach (var placement in orderedPlacements)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var image = Images[placement.ImageIndex];
            sb.Append(FormattableString.Invariant(
                $"\x1b[{placement.Row + 1};{placement.Column + 1}H"));
            sb.Append(image.IsGeometryOnly
                ? Hmp1SixelStateReplay.FramePayload(image.Payload)
                : image.Payload);
        }

        return sb.ToString();
    }
}
