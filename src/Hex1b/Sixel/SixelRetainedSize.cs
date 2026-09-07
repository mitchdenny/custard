using System.Text;
using Hex1b.Surfaces;

namespace Hex1b.Sixel;

/// <summary>
/// Deterministic retained-byte accounting for one Sixel image resource.
/// </summary>
/// <remarks>
/// This intentionally counts retained protocol and pixel content rather than
/// runtime object headers or allocator overhead, whose sizes vary by runtime.
/// </remarks>
internal static class SixelRetainedSize
{
    private const int Int32Bytes = sizeof(int);
    private const int Int64Bytes = sizeof(long);
    private const int BooleanBytes = sizeof(byte);
    private const int NullableMarkerBytes = sizeof(byte);
    private const int Rgba32Bytes = 4;

    internal static long GetInitialBytes(SixelData image)
    {
        var total = 0L;
        Add(ref total, Encoding.UTF8.GetByteCount(image.Payload));
        Add(ref total, image.ContentHash.Length);
        Add(ref total, GetParseResultBytes(image.ParseResult));
        Add(ref total, GetRasterPreparationBytes(image.RasterPreparation));
        Add(ref total, 4L * Int32Bytes);
        Add(ref total, 2L * sizeof(double) + 2L * Int32Bytes);
        Add(ref total, BooleanBytes);
        return total;
    }

    internal static long GetRasterBytes(SixelRasterResult raster)
    {
        var total = 0L;
        Add(ref total, Int32Bytes);
        Add(ref total, 7L * 2 * Int32Bytes);
        Add(ref total, 4L * Int32Bytes);
        Add(ref total, Int32Bytes);
        Add(ref total, Rgba32Bytes);
        Add(ref total, raster.Identity.Length);
        Add(ref total, GetRasterDiagnosticsBytes(raster.Diagnostics));
        Add(ref total, raster.Image?.RetainedTileBytes ?? 0);
        return total;
    }

    internal static long GetDensePixelBytes(SixelPixelBuffer pixels) =>
        SaturatingMultiply((long)pixels.Width * pixels.Height, Rgba32Bytes);

    private static long GetParseResultBytes(SixelParseResult parse)
    {
        var total = 0L;
        Add(ref total, 6L * Int32Bytes);
        if (parse.RasterAttributes is not null)
            Add(ref total, 4L * Int32Bytes);
        Add(ref total, 4L * Int32Bytes);
        Add(ref total, 5L * 2 * Int32Bytes);
        Add(ref total, 2L * 4 * Int32Bytes);
        Add(ref total, Int32Bytes);
        Add(ref total, GetPaletteCommandsBytes(parse.PaletteMutations));
        Add(ref total, GetPaletteCommandsBytes(parse.FinalPaletteDefinitions));
        foreach (var command in parse.Commands)
        {
            Add(ref total, 5L * Int32Bytes + NullableMarkerBytes);
            if (command.Palette is { } palette)
                Add(ref total, GetPaletteCommandBytes(palette));
        }
        Add(ref total, BooleanBytes + Int32Bytes);
        Add(ref total, GetDiagnosticsBytes(parse.Diagnostics));
        return total;
    }

    private static long GetRasterPreparationBytes(SixelRasterPreparation? preparation)
    {
        if (preparation is null)
            return 0;

        var total = 0L;
        Add(ref total, preparation.Identity.Length);
        Add(ref total, Rgba32Bytes);
        Add(ref total, SaturatingMultiply(preparation.Environment.Registers.Count, Rgba32Bytes));
        return total;
    }

    private static long GetPaletteCommandsBytes(IReadOnlyList<SixelPaletteCommand> commands)
    {
        var total = 0L;
        foreach (var command in commands)
            Add(ref total, GetPaletteCommandBytes(command));
        return total;
    }

    private static long GetPaletteCommandBytes(SixelPaletteCommand command)
    {
        long total = Int32Bytes;
        Add(ref total, NullableMarkerBytes + (command.ColorSpace is null ? 0 : Int32Bytes));
        Add(ref total, GetNullableIntBytes(command.X));
        Add(ref total, GetNullableIntBytes(command.Y));
        Add(ref total, GetNullableIntBytes(command.Z));
        return total;
    }

    private static long GetNullableIntBytes(int? value) =>
        NullableMarkerBytes + (value is null ? 0 : Int32Bytes);

    private static long GetDiagnosticsBytes(IReadOnlyList<SixelDiagnostic> diagnostics)
    {
        var total = 0L;
        foreach (var diagnostic in diagnostics)
        {
            Add(ref total, Int32Bytes + Int64Bytes + NullableMarkerBytes);
            if (diagnostic.Command is not null)
                Add(ref total, sizeof(byte));
            Add(ref total, Encoding.UTF8.GetByteCount(diagnostic.Message));
        }
        return total;
    }

    private static long GetRasterDiagnosticsBytes(
        IReadOnlyList<SixelRasterDiagnostic> diagnostics)
    {
        var total = 0L;
        foreach (var diagnostic in diagnostics)
        {
            Add(ref total, Int32Bytes);
            Add(ref total, Encoding.UTF8.GetByteCount(diagnostic.Message));
        }
        return total;
    }

    private static long SaturatingMultiply(long left, long right)
    {
        if (left == 0 || right == 0)
            return 0;
        return left > long.MaxValue / right ? long.MaxValue : left * right;
    }

    private static void Add(ref long total, long value)
    {
        total = value > long.MaxValue - total ? long.MaxValue : total + value;
    }
}
