using System.Text;
using System.Text.Json;
using Hex1b.Automation;
using Hex1b.Tokens;
using Microsoft.Extensions.Time.Testing;

namespace Hex1b.Tests.Hmp1;

[TestClass]
public class Hmp1KgpStateReplayTests
{
    [TestMethod]
    public async Task WriteAsync_ComposedAnimation_PreservesAllFramesGapsAndStoppedCurrentFrame()
    {
        var time = new FakeTimeProvider();
        using var producerWorkload = new Hex1bAppWorkloadAdapter();
        using var producer = CreateTerminal(producerWorkload, time);
        var root = Enumerable.Range(0, 40 * 30).SelectMany(_ => new byte[] { 80, 40, 20 }).ToArray();
        producer.ApplyTokens(AnsiTokenizer.Tokenize(
            "\x1b[3;5H" +
            KgpTestHelper.BuildCommand("a=T,f=24,s=40,v=30,i=7,p=11,X=9,Y=19,C=1,q=2", root) +
            KgpTestHelper.BuildCommand("a=f,f=32,s=1,v=1,i=7,c=1,x=2,y=3,z=30,q=2", [200, 100, 50, 128]) +
            KgpTestHelper.BuildCommand("a=f,f=32,s=1,v=1,i=7,x=1,y=1,X=1,z=-1,q=2", [17, 34, 51, 0]) +
            KgpTestHelper.BuildCommand("a=f,f=32,s=40,v=30,i=7,X=1,z=45,q=2", KgpTestHelper.CreatePixelData(40, 30)) +
            KgpTestHelper.BuildCommand("a=a,i=7,r=1,z=25,c=3,s=1,v=3,q=2")));
        using var source = producer.CreateSnapshot();
        using var viewerWorkload = new Hex1bAppWorkloadAdapter();
        using var viewer = CreateTerminal(viewerWorkload, time);

        await ReplayAsync(source, viewer);

        using var replay = viewer.CreateSnapshot();
        var expected = source.KgpImages[7];
        var actual = replay.KgpImages[7];
        Assert.AreEqual(4, actual.FrameCount);
        Assert.AreEqual(KgpFormat.Rgba32, actual.Format);
        Assert.AreEqual(3, actual.CurrentFrameNumber);
        Assert.AreEqual(KgpParsedCommand.AnimationPlaybackState.Stopped, actual.AnimationState!.PlaybackState);
        Assert.AreEqual(3u, actual.AnimationState.MaximumLoops);
        for (var frame = 0; frame < expected.FrameCount; frame++)
        {
            TestSeq.AreEqual(expected.AnimationFrames![frame].Data, actual.AnimationFrames![frame].Data);
            Assert.AreEqual(expected.AnimationFrames[frame].GapMilliseconds, actual.AnimationFrames[frame].GapMilliseconds);
        }
        TestSeq.AreEqual(new byte[] { 17, 34, 51, 0 }, actual.CurrentFrameData.AsSpan((40 + 1) * 4, 4).ToArray());
        Assert.IsTrue(TestSeq.Single(replay.KgpPlacements).UsesNativeSize);
        Assert.AreEqual(source.CursorX, replay.CursorX);
        Assert.AreEqual(source.CursorY, replay.CursorY);
        time.Advance(TimeSpan.FromSeconds(10));
        Assert.AreEqual(3, viewer.KgpImageStore.GetImageById(7)!.CurrentFrameNumber);
    }

