#pragma warning disable HEX1B_SIXEL // Testing experimental Sixel API

using Hex1b.Surfaces;
using Hex1b.Theming;
using Hex1b.Tokens;

namespace Hex1b.Tests;

[TestClass]
public class SixelSurfaceComparerTests
{
    [TestMethod]
    public void ToTokens_ReplacedSixel_ClearsOldPixelsBeforeEmittingReplacement()
    {
        var previous = CreateSixelSurface(
            CreateSolidPixels(20, 20, Rgba32.FromRgb(255, 0, 0)),
            0,
            0,
            2,
            1);
        var current = CreateSixelSurface(
            CreateSolidPixels(19, 20, Rgba32.FromRgb(0, 0, 255)),
            0,
            0,
            2,
            1);

        var tokens = SurfaceComparer.ToTokens(
            SurfaceComparer.Compare(previous, current),
            current,
            previous);
        var clearIndex = FindSpaceToken(tokens);
        var sixelIndex = tokens
            .Select((token, index) => (token, index))
            .First(item => item.token is UnrecognizedSequenceToken sequence &&
                           sequence.Sequence.StartsWith("\x1bP", StringComparison.Ordinal))
            .index;

        Assert.IsTrue(clearIndex >= 0 && clearIndex < sixelIndex, Describe(tokens));
    }

