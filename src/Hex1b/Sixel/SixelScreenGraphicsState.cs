using Hex1b.Sixel;
using Hex1b.Surfaces;

namespace Hex1b;

/// <summary>
/// One screen's (main or alternate) independent Sixel graphics state: its
/// image store, its live placements, and (main screen only) the placements
/// that have scrolled into scrollback history, partitioned by scrollback row
/// identity.
/// </summary>
internal sealed class SixelScreenGraphicsState
{
    private sealed class RetainedResourceOwner(
        SixelScreenGraphicsState screen,
        object syncRoot,
        Func<SixelScreenGraphicsState, SixelData, long, bool> tryReserve)
        : ISixelRetainedResourceOwner
    {
        public SixelRasterResult GetRaster(SixelData image)
        {
            lock (syncRoot)
            {
                if (!screen.Images.Contains(image))
                {
                    image.DetachRetainedResourceOwner(this);
                    return image.GetRasterUnowned();
                }
                return image.GetRasterOwned(bytes => tryReserve(screen, image, bytes));
            }
        }

        public SixelPixelBuffer? GetPixels(SixelData image)
        {
            lock (syncRoot)
            {
                if (!screen.Images.Contains(image))
                {
                    image.DetachRetainedResourceOwner(this);
                    return image.GetPixelsUnowned();
                }
                return image.GetPixelsOwned(bytes => tryReserve(screen, image, bytes));
            }
        }
    }

    internal SixelScreenGraphicsState(
        object syncRoot,
        Func<SixelScreenGraphicsState, SixelData, long, bool> tryReserve)
    {
        Images = new SixelImageStore(new RetainedResourceOwner(this, syncRoot, tryReserve));
    }

    internal SixelImageStore Images { get; }

    internal List<SixelPlacement> Placements { get; } = [];

    /// <summary>
    /// Placements that scrolled into history, keyed by the stable scrollback
    /// row identity (<see cref="ScrollbackEntry.RowId"/>) they are anchored
    /// to. Only ever populated for the main screen; the alternate screen has
    /// no history partition.
    /// </summary>
    internal Dictionary<long, List<SixelHistoryPlacement>> HistoryPlacements { get; } = [];

    internal void Clear()
    {
        Placements.Clear();
        HistoryPlacements.Clear();
        Images.Clear();
    }

    /// <summary>
    /// Recomputes which images are still reachable from this screen's live
    /// placements and history placements, sweeping everything else from
    /// <see cref="Images"/>.
    /// </summary>
    internal int ReconcileImages()
    {
        var retained = new HashSet<byte[]>(SixelContentHashComparer.Instance);
        foreach (var placement in Placements)
            retained.Add(placement.Image.ContentHash);
        foreach (var list in HistoryPlacements.Values)
        {
            foreach (var historyPlacement in list)
                retained.Add(historyPlacement.Placement.Image.ContentHash);
        }

        return Images.RemoveUnreferenced(retained);
    }
}