    [TestMethod]
    [DataRow(55, false)]
    [DataRow(70, false)]
    [DataRow(500, false)]
    [DataRow(55, true)]
    public async Task WriteAsync_FiniteAnimation_PreservesLoopProgressAndRemainingGap(int elapsedMilliseconds, bool numbered)
    {
        var producerTime = new FakeTimeProvider();
        var viewerTime = new FakeTimeProvider();
        using var producerWorkload = new Hex1bAppWorkloadAdapter();
        using var producer = CreateTerminal(producerWorkload, producerTime);
        // Force numbered images to receive a different allocated ID on replay.
        producer.ApplyTokens(AnsiTokenizer.Tokenize(
            KgpTestHelper.BuildCommand("a=t,f=32,s=1,v=1,i=123,q=2", [0, 0, 0, 0])));
        var identity = numbered ? "I=42" : "i=7";
        producer.ApplyTokens(AnsiTokenizer.Tokenize(
            KgpTestHelper.BuildCommand($"a=T,f=32,s=1,v=1,{identity},C=1,q=2", [1, 0, 0, 255]) +
            KgpTestHelper.BuildCommand($"a=f,f=32,s=1,v=1,{identity},z=30,q=2", [2, 0, 0, 255]) +
            KgpTestHelper.BuildCommand($"a=a,{identity},r=1,z=20,s=3,v=2,q=2")));
        for (var elapsed = 0; elapsed < elapsedMilliseconds; elapsed++)
            producerTime.Advance(TimeSpan.FromMilliseconds(1));
        using var source = producer.CreateSnapshot();
        Assert.AreEqual(1u, TestSeq.Single(source.KgpImages.Values).AnimationState!.CompletedLoops);
        using var viewerWorkload = new Hex1bAppWorkloadAdapter();
        using var viewer = CreateTerminal(viewerWorkload, viewerTime);

        await ReplayAsync(source, viewer);

        foreach (var advance in new[] { 0, 14, 1, 30, 500 })
        {
            producerTime.Advance(TimeSpan.FromMilliseconds(advance));
            viewerTime.Advance(TimeSpan.FromMilliseconds(advance));
            using var expectedSnapshot = producer.CreateSnapshot();
            using var actualSnapshot = viewer.CreateSnapshot();
            var expected = TestSeq.Single(expectedSnapshot.KgpImages.Values);
            var actual = TestSeq.Single(actualSnapshot.KgpImages.Values);
            Assert.AreEqual(2, actual.FrameCount);
            Assert.AreEqual(expected.CurrentFrameNumber, actual.CurrentFrameNumber,
                $"Advance {advance}; source shown {expected.AnimationState!.CurrentFrameShownAt:O}, now {producerTime.GetUtcNow():O}; " +
                $"replay shown {actual.AnimationState!.CurrentFrameShownAt:O}, now {viewerTime.GetUtcNow():O}, " +
                $"gaps {string.Join(',', actual.AnimationFrames!.Select(frame => frame.GapMilliseconds))}");
            TestSeq.AreEqual(expected.CurrentFrameData, actual.CurrentFrameData);
            Assert.AreEqual(expected.AnimationState!.PlaybackState, actual.AnimationState!.PlaybackState);
            Assert.AreEqual(expected.AnimationState.MaximumLoops, actual.AnimationState.MaximumLoops);
            Assert.AreEqual(expected.AnimationState.CompletedLoops, actual.AnimationState.CompletedLoops);
        }
        using var parked = viewer.CreateSnapshot();
        Assert.AreEqual(2, TestSeq.Single(parked.KgpImages.Values).CurrentFrameNumber);
    }

