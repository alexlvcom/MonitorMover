using System.Net;
using AlexLV.WindowsUi;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MonitorMover;

var payload = Encoding.UTF8.GetBytes("test installer payload");
var digest = Convert.ToHexString(SHA256.HashData(payload));
const string root = "https://github.com/alexlvcom/MonitorMover/releases/download/v1.3.0/";

JsonElement Release(string tag = "v1.3.0", bool prerelease = false, string? hash = null, string name = UpdateService.InstallerName, string? url = null) =>
    JsonSerializer.SerializeToElement(new
    {
        tag_name = tag, draft = false, prerelease,
        assets = new object[] {
            new { name, browser_download_url = url ?? root + name, digest = hash is null ? null : "sha256:" + hash },
            new { name = "SHA256SUMS.txt", browser_download_url = root + "SHA256SUMS.txt", digest = (string?)null },
        }
    });

void Check(bool pass, string message) { if (!pass) throw new Exception(message); Console.WriteLine("PASS: " + message); }

var now = new DateTime(2026, 1, 31, 12, 0, 0, DateTimeKind.Utc);
Check(UpdateSchedule.NextCheck(now, UpdateCheckFrequency.Hourly) == now.AddHours(1), "hourly schedule");
Check(UpdateSchedule.NextCheck(now, UpdateCheckFrequency.Daily) == now.AddDays(1), "daily default schedule");
Check(UpdateSchedule.NextCheck(now, UpdateCheckFrequency.Weekly) == now.AddDays(7), "weekly schedule");
Check(UpdateSchedule.NextCheck(now, UpdateCheckFrequency.Monthly) == new DateTime(2026, 2, 28, 12, 0, 0, DateTimeKind.Utc), "calendar month handles month end");
Check(UpdateSchedule.NextCheck(now, UpdateCheckFrequency.Never) == DateTime.MaxValue, "Never disables scheduled checks");
Check(UpdateSchedule.Normalize((UpdateCheckFrequency)99) == UpdateCheckFrequency.Daily, "invalid saved frequency falls back to daily");

var current = new Version(1, 2, 0);
Check(UpdateService.ParseRelease(Release(), current)?.Version == new Version(1, 3, 0), "newer stable release detected");
Check(UpdateService.ParseRelease(Release("v1.2.0"), current) is null, "same version ignored");
Check(UpdateService.ParseRelease(Release("v1.1.13"), current) is null, "older release ignored");
Check(UpdateService.ParseRelease(Release(prerelease: true), current) is null, "prerelease ignored");
Check(UpdateService.ParseRelease(Release(name: "MonitorMover.exe"), current) is null, "portable-only release ignored");

try { UpdateService.ParseRelease(Release(url: "https://example.com/setup.exe"), current); throw new Exception("unsafe URL accepted"); }
catch (InvalidDataException) { Console.WriteLine("PASS: foreign installer URL rejected"); }

var cache = Path.Combine(Path.GetTempPath(), "monitormover-update-tests-" + Guid.NewGuid().ToString("N"));
try
{
    using var service = new UpdateService(new FakeHandler(payload, digest));
    var update = (await service.CheckAsync(current, default))!;
    var path = await service.DownloadAsync(update, cache, default);
    Check(File.ReadAllBytes(path).SequenceEqual(payload), "installer downloaded and SHA-256 verified");

    var fallback = UpdateService.ParseRelease(Release(), current)!;
    var fallbackPath = await service.DownloadAsync(fallback, cache, default);
    Check(File.Exists(fallbackPath), "SHA256SUMS fallback supported");

    var bad = update with { Sha256 = new string('0', 64) };
    var before = Directory.GetFiles(cache, "*.exe", SearchOption.AllDirectories).Length;
    try { await service.DownloadAsync(bad, cache, default); throw new Exception("bad checksum accepted"); }
    catch (InvalidDataException) { Console.WriteLine("PASS: checksum mismatch rejected"); }
    Check(Directory.GetFiles(cache, "*.exe", SearchOption.AllDirectories).Length == before, "failed download removed");

    using var cts = new CancellationTokenSource(); cts.Cancel();
    try { await service.DownloadAsync(update, cache, cts.Token); throw new Exception("cancellation ignored"); }
    catch (OperationCanceledException) { Console.WriteLine("PASS: download cancellation supported"); }

    using var missing = new UpdateService(new FakeHandler(payload, digest, HttpStatusCode.NotFound));
    Check(await missing.CheckAsync(current, default) is null, "missing release handled");

    using var rateLimited = new UpdateService(new FakeHandler(payload, digest, HttpStatusCode.Forbidden));
    try { await rateLimited.CheckAsync(current, default); throw new Exception("API failure ignored"); }
    catch (HttpRequestException) { Console.WriteLine("PASS: API failure reported to caller"); }
}
finally { if (Directory.Exists(cache)) Directory.Delete(cache, true); }

var settingsRoot = Path.Combine(Path.GetTempPath(), "MonitorMoverSettingsTest-" + Guid.NewGuid().ToString("N"));
try
{
    var settingsPath = Path.Combine(settingsRoot, "MonitorMover", "settings.json");
    new AppSettings { UpdateCheckFrequency = UpdateCheckFrequency.Hourly }.Save(settingsPath);
    Check(File.Exists(settingsPath) && File.ReadAllText(settingsPath).Contains("\"UpdateCheckFrequency\": 1"), "settings save creates a missing folder and file");
    Check(!File.Exists(settingsPath + ".tmp"), "settings save leaves no temp file");

    var blocked = Path.Combine(settingsRoot, "blocked");
    Directory.CreateDirectory(Path.Combine(blocked, "settings.json"));
    try { new AppSettings().Save(Path.Combine(blocked, "settings.json")); throw new Exception("settings save failure swallowed"); }
    catch (IOException) { Console.WriteLine("PASS: settings save failure reported to caller"); }
    catch (UnauthorizedAccessException) { Console.WriteLine("PASS: settings save failure reported to caller"); }
}
finally { if (Directory.Exists(settingsRoot)) Directory.Delete(settingsRoot, true); }

