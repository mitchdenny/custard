using System.Net;
using WebTerminalDemo;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 16 * 1024);
if (builder.Configuration["urls"] is null)
    builder.WebHost.UseUrls("http://localhost:5290");
var app = builder.Build();
await using var terminals = new TerminalRegistry(app.Logger, app.Lifetime.ApplicationStopping);

// This demo can launch a local shell. Reject remote clients, DNS rebinding, and cross-origin mutations.
app.Use(async (context, next) =>
{
    var host = context.Request.Host.Host.Trim('[', ']');
    if (context.Connection.RemoteIpAddress is not { } address || !IPAddress.IsLoopback(address) ||
        !(host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
          IPAddress.TryParse(host, out var hostAddress) && IPAddress.IsLoopback(hostAddress)))
    {
        await Results.Json(new { error = "Only loopback connections are allowed." }, statusCode: 403).ExecuteAsync(context);
        return;
    }
    context.Response.Headers.XContentTypeOptions = "nosniff";
    if (context.Request.Path.StartsWithSegments("/api"))
        context.Response.Headers.CacheControl = "no-store";
    if ((context.Request.Path == "/ws" || !HttpMethods.IsGet(context.Request.Method) &&
         !HttpMethods.IsHead(context.Request.Method)) && !IsSameOrigin(context.Request))
    {
        await Results.Json(new { error = "A matching same-origin Origin header is required." }, statusCode: 403).ExecuteAsync(context);
        return;
    }
    await next(context);
});
app.UseWebSockets();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapGet("/health", () => Results.Ok(new { status = "ready", renderer = "WebGPU spike" }));
app.MapGet("/api/terminals", () => Results.Ok(terminals.List()));
app.MapPost("/api/terminals", (CreateTerminalRequest request) =>
{
    if (request.Scene is not ("mixed" or "text" or "sixel" or "kgp" or "animation" or "shell"))
        return Results.BadRequest(new { error = "scene must be mixed, text, sixel, kgp, animation, or shell." });
    if (request.Columns is < 20 or > 300 || request.Rows is < 10 or > 100)
        return Results.BadRequest(new { error = "columns must be 20..300 and rows must be 10..100." });
    if (!IsValidName(request.Name))
        return Results.BadRequest(new { error = "name must contain 1..80 printable characters when supplied." });
    try
    {
        var instance = terminals.Create(request);
        return instance is null
            ? Results.Json(new { error = "At most four terminal instances can run at once." }, statusCode: 429)
            : Results.Created($"/api/terminals/{instance.Id}", instance);
    }
    catch (ObjectDisposedException)
    {
        return Results.Json(new { error = "The application is shutting down." }, statusCode: 503);
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Could not create terminal instance");
        return Results.Problem("Could not start the terminal workload; see server log.", statusCode: 500);
    }
});
app.MapDelete("/api/terminals/{id}", async (string id) =>
    await terminals.DeleteAsync(id)
        ? Results.NoContent()
        : Results.NotFound(new { error = "Terminal instance not found." }));
app.MapPost("/api/terminals/{id}/controls", (string id, TerminalControlsRequest request) =>
{
    if (request.Rate is < 1 or > 120 || request.Batch is < 1 or > 1000)
        return Results.BadRequest(new { error = "rate must be 1..120 and batch must be 1..1000." });
    return terminals.UpdateControls(id, request) switch
    {
        404 => Results.NotFound(new { error = "Terminal instance not found." }),
        409 => Results.Conflict(new { error = "Controls are only available for generated workloads, not shells." }),
        _ => Results.NoContent()
    };
});
app.MapGet("/ws", async (HttpContext context) =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        await Results.BadRequest(new { error = "A WebSocket upgrade is required." }).ExecuteAsync(context);
        return;
    }
    var id = context.Request.Query["instance"].ToString();
    var name = context.Request.Query["name"].ToString();
    if (string.IsNullOrWhiteSpace(id) || name.Length > 0 && !IsValidName(name))
    {
        await Results.BadRequest(new { error = "instance is required; name may contain 1..80 printable characters." }).ExecuteAsync(context);
        return;
    }
    var (view, status) = terminals.TryOpenView(id);
    if (view is null)
    {
        await Results.Json(new { error = status == 429 ? "At most eight browser views can connect at once." :
            "Terminal instance not found or no longer running." }, statusCode: status).ExecuteAsync(context);
        return;
    }
    using (view)
    using (var socket = await context.WebSockets.AcceptWebSocketAsync())
        await new BrowserSession(socket, view.Instance, string.IsNullOrEmpty(name) ? null : name, app.Logger)
            .RunAsync(context.RequestAborted);
});
await app.RunAsync();

static bool IsSameOrigin(HttpRequest request) =>
    Uri.TryCreate(request.Headers.Origin.ToString(), UriKind.Absolute, out var origin) &&
    origin.Authority.Equals(request.Host.Value, StringComparison.OrdinalIgnoreCase) &&
    origin.Scheme.Equals(request.Scheme, StringComparison.OrdinalIgnoreCase) &&
    origin.AbsolutePath == "/" && origin.Query.Length == 0 && origin.Fragment.Length == 0 &&
    origin.UserInfo.Length == 0;

static bool IsValidName(string? name) =>
    name is null || !string.IsNullOrWhiteSpace(name) && name.Length <= 80 && !name.Any(char.IsControl);
