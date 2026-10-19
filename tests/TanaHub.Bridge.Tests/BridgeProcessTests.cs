using System.Diagnostics;
using System.Text;
using System.Text.Json;
using TanaHub.Bridge;

namespace TanaHub.Bridge.Tests;

public sealed class BridgeProcessTests
{
    [Fact]
    public async Task FeatureSettingsAndLibraryDataPersistThroughActualInfrastructure()
    {
        var directory = Path.Combine(Path.GetTempPath(), "tanahub-feature-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var first = await RunBridgeAsync(directory,
                """{"id":"settings","method":"settings.save","params":{"theme":"High contrast","aniListClientSecret":"private-client-secret","recognitionServicesEnabled":true}}""",
                """{"id":"preview","method":"library.import.preview","params":{"xml":"<myanimelist><anime><series_animedb_id>10</series_animedb_id><my_watched_episodes>4</my_watched_episodes></anime></myanimelist>"}}""",
                """{"id":"apply","method":"library.import.apply","params":{"xml":"<myanimelist><anime><series_animedb_id>10</series_animedb_id><my_watched_episodes>4</my_watched_episodes></anime></myanimelist>"}}""",
                """{"id":"review","method":"library.review","params":{"mediaId":"mal:anime:10","review":"A local review"}}""",
                """{"id":"organization","method":"library.organization","params":{"mediaId":"mal:anime:10","tags":["Favourite"],"customLists":["Weekend"]}}""",
                """{"id":"dates","method":"library.dates","params":{"mediaId":"mal:anime:10","startedAt":"2026-09-01T00:00:00+02:00"}}""");
            Assert.All(first, response => Assert.True(response.TryGetProperty("result", out _), response.GetRawText()));
            Assert.DoesNotContain("private-client-secret", first[0].GetRawText());

            var second = await RunBridgeAsync(directory,
                """{"id":"settings","method":"settings.get","params":{}}""",
                """{"id":"filtered","method":"library.list","params":{"tag":"Favourite","customList":"Weekend","sort":"Title"}}""",
                """{"id":"export","method":"library.export","params":{"format":"csv"}}""");
            Assert.Equal("High contrast", second[0].GetProperty("result").GetProperty("theme").GetString());
            Assert.True(second[0].GetProperty("result").GetProperty("hasAniListClientSecret").GetBoolean());
            var entry = Assert.Single(second[1].GetProperty("result").GetProperty("items").EnumerateArray());
            Assert.Equal("A local review", entry.GetProperty("review").GetString());
            Assert.Equal("2026-09-01T00:00:00+02:00", entry.GetProperty("startedAt").GetString());
            Assert.Contains("Favourite", second[2].GetProperty("result").GetProperty("text").GetString());
            Assert.DoesNotContain("private-client-secret", await File.ReadAllTextAsync(Path.Combine(directory, "settings.json")));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task StdioRoundtripPersistsLibraryAcrossRestartsWithoutTouchingUserData()
    {
        var directory = Path.Combine(Path.GetTempPath(), "tanahub-bridge-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var initial = await RunBridgeAsync(directory,
                """{"id":"empty","method":"library.list","params":{}}""",
                """{"id":"add","method":"library.add","params":{"mediaId":"anilist:154587","status":"Watching"}}""",
                """{"id":"progress","method":"library.progress","params":{"mediaId":"anilist:154587","amount":4}}""",
                """{"id":"score","method":"library.score","params":{"mediaId":"anilist:154587","score":9}}""",
                """{"id":"notes","method":"library.notes","params":{"mediaId":"anilist:154587","notes":"Продолжить вечером"}}""");
            Assert.Empty(initial[0].GetProperty("result").GetProperty("items").EnumerateArray());
            Assert.All(initial, response => Assert.True(response.TryGetProperty("result", out _), response.GetRawText()));
            Assert.True(File.Exists(Path.Combine(directory, "library.db")));

            var restarted = await RunBridgeAsync(directory,
                """{"id":"reload","method":"library.get","params":{"mediaId":"anilist:154587"}}""",
                """{"id":"remove","method":"library.remove","params":{"mediaId":"anilist:154587"}}""",
                """{"id":"missing","method":"library.get","params":{"mediaId":"anilist:154587"}}""");
            var saved = restarted[0].GetProperty("result");
            Assert.Equal("Watching", saved.GetProperty("status").GetString());
            Assert.Equal(4, saved.GetProperty("progress").GetInt32());
            Assert.Equal(9, saved.GetProperty("score").GetInt32());
            Assert.Equal("Продолжить вечером", saved.GetProperty("notes").GetString());
            Assert.Equal("Frieren: Beyond Journey's End", saved.GetProperty("media").GetProperty("title").GetString());
            Assert.True(restarted[1].GetProperty("result").GetProperty("removed").GetBoolean());
            Assert.Equal(JsonValueKind.Null, restarted[2].GetProperty("result").ValueKind);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static async Task<JsonElement[]> RunBridgeAsync(string directory, params string[] requests)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var testAssembly = typeof(BridgeProcessTests).Assembly.Location;
        var start = new ProcessStartInfo("dotnet")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(false),
            StandardOutputEncoding = new UTF8Encoding(false),
            StandardErrorEncoding = new UTF8Encoding(false),
            UseShellExecute = false,
            CreateNoWindow = true
        };
        // Execute the referenced bridge using the test dependency graph, which includes SQLite native assets.
        start.ArgumentList.Add("exec");
        start.ArgumentList.Add("--runtimeconfig");
        start.ArgumentList.Add(Path.ChangeExtension(testAssembly, ".runtimeconfig.json"));
        start.ArgumentList.Add("--depsfile");
        start.ArgumentList.Add(Path.ChangeExtension(testAssembly, ".deps.json"));
        start.ArgumentList.Add(typeof(BridgeHandler).Assembly.Location);
        start.Environment["TANAHUB_DATA_DIRECTORY"] = directory;
        using var process = Process.Start(start)!;
        try
        {
            var stdout = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var stderr = process.StandardError.ReadToEndAsync(timeout.Token);
            foreach (var request in requests)
                await process.StandardInput.WriteLineAsync(request.AsMemory(), timeout.Token);
            process.StandardInput.Close();
            await process.WaitForExitAsync(timeout.Token);
            Assert.Equal(0, process.ExitCode);
            Assert.Empty(await stderr);
            var lines = (await stdout).Split('\n', StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(requests.Length, lines.Length);
            return lines.Select(line =>
            {
                using var parsed = JsonDocument.Parse(line);
                return parsed.RootElement.Clone();
            }).ToArray();
        }
        finally
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
    }
}