// Theme setting.
Check(new AppSettings().Theme == AppTheme.System, "theme follows Windows by default");
Check(ThemeManager.Resolve(AppTheme.System, systemUsesLight: true) == false, "System resolves to light when Windows is light");
Check(ThemeManager.Resolve(AppTheme.System, systemUsesLight: false), "System resolves to dark when Windows is dark");
Check(!ThemeManager.Resolve(AppTheme.Light, systemUsesLight: false) && ThemeManager.Resolve(AppTheme.Dark, systemUsesLight: true),
    "Light and Dark ignore the Windows mode");
var themeRoot = Path.Combine(Path.GetTempPath(), "MonitorMoverThemeTest-" + Guid.NewGuid().ToString("N"));
try
{
    var themePath = Path.Combine(themeRoot, "settings.json");
    new AppSettings { Theme = AppTheme.Dark, UpdateCheckFrequency = UpdateCheckFrequency.Weekly }.Save(themePath);
    var loaded = AppSettings.Load(themePath);
    Check(loaded.Theme == AppTheme.Dark && loaded.UpdateCheckFrequency == UpdateCheckFrequency.Weekly, "theme survives a save and load");
    File.WriteAllText(themePath, "{ \"Theme\": 42 }");
    Check(AppSettings.Load(themePath).Theme == AppTheme.System, "unknown saved theme falls back to System");
    File.WriteAllText(themePath, "{ \"UpdateCheckFrequency\": 1 }");
    Check(AppSettings.Load(themePath).Theme == AppTheme.System, "settings from 1.2.x without a theme load as System");
}
finally { if (Directory.Exists(themeRoot)) Directory.Delete(themeRoot, true); }
var running = new AppSettings { Theme = AppTheme.Light };
var draft = running.Clone();
draft.Theme = AppTheme.Dark;
draft.UpdateCheckFrequency = UpdateCheckFrequency.Never;
Check(running.Theme == AppTheme.Light && running.UpdateCheckFrequency == UpdateCheckFrequency.Daily, "a Settings draft leaves the running settings unchanged");

// One version source: the project file, matched by the newest changelog heading.
var repo = new DirectoryInfo(AppContext.BaseDirectory);
while (repo is not null && !File.Exists(Path.Combine(repo.FullName, "MonitorMover.csproj"))) repo = repo.Parent;
Check(repo is not null, "repository root found");
var project = File.ReadAllText(Path.Combine(repo!.FullName, "MonitorMover.csproj"));
var projectVersion = System.Text.RegularExpressions.Regex.Match(project, @"<Version>([^<]+)</Version>").Groups[1].Value;
var entries = Changelog.Parse(File.ReadAllText(Path.Combine(repo.FullName, "CHANGELOG.md")));
Check(entries.Length > 0 && entries[0].Version == projectVersion, $"newest changelog heading matches the project version {projectVersion}");
Check(entries[0].Sections.Count > 0 && entries[0].Sections[0].Heading == "Main changes", "release notes lead with Main changes");
Check(project.Contains($"<InformationalVersion>{projectVersion}</InformationalVersion>") &&
      project.Contains($"<FileVersion>{projectVersion}.0</FileVersion>") &&
      project.Contains($"<AssemblyVersion>{projectVersion}.0</AssemblyVersion>"), "project version fields agree");
Check(Changelog.Entries.Length == entries.Length, "embedded changelog is the repository changelog");
var parsed = Changelog.Parse("## 2.0.0 - 2026-01-02\n\n### Main changes\n- First\n  continued\n\n### Other updates\n- Second\n\n## 1.9.0 - 2026-01-01\n- Old\n");
Check(parsed.Length == 2 && parsed[0].Notes.SequenceEqual(new[] { "First continued", "Second" }) && parsed[1].Notes.Single() == "Old",
    "changelog parser reads sections, wrapped bullets and older releases");

// WPF needs culture data, and every window must open already themed.
Check(!project.Contains("<InvariantGlobalization>true</InvariantGlobalization>"),
    "the app keeps culture data for WPF text layout (invariant mode crashed TextBox layout)");
var nonThemed = Directory.GetFiles(Path.Combine(repo.FullName, "Ui"), "*.xaml")
    .Where(f => File.ReadAllText(f).Contains("x:Class=") && !File.ReadAllText(f).TrimStart().StartsWith("<ui:ThemedWindow"))
    .Select(Path.GetFileName).ToList();
Check(nonThemed.Count == 0, "every window derives from ThemedWindow" + (nonThemed.Count > 0 ? ": " + string.Join(", ", nonThemed) : ""));

sealed class FakeHandler(byte[] payload, string digest, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.Headers.UserAgent.Count == 0) throw new Exception("Missing GitHub User-Agent");
        var url = request.RequestUri!.AbsoluteUri;
        var content = url.Contains("api.github.com")
            ? JsonSerializer.Serialize(new {
                tag_name = "v1.3.0", draft = false, prerelease = false,
                assets = new[] { new { name = UpdateService.InstallerName, browser_download_url = "https://github.com/alexlvcom/MonitorMover/releases/download/v1.3.0/" + UpdateService.InstallerName, digest = "sha256:" + digest } }
            }) : digest + "  " + UpdateService.InstallerName;
        return Task.FromResult(new HttpResponseMessage(status) {
            Content = url.EndsWith(".exe") ? new ByteArrayContent(payload) : new StringContent(content)
        });
    }
}
