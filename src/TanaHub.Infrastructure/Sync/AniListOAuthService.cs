using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Net.Http.Headers;
using TanaHub.Application.Common;
using TanaHub.Application.Services;

namespace TanaHub.Infrastructure.Sync;

public sealed class AniListOAuthService : IAniListAuthService
{
    private const string AuthBaseUrl = "https://anilist.co/api/v2/oauth/authorize";
    private const string TokenUrl = "https://anilist.co/api/v2/oauth/token";
    private const int CallbackPort = 7575;
    private static readonly string RedirectUri = $"http://localhost:{CallbackPort}/callback/";

    private readonly HttpClient httpClient;

    public AniListOAuthService(HttpClient httpClient) => this.httpClient = httpClient;

    public async Task<Result<AniListAuthResult>> AuthorizeAsync(
        string clientId,
        string clientSecret,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            return Result<AniListAuthResult>.Failure(ApplicationError.Validation("Client ID and Client Secret are required."));

        var authUrl = $"{AuthBaseUrl}?client_id={Uri.EscapeDataString(clientId)}" +
                      $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}&response_type=code";

        using var listener = new HttpListener();
        listener.Prefixes.Add(RedirectUri);
        try
        {
            listener.Start();
        }
        catch (Exception ex)
        {
            return Result<AniListAuthResult>.Failure(ApplicationError.Validation($"Cannot start local listener: {ex.Message}"));
        }

        Process.Start(new ProcessStartInfo(authUrl) { UseShellExecute = true });

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromMinutes(5));

        HttpListenerContext ctx;
        try
        {
            ctx = await listener.GetContextAsync().WaitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            return Result<AniListAuthResult>.Failure(ApplicationError.Validation("Authorization timed out."));
        }

        var code = ParseQueryParam(ctx.Request.Url?.Query, "code");
        await RespondAndClose(ctx, code is not null);

        if (string.IsNullOrWhiteSpace(code))
            return Result<AniListAuthResult>.Failure(ApplicationError.Validation("Authorization was denied or no code received."));

        var accessToken = await ExchangeCodeAsync(code, clientId, clientSecret, cancellationToken);
        if (accessToken is null)
            return Result<AniListAuthResult>.Failure(ApplicationError.Validation("Failed to exchange authorization code for access token."));

        var viewer = await FetchViewerAsync(accessToken, cancellationToken);
        if (viewer is null)
            return Result<AniListAuthResult>.Failure(ApplicationError.Validation("Failed to fetch AniList user info."));

        return Result<AniListAuthResult>.Success(new AniListAuthResult(accessToken, viewer.Value.Username, viewer.Value.UserId));
    }

    private async Task<string?> ExchangeCodeAsync(string code, string clientId, string clientSecret, CancellationToken ct)
    {
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["redirect_uri"] = RedirectUri,
            ["code"] = code,
        });

        try
        {
            var response = await httpClient.PostAsync(TokenUrl, form, ct);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("access_token").GetString();
        }
        catch
        {
            return null;
        }
    }

    private async Task<(string Username, int UserId)?> FetchViewerAsync(string accessToken, CancellationToken ct)
    {
        const string query = """{"query":"query{Viewer{id name}}"}""";
        var req = new HttpRequestMessage(HttpMethod.Post, "https://graphql.anilist.co");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        req.Content = new StringContent(query, Encoding.UTF8, "application/json");

        try
        {
            var response = await httpClient.SendAsync(req, ct);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var viewer = doc.RootElement.GetProperty("data").GetProperty("Viewer");
            var username = viewer.GetProperty("name").GetString() ?? string.Empty;
            var userId = viewer.GetProperty("id").GetInt32();
            return (username, userId);
        }
        catch
        {
            return null;
        }
    }

    private static string? ParseQueryParam(string? query, string key)
    {
        if (string.IsNullOrWhiteSpace(query)) return null;
        var qs = query.TrimStart('?');
        foreach (var pair in qs.Split('&'))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 2 && parts[0] == key)
                return Uri.UnescapeDataString(parts[1]);
        }
        return null;
    }

    private static async Task RespondAndClose(HttpListenerContext ctx, bool success)
    {
        var msg = success
            ? "Authentication successful — you can close this window and return to TanaHub."
            : "Authentication failed or was denied — you can close this window.";
        var html = $"<html><head><title>TanaHub</title></head><body style=\"font-family:sans-serif;padding:40px\"><p>{msg}</p></body></html>";
        var bytes = Encoding.UTF8.GetBytes(html);
        ctx.Response.ContentType = "text/html; charset=utf-8";
        ctx.Response.ContentLength64 = bytes.Length;
        await ctx.Response.OutputStream.WriteAsync(bytes);
        ctx.Response.Close();
    }
}
