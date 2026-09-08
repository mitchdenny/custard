using System.Net.WebSockets;
using System.Text.Json;
using Hex1b;

namespace WebTerminalDemo;

internal sealed class BrowserSession(WebSocket socket, TerminalInstance instance, string? name, bool relay, ILogger logger)
{
    public async Task RunAsync(CancellationToken requestAborted)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(requestAborted, instance.Stopping);
        Hwt1PresentationAdapter? presentation = null;
        Hmp1BrowserView? relayView = null;
        Task[] tasks = [];
        var closeStatus = WebSocketCloseStatus.NormalClosure;
        var closeReason = "View closed";
        try
        {
            if (relay)
            {
                relayView = await Hmp1BrowserView.CreateAsync(instance.Presentation, name, cts.Token);
                presentation = relayView.Presentation;
            }
            else
                presentation = await instance.Presentation.CreateBrowserViewAsync(name, cts.Token);
            tasks = [SendFramesAsync(presentation, cts.Token), ReceiveAsync(presentation, cts.Token)];
            var finished = await Task.WhenAny(tasks);
            await finished;
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested) { }
        catch (WebSocketException ex)
        {
            logger.LogInformation("Browser view of {Instance} disconnected: {Reason}", instance.Id, ex.Message);
        }
        catch (TimeoutException ex)
        {
            logger.LogWarning(ex, "Browser view of {Instance} stopped acknowledging frames", instance.Id);
            closeStatus = WebSocketCloseStatus.PolicyViolation;
            closeReason = "Frame acknowledgement timed out";
        }
        catch (Exception ex) when (ex is InvalidDataException or JsonException or InvalidOperationException or
            KeyNotFoundException or FormatException or ArgumentException)
        {
            logger.LogWarning(ex, "Invalid browser input or state for {Instance}", instance.Id);
            closeStatus = WebSocketCloseStatus.PolicyViolation;
            closeReason = "Invalid input or terminal state; see server log";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Browser view of {Instance} failed", instance.Id);
            closeStatus = WebSocketCloseStatus.InternalServerError;
            closeReason = "Terminal view failed; see server log";
        }
        finally
        {
            await cts.CancelAsync();
            foreach (var task in tasks)
                await ObserveAsync(task);
            if (presentation is not null)
            {
                try
                {
                    if (relayView is not null)
                        await relayView.DisposeAsync();
                    else
                        await presentation.DisposeAsync();
                }
                catch (Exception ex) { logger.LogDebug(ex, "HMP browser peer disposal failed for {Instance}", instance.Id); }
            }
            if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                using var closeTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                try { await socket.CloseOutputAsync(closeStatus, closeReason, closeTimeout.Token); }
                catch (Exception ex) when (ex is WebSocketException or OperationCanceledException or ObjectDisposedException)
                {
                    logger.LogDebug(ex, "Browser close handshake failed for {Instance}", instance.Id);
                    socket.Abort();
                }
            }
        }
    }

    private async Task ObserveAsync(Task task)
    {
        try { await task; }
        catch (OperationCanceledException) { }
        catch (Exception ex) { logger.LogDebug(ex, "Browser task ended for {Instance}", instance.Id); }
    }

    private async Task SendFramesAsync(Hwt1PresentationAdapter presentation, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var frame = await presentation.ReadFrameAsync(ct);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromMinutes(2));
            try { await socket.SendAsync(frame, WebSocketMessageType.Binary, true, timeout.Token); }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                throw new TimeoutException("Browser did not receive a frame within two minutes.");
            }
        }
    }

    private async Task ReceiveAsync(Hwt1PresentationAdapter presentation, CancellationToken ct)
    {
        var buffer = new byte[64 * 1024];
        while (!ct.IsCancellationRequested)
        {
            var length = 0;
            ValueWebSocketReceiveResult result;
            do
            {
                if (length == buffer.Length)
                    throw new InvalidDataException("Input exceeds 64 KiB.");
                result = await socket.ReceiveAsync(buffer.AsMemory(length), ct);
                if (result.MessageType == WebSocketMessageType.Close)
                    return;
                if (result.MessageType != WebSocketMessageType.Text)
                    throw new InvalidDataException("Expected a JSON command.");
                length += result.Count;
            } while (!result.EndOfMessage);

            await presentation.HandleMessageAsync(buffer.AsMemory(0, length), ct);
        }
    }
}