    [TestMethod]
    public async Task WriteAsync_LoadingTail_PreservesElapsedTimeAndResumesWhenFrameAppended()
    {
        var producerTime = new FakeTimeProvider();
        var viewerTime = new FakeTimeProvider();
        using var producerWorkload = new Hex1bAppWorkloadAdapter();
        using var producer = CreateTerminal(producerWorkload, producerTime);
        producer.ApplyTokens(AnsiTokenizer.Tokenize(
            KgpTestHelper.BuildCommand("a=T,f=32,s=1,v=1,i=7,C=1,q=2", [1, 0, 0, 255]) +
            KgpTestHelper.BuildCommand("a=f,f=32,s=1,v=1,i=7,z=30,q=2", [2, 0, 0, 255]) +
            KgpTestHelper.BuildCommand("a=a,i=7,r=1,z=20,s=2,q=2")));
        producerTime.Advance(TimeSpan.FromMilliseconds(20));
        producerTime.Advance(TimeSpan.FromMilliseconds(80));
        using var source = producer.CreateSnapshot();
        using var viewerWorkload = new Hex1bAppWorkloadAdapter();
        using var viewer = CreateTerminal(viewerWorkload, viewerTime);

        await ReplayAsync(source, viewer);

        Assert.AreEqual(2, viewer.KgpImageStore.GetImageById(7)!.CurrentFrameNumber);
        var append = AnsiTokenizer.Tokenize(
            KgpTestHelper.BuildCommand("a=f,f=32,s=1,v=1,i=7,z=50,q=2", [3, 0, 0, 255]));
        producer.ApplyTokens(append);
        viewer.ApplyTokens(append);
        Assert.AreEqual(3, producer.KgpImageStore.GetImageById(7)!.CurrentFrameNumber);
        Assert.AreEqual(3, viewer.KgpImageStore.GetImageById(7)!.CurrentFrameNumber);
        TestSeq.AreEqual(new byte[] { 3, 0, 0, 255 }, viewer.KgpImageStore.GetImageById(7)!.CurrentFrameData);
        viewerTime.Advance(TimeSpan.FromSeconds(10));
        Assert.AreEqual(3, viewer.KgpImageStore.GetImageById(7)!.CurrentFrameNumber);
    }

    [TestMethod]
    public async Task WriteAsync_PlainAnsiConsumer_FiniteFinalPassDoesNotRestart()
    {
        var producerTime = new FakeTimeProvider();
        var viewerTime = new FakeTimeProvider();
        using var producerWorkload = new Hex1bAppWorkloadAdapter();
        using var producer = CreateTerminal(producerWorkload, producerTime);
        producer.ApplyTokens(AnsiTokenizer.Tokenize(
            KgpTestHelper.BuildCommand("a=T,f=32,s=1,v=1,i=7,C=1,q=2", [1, 0, 0, 255]) +
            KgpTestHelper.BuildCommand("a=f,f=32,s=1,v=1,i=7,z=30,q=2", [2, 0, 0, 255]) +
            KgpTestHelper.BuildCommand("a=a,i=7,r=1,z=20,s=3,v=2,q=2")));
        for (var elapsed = 0; elapsed < 500; elapsed++)
            producerTime.Advance(TimeSpan.FromMilliseconds(1));
        using var source = producer.CreateSnapshot();
        using var viewerWorkload = new Hex1bAppWorkloadAdapter();
        using var viewer = CreateTerminal(viewerWorkload, viewerTime);

        await ReplayAsync(source, viewer, applyCheckpoint: false);

        viewerTime.Advance(TimeSpan.FromSeconds(10));
        Assert.AreEqual(2, viewer.KgpImageStore.GetImageById(7)!.CurrentFrameNumber);
        TestSeq.AreEqual(new byte[] { 2, 0, 0, 255 }, viewer.KgpImageStore.GetImageById(7)!.CurrentFrameData);
    }

    private static Hex1bTerminal CreateTerminal(Hex1bAppWorkloadAdapter workload, TimeProvider time)
        => Hex1bTerminal.CreateBuilder().WithWorkload(workload)
            .WithHeadless(new TerminalCapabilities { SupportsKgp = true, CellPixelWidth = 10, CellPixelHeight = 20 })
            .WithDimensions(20, 10).WithTimeProvider(time).Build();

