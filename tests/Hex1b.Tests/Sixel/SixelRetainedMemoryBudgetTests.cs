using System.Text;
using System.Runtime.CompilerServices;
using Hex1b.Reflow;
using Hex1b.Sixel;
using Hex1b.Tokens;

namespace Hex1b.Tests.Sixel;

[TestClass]
public class SixelRetainedMemoryBudgetTests
{
    [TestMethod]
    public async Task InitialImage_ExactBudgetIsRetained_BoundaryMinusOneIsRejected()
    {
        var frame = Frame("#1;2;100;0;0#1@");
        var requiredBytes = await MeasureInitialBytesAsync(frame);

        await using var exact = SixelTestTerminal.Create(
            graphics: GraphicsWithBudget(requiredBytes));
        await FeedAndWaitAsync(exact, frame, expectedPlacements: 1);
        Assert.AreEqual(requiredBytes, exact.Terminal.SixelRetainedByteCount);

        await using var below = SixelTestTerminal.Create(
            graphics: GraphicsWithBudget(requiredBytes - 1));
        await FeedAndWaitAsync(below, frame, expectedPlacements: 0);
        Assert.AreEqual(0, below.Terminal.TrackedSixelCount);
        Assert.AreEqual(0L, below.Terminal.SixelRetainedByteCount);
    }

    [TestMethod]
    public async Task ZeroBudget_RejectsSixelWithoutDisturbingTerminalProgress()
    {
        await using var terminal = SixelTestTerminal.Create(
            graphics: GraphicsWithBudget(0));

        await terminal.FeedAsync(
            Frame("#1;2;100;0;0#1@").Concat("Z"u8.ToArray()).ToArray(),
            cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsText("Z"),
            "terminal progressed after rejected Sixel",
            TestContext.Current.CancellationToken);

        Assert.AreEqual(0, terminal.Terminal.SixelPlacementCount);
        Assert.AreEqual(0, terminal.Terminal.TrackedSixelCount);
        Assert.AreEqual(0L, terminal.Terminal.SixelRetainedByteCount);
    }

