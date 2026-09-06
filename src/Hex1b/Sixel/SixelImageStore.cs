using Hex1b.Sixel;

namespace Hex1b;

/// <summary>
/// Identity-keyed store of anonymous Sixel image resources for a single screen
/// (main or alternate).
/// </summary>
/// <remarks>
/// Unlike <see cref="TrackedObjectStore"/>'s manually reference-counted
/// tracked objects, this store follows <see cref="KgpImageStore"/>'s
/// reachability model: an image is retained only while at least one live
/// placement or history placement in the owning screen still references it.
/// <see cref="RemoveUnreferenced"/> recomputes that reachable set and sweeps
/// everything else after any placement mutation. Snapshots are unaffected by
/// this sweep because they hold their own direct <see cref="SixelData"/>
/// references (kept alive by ordinary .NET garbage collection), decoupled
/// from this store's dictionary.
/// </remarks>
internal sealed class SixelImageStore
{
    private readonly Dictionary<byte[], SixelData> _byHash = new(SixelContentHashComparer.Instance);

    /// <summary>Number of distinct images currently retained.</summary>
    internal int Count => _byHash.Count;

    /// <summary>The images currently retained.</summary>
    internal IReadOnlyCollection<SixelData> Images => _byHash.Values;

    /// <summary>
    /// Aggregate logical pixel area of the distinct retained images.
    /// </summary>
    internal long GetBoundedLogicalPixelCount(long maximumPerImage)
    {
        long total = 0;
        foreach (var image in _byHash.Values)
        {
            var extent = image.ParseResult.UnscaledLogicalCanvasExtent;
            var pixels = Math.Min((long)extent.Width * extent.Height, maximumPerImage);
            total = pixels > long.MaxValue - total ? long.MaxValue : total + pixels;
        }

        return total;
    }

    /// <summary>
    /// Gets the existing image for this exact resource identity (payload,
    /// captured raster state, protocol metrics, and cell span), or creates and
    /// stores a new one.
    /// </summary>
    internal SixelData GetOrCreate(
        string payload,
        byte[] sourceContentHash,
        int widthInCells,
        int heightInCells,
        SixelParseResult parseResult,
        SixelRasterPreparation rasterPreparation,
        SixelCellMetrics cellMetrics,
        bool payloadComplete,
        out bool created)
    {
        var hash = SixelData.ComputeHash(
            sourceContentHash,
            rasterPreparation.Identity,
            widthInCells,
            heightInCells,
            cellMetrics);
        if (_byHash.TryGetValue(hash, out var existing))
        {
            created = false;
            return existing;
        }

        var image = new SixelData(
            payload,
            widthInCells,
            heightInCells,
            hash,
            parseResult.DeclaredExtent.Width,
            parseResult.DeclaredExtent.Height,
            parseResult,
            rasterPreparation: rasterPreparation,
            cellMetrics: cellMetrics,
            payloadComplete: payloadComplete);
        _byHash[hash] = image;
        created = true;
        return image;
    }

    internal void Clear() => _byHash.Clear();

    /// <summary>
    /// Removes every image whose content hash is not present in
    /// <paramref name="retainedHashes"/> (mark-and-sweep reachability, the
    /// same strategy <see cref="KgpImageStore.RemoveUnreferencedImages"/> uses).
    /// </summary>
    internal int RemoveUnreferenced(HashSet<byte[]> retainedHashes)
    {
        if (_byHash.Count == 0)
            return 0;

        List<byte[]>? toRemove = null;
        foreach (var hash in _byHash.Keys)
        {
            if (!retainedHashes.Contains(hash))
                (toRemove ??= []).Add(hash);
        }

        if (toRemove is null)
            return 0;

        foreach (var hash in toRemove)
            _byHash.Remove(hash);

        return toRemove.Count;
    }
}