    private static async Task ReplayAsync(Hex1bTerminalSnapshot source, Hex1bTerminal viewer, bool applyCheckpoint = true)
    {
        using var stream = new MemoryStream();
        await Hmp1KgpStateReplay.WriteAsync(stream, source.KgpPlacements, source.KgpImages,
            source.CursorX, source.CursorY, TestContext.Current.CancellationToken, source.KgpAnimationTimestamp);
        stream.Position = 0;
        while (stream.Position < stream.Length)
        {
            var frame = await Hmp1Protocol.ReadFrameAsync(stream, TestContext.Current.CancellationToken)
                ?? throw new AssertFailedException("Expected a KGP replay frame.");
            if (frame.Type == Hmp1FrameType.Output)
                viewer.ApplyTokens(AnsiTokenizer.Tokenize(Encoding.UTF8.GetString(frame.Payload.Span)));
            else
            {
                Assert.AreEqual(Hmp1FrameType.KgpAnimationState, frame.Type);
                if (applyCheckpoint)
                    viewer.ApplyHmp1KgpAnimationState(JsonSerializer.Deserialize(
                        frame.Payload.Span, Hmp1JsonContext.Default.Hmp1KgpAnimationState)!);
            }
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task WriteAsync_NativeSprite_PreservesPixelSizeOffsetsAndCrop(bool crop)
    {
        var capabilities = new TerminalCapabilities
        {
            SupportsKgp = true,
            CellPixelWidth = 10,
            CellPixelHeight = 20
        };
        using var producerWorkload = new Hex1bAppWorkloadAdapter();
        using var producer = Hex1bTerminal.CreateBuilder()
            .WithWorkload(producerWorkload)
            .WithHeadless(capabilities)
            .WithDimensions(20, 10)
            .Build();
        var pixels = KgpTestHelper.CreatePixelData(3, 3);
        var cropControls = crop ? ",x=1,y=1,w=2,h=2" : "";
        producer.ApplyTokens(AnsiTokenizer.Tokenize(
            "\x1b[3;5H" + KgpTestHelper.BuildCommand(
                $"a=T,f=32,s=3,v=3,i=7,p=11,X=9,Y=19,C=1,q=2{cropControls}",
                pixels)));
        using var source = producer.CreateSnapshot();
        using var stream = new MemoryStream();

        await Hmp1KgpStateReplay.WriteAsync(
            stream, source.KgpPlacements, source.KgpImages,
            source.CursorX, source.CursorY, TestContext.Current.CancellationToken);

        using var viewerWorkload = new Hex1bAppWorkloadAdapter();
        using var viewer = Hex1bTerminal.CreateBuilder()
            .WithWorkload(viewerWorkload)
            .WithHeadless(capabilities)
            .WithDimensions(20, 10)
            .Build();
        stream.Position = 0;
        while (stream.Position < stream.Length)
        {
            var frame = await Hmp1Protocol.ReadFrameAsync(
                stream, TestContext.Current.CancellationToken)
                ?? throw new AssertFailedException("Expected a KGP replay frame.");
            Assert.AreEqual(Hmp1FrameType.Output, frame.Type);
            var payload = Encoding.UTF8.GetString(frame.Payload.Span);
            Assert.DoesNotContain(",c=", payload);
            Assert.DoesNotContain(",r=", payload);
            viewer.ApplyTokens(AnsiTokenizer.Tokenize(payload));
        }

        using var replay = viewer.CreateSnapshot();
        var placement = TestSeq.Single(replay.KgpPlacements);
        Assert.IsTrue(placement.UsesNativeSize);
        Assert.AreEqual(7u, placement.ImageId);
        Assert.AreEqual(11u, placement.PlacementId);
        Assert.AreEqual(2, placement.Row);
        Assert.AreEqual(4, placement.Column);
        Assert.AreEqual(2u, placement.DisplayColumns);
        Assert.AreEqual(2u, placement.DisplayRows);
        Assert.AreEqual(9u, placement.CellOffsetX);
        Assert.AreEqual(19u, placement.CellOffsetY);
        Assert.AreEqual(crop ? 1u : 0u, placement.SourceX);
        Assert.AreEqual(crop ? 1u : 0u, placement.SourceY);
        Assert.AreEqual(crop ? 2u : 3u, placement.SourceWidth);
        Assert.AreEqual(crop ? 2u : 3u, placement.SourceHeight);
        TestSeq.AreEqual(pixels, replay.KgpImages[7].Data);
        Assert.AreEqual(source.CursorX, replay.CursorX);
        Assert.AreEqual(source.CursorY, replay.CursorY);
    }
}