    [TestMethod]
    public async Task ManyTinyHighPayloadImages_EvictOldestResourcesDeterministically()
    {
        var frames = Enumerable.Range(1, 8)
            .Select(register => Frame(
                $"#{register};2;{register};{register};{register}" +
                string.Concat(Enumerable.Repeat($"#{register}", 1024)) +
                "@"))
            .ToArray();
        var oneImageBytes = await MeasureInitialBytesAsync(frames[0]);
        var graphics = GraphicsWithBudget(checked(oneImageBytes * 2));
        graphics.MaximumImagesPerScreen = 16;
        graphics.MaximumPlacementsPerScreen = 16;
        await using var terminal = SixelTestTerminal.Create(height: 12, graphics: graphics);

        var stream = new List<byte>();
        for (var index = 0; index < frames.Length; index++)
        {
            stream.AddRange(Encoding.ASCII.GetBytes($"\x1b[{index + 1};1H"));
            stream.AddRange(frames[index]);
        }
        stream.Add((byte)'Z');

        await terminal.FeedAsync(
            stream.ToArray(),
            cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsText("Z"),
            "high-payload image sequence",
            TestContext.Current.CancellationToken);

        Assert.AreEqual(2, terminal.Terminal.TrackedSixelCount);
        Assert.IsLessThanOrEqualTo(
            graphics.MaximumRetainedBytesPerScreen,
            terminal.Terminal.SixelRetainedByteCount);
        Assert.IsTrue(terminal.Terminal.SixelPlacements.Any(
            placement => placement.Image.Payload.Contains("#7;2;7;7;7", StringComparison.Ordinal)));
        Assert.IsTrue(terminal.Terminal.SixelPlacements.Any(
            placement => placement.Image.Payload.Contains("#8;2;8;8;8", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task DeduplicatedPlacements_CountImageBytesOnce()
    {
        var frame = Frame("#1;2;100;0;0#1@");
        await using var terminal = SixelTestTerminal.Create();

        await FeedAndWaitAsync(terminal, frame, expectedPlacements: 1);
        var retainedBytes = terminal.Terminal.SixelRetainedByteCount;

        await terminal.FeedAsync(
            Encoding.ASCII.GetBytes("\x1b[2;1H")
                .Concat(frame)
                .ToArray(),
            cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            _ => terminal.Terminal.SixelPlacementCount == 2,
            "deduplicated placement",
            TestContext.Current.CancellationToken);

        Assert.AreEqual(1, terminal.Terminal.TrackedSixelCount);
        Assert.AreEqual(retainedBytes, terminal.Terminal.SixelRetainedByteCount);
        Assert.AreSame(
            terminal.Terminal.SixelPlacements[0].Image,
            terminal.Terminal.SixelPlacements[1].Image);
    }

    [TestMethod]
    public async Task SparseRasterTiles_AreAddedToRetainedByteAccounting()
    {
        var frame = Frame(
            "\"1;1;8193;6#1;2;100;0;0#1@!4095?@!4095?@");
        await using var terminal = SixelTestTerminal.Create(
            width: 20,
            graphics: GraphicsWithBudget(8L * 1024 * 1024));
        await FeedAndWaitAsync(terminal, frame, expectedPlacements: 1);

        var image = TestSeq.Single(terminal.Terminal.SixelPlacements).Image;
        var before = terminal.Terminal.SixelRetainedByteCount;

        Assert.AreEqual(SixelRasterStatus.Rasterized, image.RasterStatus);

        var after = terminal.Terminal.SixelRetainedByteCount;
        Assert.AreEqual(image.RetainedByteCount, after);
        Assert.IsGreaterThanOrEqualTo(3L * ((64 * 64 * 4) + sizeof(long)), after - before);
    }

    [TestMethod]
    public async Task DenseMaterialization_WhenBudgetIsFull_DoesNotEvictOlderImage()
    {
        var first = Frame("#1;2;100;0;0#1!8~");
        var second = Frame("#2;2;0;100;0#2!8~");
        var firstSizes = await MeasureStagesAsync(first);
        var secondSizes = await MeasureStagesAsync(second);
        var budget = checked(firstSizes.Initial + secondSizes.Rasterized);
        Assert.IsTrue(secondSizes.Dense > secondSizes.Rasterized);
        Assert.IsTrue(
            checked(firstSizes.Initial + secondSizes.Dense) > budget);

        await using var terminal = SixelTestTerminal.Create(
            graphics: GraphicsWithBudget(budget));
        await terminal.FeedAsync(
            first.Concat(Encoding.ASCII.GetBytes("\x1b[2;1H"))
                .Concat(second)
                .ToArray(),
            cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            _ => terminal.Terminal.SixelPlacementCount == 2,
            "two images before dense materialization",
            TestContext.Current.CancellationToken);

        var newest = terminal.Terminal.SixelPlacements.MaxBy(placement => placement.Sequence)!;
        Assert.AreEqual(SixelRasterStatus.Rasterized, newest.Image.RasterStatus);
        Assert.AreEqual(2, terminal.Terminal.TrackedSixelCount);
        var before = terminal.Terminal.SixelRetainedByteCount;

        Assert.IsNotNull(newest.Image.GetPixels());

        Assert.IsFalse(newest.Image.HasMaterializedPixels);
        Assert.AreEqual(2, terminal.Terminal.TrackedSixelCount);
        Assert.AreEqual(2, terminal.Terminal.SixelPlacementCount);
        Assert.AreEqual(before, terminal.Terminal.SixelRetainedByteCount);
        Assert.IsLessThanOrEqualTo(budget, terminal.Terminal.SixelRetainedByteCount);
    }

    [TestMethod]
    public async Task DenseCacheThatCannotFit_IsReturnedUncachedWithoutCorruptingState()
    {
        var frame = Frame("#1;2;100;0;0#1!8~");
        var sizes = await MeasureStagesAsync(frame);
        await using var terminal = SixelTestTerminal.Create(
            graphics: GraphicsWithBudget(sizes.Rasterized));
        await FeedAndWaitAsync(terminal, frame, expectedPlacements: 1);

        var image = TestSeq.Single(terminal.Terminal.SixelPlacements).Image;
        Assert.AreEqual(SixelRasterStatus.Rasterized, image.RasterStatus);
        var before = terminal.Terminal.SixelRetainedByteCount;

        Assert.IsNotNull(image.GetPixels());

        Assert.IsFalse(image.HasMaterializedPixels);
        Assert.AreEqual(before, terminal.Terminal.SixelRetainedByteCount);
        Assert.AreEqual(1, terminal.Terminal.SixelPlacementCount);
    }

    [TestMethod]
    public async Task MainAndAlternateScreens_HaveIndependentBudgetsAndCleanup()
    {
        var mainFrame = Frame("#1;2;100;0;0#1@");
        var alternateFrame = Frame("#2;2;0;100;0#2A");
        var budget = Math.Max(
            await MeasureInitialBytesAsync(mainFrame),
            await MeasureInitialBytesAsync(alternateFrame));
        await using var terminal = SixelTestTerminal.Create(
            graphics: GraphicsWithBudget(budget));

        await FeedAndWaitAsync(terminal, mainFrame, expectedPlacements: 1);
        var mainBytes = terminal.Terminal.SixelRetainedByteCount;

        await terminal.FeedAsync(
            Encoding.ASCII.GetBytes("\x1b[?1049h")
                .Concat(alternateFrame)
                .ToArray(),
            cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.InAlternateScreen &&
                terminal.Terminal.SixelPlacementCount == 1,
            "alternate image",
            TestContext.Current.CancellationToken);
        Assert.IsGreaterThan(0L, terminal.Terminal.SixelRetainedByteCount);

        await terminal.FeedAsync(
            Encoding.ASCII.GetBytes("\x1b[?1049l"),
            cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => !snapshot.InAlternateScreen,
            "main image restored",
            TestContext.Current.CancellationToken);
        Assert.AreEqual(mainBytes, terminal.Terminal.SixelRetainedByteCount);

        await terminal.FeedAsync(
            Encoding.ASCII.GetBytes("\x1b[?1049h"),
            cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.InAlternateScreen,
            "fresh alternate screen",
            TestContext.Current.CancellationToken);
        Assert.AreEqual(0L, terminal.Terminal.SixelRetainedByteCount);
    }

    [TestMethod]
    public async Task ResetAndDisposal_ClearRetainedByteAccounting()
    {
        var frame = Frame("#1;2;100;0;0#1@");
        var terminal = SixelTestTerminal.Create();
        try
        {
            await FeedAndWaitAsync(terminal, frame, expectedPlacements: 1);
            Assert.IsGreaterThan(0L, terminal.Terminal.SixelRetainedByteCount);

            await terminal.FeedPreTokenizedAsync(
                Encoding.ASCII.GetBytes("\x1bc"),
                [RisToken.Instance],
                TestContext.Current.CancellationToken);
            await terminal.WaitForAsync(
                _ => terminal.Terminal.SixelRetainedByteCount == 0,
                "retained bytes cleared by RIS",
                TestContext.Current.CancellationToken);
            Assert.AreEqual(0L, terminal.Terminal.SixelRetainedByteCount);

            await FeedAndWaitAsync(terminal, frame, expectedPlacements: 1);
        }
        finally
        {
            await terminal.DisposeAsync();
        }

        Assert.AreEqual(0L, terminal.Terminal.SixelRetainedByteCount);
    }

    [TestMethod]
    public async Task SnapshotAfterLiveEviction_CanMaterializeOutsideLiveBudget()
    {
        var frame = Frame("#1;2;100;0;0#1!8~");
        var sizes = await MeasureStagesAsync(frame);
        await using var terminal = SixelTestTerminal.Create(
            graphics: GraphicsWithBudget(sizes.Rasterized));
        await FeedAndWaitAsync(terminal, frame, expectedPlacements: 1);

        using var snapshot = terminal.Terminal.CreateSnapshot();
        var image = TestSeq.Single(snapshot.SixelImages.Values);

        await terminal.FeedAsync(
            Encoding.ASCII.GetBytes("\x1b[2J"),
            cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            _ => terminal.Terminal.SixelPlacementCount == 0,
            "live image evicted",
            TestContext.Current.CancellationToken);

        Assert.AreEqual(0L, terminal.Terminal.SixelRetainedByteCount);
        Assert.IsNotNull(image.GetPixels());
        Assert.IsTrue(image.HasMaterializedPixels);
        Assert.AreEqual(0L, terminal.Terminal.SixelRetainedByteCount);
    }

    [TestMethod]
    public async Task SnapshotExportsAndHmp1Replay_DoNotEvictLivePlacements()
    {
        var first = Frame("#1;2;100;0;0#1!8~");
        var second = Frame("#2;2;0;100;0#2!8~");
        var firstSizes = await MeasureStagesAsync(first);
        var secondSizes = await MeasureStagesAsync(second);
        var budget = checked(firstSizes.Initial + secondSizes.Initial);
        await using var terminal = SixelTestTerminal.Create(
            graphics: GraphicsWithBudget(budget));
        await terminal.FeedAsync(
            first.Concat(Encoding.ASCII.GetBytes("\x1b[2;1H"))
                .Concat(second)
                .ToArray(),
            cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            _ => terminal.Terminal.SixelPlacementCount == 2,
            "two images before read-only materialization",
            TestContext.Current.CancellationToken);
        var before = terminal.Terminal.SixelRetainedByteCount;

        using var snapshot = terminal.Terminal.CreateSnapshot();
        _ = snapshot.ToSvg();
        _ = snapshot.ToHtml();
        foreach (var placement in snapshot.SixelPlacements)
            _ = Hmp1SixelStateReplay.BuildPlacementSequence(placement);

        Assert.AreEqual(2, terminal.Terminal.SixelPlacementCount);
        Assert.AreEqual(2, terminal.Terminal.TrackedSixelCount);
        Assert.AreEqual(before, terminal.Terminal.SixelRetainedByteCount);
    }

    [TestMethod]
    public async Task MixedProtocols_ShareOneScreenBudgetWithoutCrossProtocolEviction()
    {
        var frame = Frame("#1;2;100;0;0#1@");
        var sixelBytes = await MeasureInitialBytesAsync(frame);
        var graphics = GraphicsWithBudget(sixelBytes);
        await using var kgpFirst = SixelTestTerminal.Create(
            supportsKgp: true,
            graphics: graphics);

        kgpFirst.Terminal.ApplyTokens(AnsiTokenizer.Tokenize(
            KgpTestHelper.BuildTransmitCommand(
                imageId: 1,
                width: 1,
                height: 1,
                quiet: 2)));
        Assert.AreEqual(4L, kgpFirst.Terminal.KgpImageStore.TotalSize);
        await FeedAndWaitAsync(kgpFirst, frame, expectedPlacements: 0);
        Assert.AreEqual(4L, kgpFirst.Terminal.GraphicsRetainedByteCount);
        Assert.IsNotNull(kgpFirst.Terminal.KgpImageStore.GetImageById(1));

        kgpFirst.Terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[?1049h"));
        Assert.AreEqual(0L, kgpFirst.Terminal.GraphicsRetainedByteCount);
        await FeedAndWaitAsync(kgpFirst, frame, expectedPlacements: 1);
        Assert.AreEqual(sixelBytes, kgpFirst.Terminal.GraphicsRetainedByteCount);
        kgpFirst.Terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[?1049l"));
        Assert.AreEqual(4L, kgpFirst.Terminal.GraphicsRetainedByteCount);
        Assert.IsNotNull(kgpFirst.Terminal.KgpImageStore.GetImageById(1));

        await using var sixelFirst = SixelTestTerminal.Create(
            supportsKgp: true,
            graphics: graphics);
        await FeedAndWaitAsync(sixelFirst, frame, expectedPlacements: 1);
        sixelFirst.Terminal.ApplyTokens(AnsiTokenizer.Tokenize(
            KgpTestHelper.BuildTransmitCommand(
                imageId: 1,
                width: 1,
                height: 1,
                quiet: 2)));

        Assert.IsNull(sixelFirst.Terminal.KgpImageStore.GetImageById(1));
        Assert.AreEqual(1, sixelFirst.Terminal.SixelPlacementCount);
        Assert.AreEqual(sixelBytes, sixelFirst.Terminal.GraphicsRetainedByteCount);
    }

    [TestMethod]
    public async Task PayloadAndCommandMetadata_AreCountedAsRetainedManagedContent()
    {
        var body = "#1;2;100;0;0" + string.Concat(Enumerable.Repeat("#1@", 256));
        var frame = Frame(body);
        await using var terminal = SixelTestTerminal.Create();
        await FeedAndWaitAsync(terminal, frame, expectedPlacements: 1);
        var image = TestSeq.Single(terminal.Terminal.SixelPlacements).Image;
        var minimumPayloadAndCommands =
            ((long)image.Payload.Length * sizeof(char)) +
            ((long)image.ParseResult.Commands.Count * Unsafe.SizeOf<SixelCommand>());

        Assert.IsGreaterThanOrEqualTo(
            minimumPayloadAndCommands,
            terminal.Terminal.SixelRetainedByteCount);
    }

    [TestMethod]
    public async Task DamageHistoryPruningAndReflow_KeepAccountingReachabilityExact()
    {
        var frame = Frame("#1;2;100;0;0#1!2~");
        await using var damageTerminal = SixelTestTerminal.Create();
        await FeedAndWaitAsync(damageTerminal, frame, expectedPlacements: 1);
        var beforeDamage = damageTerminal.Terminal.SixelRetainedByteCount;
        await damageTerminal.FeedAsync(
            Encoding.ASCII.GetBytes("\x1b[1;1HX"),
            cancellationToken: TestContext.Current.CancellationToken);
        await damageTerminal.WaitForAsync(
            snapshot => snapshot.ContainsText("X"),
            "placement partially damaged",
            TestContext.Current.CancellationToken);
        Assert.AreEqual(beforeDamage, damageTerminal.Terminal.SixelRetainedByteCount);
        Assert.AreEqual(1, damageTerminal.Terminal.SixelPlacementCount);

        await damageTerminal.FeedAsync(
            Encoding.ASCII.GetBytes("\x1b[2J"),
            cancellationToken: TestContext.Current.CancellationToken);
        await damageTerminal.WaitForAsync(
            _ => damageTerminal.Terminal.SixelPlacementCount == 0,
            "damaged image cleared",
            TestContext.Current.CancellationToken);
        Assert.AreEqual(0L, damageTerminal.Terminal.SixelRetainedByteCount);

        await using var historyTerminal = SixelTestTerminal.Create(
            width: 4,
            height: 2,
            scrollbackCapacity: 4);
        await FeedAndWaitAsync(historyTerminal, frame, expectedPlacements: 1);
        await historyTerminal.FeedAsync(
            Encoding.ASCII.GetBytes("\x1b[2;1H\n"),
            cancellationToken: TestContext.Current.CancellationToken);
        await historyTerminal.WaitForAsync(
            _ => historyTerminal.Terminal.SixelHistoryPlacementCount > 0,
            "image moved into history",
            TestContext.Current.CancellationToken);
        Assert.IsGreaterThan(0L, historyTerminal.Terminal.SixelRetainedByteCount);

        await historyTerminal.FeedAsync(
            Encoding.ASCII.GetBytes("\x1b[3J"),
            cancellationToken: TestContext.Current.CancellationToken);
        await historyTerminal.WaitForAsync(
            _ => historyTerminal.Terminal.SixelHistoryPlacementCount == 0,
            "history image cleared",
            TestContext.Current.CancellationToken);
        Assert.AreEqual(0L, historyTerminal.Terminal.SixelRetainedByteCount);

        await using var reflowTerminal = SixelTestTerminal.Create(
            width: 4,
            height: 3,
            scrollbackCapacity: 4,
            reflow: KittyReflowStrategy.Instance);
        await FeedAndWaitAsync(reflowTerminal, frame, expectedPlacements: 1);
        var beforeReflow = reflowTerminal.Terminal.SixelRetainedByteCount;
        using var snapshot = reflowTerminal.Terminal.CreateSnapshot();
        Assert.AreEqual(beforeReflow, reflowTerminal.Terminal.SixelRetainedByteCount);

        reflowTerminal.Terminal.Resize(8, 3);
        Assert.AreEqual(beforeReflow, reflowTerminal.Terminal.SixelRetainedByteCount);
        AssertAccountingMatchesSnapshot(reflowTerminal.Terminal);
    }

    private static Hex1bTerminalGraphicsOptions GraphicsWithBudget(long budget) => new()
    {
        MaximumRetainedBytesPerScreen = budget,
    };

    private static byte[] Frame(string body) =>
        Encoding.ASCII.GetBytes($"\x1bPq{body}\x1b\\");

    private static async Task FeedAndWaitAsync(
        SixelTestTerminal terminal,
        byte[] frame,
        int expectedPlacements)
    {
        await terminal.FeedAsync(
            frame.Concat("Z"u8.ToArray()).ToArray(),
            cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsText("Z") &&
                terminal.Terminal.SixelPlacementCount == expectedPlacements,
            $"{expectedPlacements} retained placements",
            TestContext.Current.CancellationToken);
    }

    private static async Task<long> MeasureInitialBytesAsync(byte[] frame)
    {
        await using var terminal = SixelTestTerminal.Create();
        await FeedAndWaitAsync(terminal, frame, expectedPlacements: 1);
        return terminal.Terminal.SixelRetainedByteCount;
    }

    private static async Task<(long Initial, long Rasterized, long Dense)> MeasureStagesAsync(
        byte[] frame)
    {
        await using var terminal = SixelTestTerminal.Create();
        await FeedAndWaitAsync(terminal, frame, expectedPlacements: 1);
        var image = TestSeq.Single(terminal.Terminal.SixelPlacements).Image;
        var initial = terminal.Terminal.SixelRetainedByteCount;
        _ = image.RasterStatus;
        var rasterized = terminal.Terminal.SixelRetainedByteCount;
        Assert.IsNotNull(image.GetPixels());
        var dense = terminal.Terminal.SixelRetainedByteCount;
        return (initial, rasterized, dense);
    }

    private static void AssertAccountingMatchesSnapshot(Hex1bTerminal terminal)
    {
        using var snapshot = terminal.CreateSnapshot(scrollbackLines: terminal.ScrollbackCount);
        var retainedBytes = snapshot.SixelImages.Values.Sum(image => image.RetainedByteCount);
        Assert.AreEqual(retainedBytes, terminal.SixelRetainedByteCount);
    }
}
