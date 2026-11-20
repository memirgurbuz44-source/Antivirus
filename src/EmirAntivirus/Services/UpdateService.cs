using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace EmirAntivirus.Services;

public sealed record UpdateInfo(Version Version, string TagName, string ReleaseUrl);

public sealed class UpdateService
{
    private const string LatestReleaseApi = "https://api.github.com/repos/memirgurbuz44-source/Antivirus/releases/latest";
    private static readonly HttpClient Client = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("EmirAntivirus-Updater/1.0");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }

    public Version CurrentVersion => Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0);

    public async Task<UpdateInfo?> CheckAsync(CancellationToken cancellationToken = default)
    {
        using var response = await Client.GetAsync(LatestReleaseApi, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = json.RootElement;
        var tag = root.GetProperty("tag_name").GetString() ?? "";
        var url = root.GetProperty("html_url").GetString() ?? "";
        if (!Version.TryParse(tag.TrimStart('v', 'V'), out var latest) ||
            !Uri.TryCreate(url, UriKind.Absolute, out var releaseUri) ||
            releaseUri.Scheme != Uri.UriSchemeHttps) return null;
        return latest > CurrentVersion ? new UpdateInfo(latest, tag, releaseUri.ToString()) : null;
    }
}