    [TestMethod]
    public void ToTokens_RemovedSixel_ClearsEntirePreviousRegion()
    {
        var previous = CreateSixelSurface(Rgba32.FromRgb(255, 0, 0), 1, 1, 3, 2);
        var current = new Surface(8, 4, CellMetrics.Default);

        var tokens = SurfaceComparer.ToTokens(
            SurfaceComparer.Compare(previous, current),
            current,
            previous);
        Assert.IsTrue(tokens.OfType<TextToken>().Sum(token => token.Text.Length) >= 6);
        Assert.IsFalse(tokens.OfType<UnrecognizedSequenceToken>().Any(
            token => token.Sequence.StartsWith("\x1bP", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void ToTokens_MovedSixel_ClearsOldRegionThenEmitsNewAnchor()
    {
        var previous = CreateSixelSurface(Rgba32.FromRgb(0, 200, 120), 0, 0, 2, 1);
        var current = CreateSixelSurface(Rgba32.FromRgb(0, 200, 120), 4, 2, 2, 1);

        var tokens = SurfaceComparer.ToTokens(
            SurfaceComparer.Compare(previous, current),
            current,
            previous);
        var clearIndex = FindSpaceToken(tokens);
        var newPositionIndex = tokens
            .Select((token, index) => (token, index))
            .First(item => item.token is CursorPositionToken { Row: 3, Column: 5 })
            .index;

        Assert.IsTrue(clearIndex >= 0 && clearIndex < newPositionIndex, Describe(tokens));
    }

    [TestMethod]
    public void ToTokens_TextOverAnchor_FragmentsImageAndRendersTextAfterward()
    {
        var previous = new Surface(6, 2, CellMetrics.Default);
        var current = new Surface(6, 2, CellMetrics.Default);
        var context = new SurfaceRenderContext(current);
        context.WriteSixel(CreateSolidPixels(20, 20, Rgba32.FromRgb(200, 80, 40)), 2, 1);
        context.SetCursorPosition(0, 0);
        context.Write("X");

        var tokens = SurfaceComparer.ToTokens(
            SurfaceComparer.Compare(previous, current),
            current,
            previous);

        var sixelIndex = tokens
            .Select((token, index) => (token, index))
            .First(item => item.token is UnrecognizedSequenceToken sequence &&
                           sequence.Sequence.StartsWith("\x1bP", StringComparison.Ordinal))
            .index;
        var textIndex = tokens
            .Select((token, index) => (token, index))
            .First(item => item.token is TextToken { Text: "X" })
            .index;

        Assert.IsTrue(sixelIndex < textIndex);
    }

    [TestMethod]
    public void ToTokens_SixelWrittenWithActiveBackground_EmitsImage()
    {
        var previous = new Surface(6, 2, CellMetrics.Default);
        var current = new Surface(6, 2, CellMetrics.Default);
        var context = new SurfaceRenderContext(current);
        context.Write("\x1b[44m");
        context.WriteSixel(CreateSolidPixels(20, 20, Rgba32.FromRgb(200, 80, 40)), 2, 1);

        var tokens = SurfaceComparer.ToTokens(
            SurfaceComparer.Compare(previous, current),
            current,
            previous);

        Assert.IsTrue(tokens.OfType<UnrecognizedSequenceToken>().Any(
            token => token.Sequence.StartsWith("\x1bP", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void ToTokens_SixelCompositedOverBackground_EmitsImage()
    {
        var previous = new Surface(6, 2, CellMetrics.Default);
        var current = new Surface(6, 2, CellMetrics.Default);
        current.Clear(new SurfaceCell(" ", null, Hex1bColor.Blue));

        var child = CreateSixelSurface(
            CreateSolidPixels(20, 20, Rgba32.FromRgb(200, 80, 40)),
            0,
            0,
            2,
            1);
        current.Composite(child, 0, 0);

        var tokens = SurfaceComparer.ToTokens(
            SurfaceComparer.Compare(previous, current),
            current,
            previous);

        Assert.IsTrue(tokens.OfType<UnrecognizedSequenceToken>().Any(
            token => token.Sequence.StartsWith("\x1bP", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void Compare_SamePayloadWithDifferentCellSpan_IsDifferent()
    {
        var pixels = CreateSolidPixels(20, 20, Rgba32.FromRgb(80, 160, 240));
        var previous = CreateSixelSurface(pixels, 0, 0, 2, 1);
        var current = CreateSixelSurface(pixels, 0, 0, 3, 2);

        var diff = SurfaceComparer.Compare(previous, current);

        Assert.IsFalse(diff.IsEmpty);
    }

    [TestMethod]
    public void Compare_RepeatedIdenticalSixelRender_HasNoChanges()
    {
        var pixels = CreateSolidPixels(20, 20, Rgba32.FromRgb(80, 160, 240));
        var previous = CreateSixelSurface(pixels, 0, 0, 2, 1);
        var current = CreateSixelSurface(pixels, 0, 0, 2, 1);

        Assert.IsTrue(SurfaceComparer.Compare(previous, current).IsEmpty);
    }

    private static Surface CreateSixelSurface(
        Rgba32 color,
        int x,
        int y,
        int width,
        int height)
        => CreateSixelSurface(CreateSolidPixels(width * 10, height * 20, color), x, y, width, height);

    private static Surface CreateSixelSurface(
        SixelPixelBuffer pixels,
        int x,
        int y,
        int width,
        int height)
    {
        var surface = new Surface(8, 4, CellMetrics.Default);
        var context = new SurfaceRenderContext(surface);
        context.SetCursorPosition(x, y);
        context.WriteSixel(pixels, width, height);
        return surface;
    }

    private static SixelPixelBuffer CreateSolidPixels(int width, int height, Rgba32 color)
    {
        var pixels = new SixelPixelBuffer(width, height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                pixels[x, y] = color;
            }
        }

        return pixels;
    }

    private static int FindSpaceToken(IReadOnlyList<AnsiToken> tokens)
        => tokens
            .Select((token, index) => (token, index))
            .Where(item => item.token is TextToken text &&
                           text.Text.Length > 0 &&
                           text.Text.All(static character => character == ' '))
            .Select(static item => item.index)
            .DefaultIfEmpty(-1)
            .First();

    private static string Describe(IReadOnlyList<AnsiToken> tokens)
        => string.Join(Environment.NewLine, tokens.Select((token, index) => $"{index}: {token}"));
}
