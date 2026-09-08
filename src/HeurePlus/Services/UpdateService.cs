using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace HeurePlus.Services;

/// <summary>
/// Vérifie et applique les mises à jour de Heure+ via <b>GitHub Releases</b>
/// (fonctionne pour la version installée comme pour la version lancée depuis
/// les sources). La détection d'un dépôt git local sert seulement à indiquer
/// qu'on est en « version de développement ».
/// </summary>
public sealed class UpdateService
{
    /// <summary>Dépôt GitHub « propriétaire/nom ».</summary>
    public const string GitHubRepo = "Foxitoww/Heure-";

    private static readonly HttpClient Http = CreateHttp();

    private static HttpClient CreateHttp()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("HeurePlus-Updater");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }

    public string? RepoRoot { get; }

    public UpdateService()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, ".git")))
            {
                RepoRoot = dir.FullName;
                break;
            }
            dir = dir.Parent;
        }
    }

    /// <summary>Vrai si l'exécutable tourne à l'intérieur du dépôt source.</summary>
    public bool RunningFromSources => RepoRoot is not null;

    public sealed record ReleaseInfo(
        string Tag, string Name, string Notes,
        Version? Version, string? InstallerUrl, long InstallerSize, string HtmlUrl);

    /// <summary>Dernière release publiée sur GitHub (null si injoignable / aucune).</summary>
    public async Task<ReleaseInfo?> LatestReleaseAsync()
    {
        try
        {
            var url = $"https://api.github.com/repos/{GitHubRepo}/releases/latest";
            using var response = await Http.GetAsync(url).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return null;

            await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);
            var root = doc.RootElement;

            string tag = root.TryGetProperty("tag_name", out var t) ? t.GetString() ?? "" : "";
            string name = root.TryGetProperty("name", out var n) && !string.IsNullOrWhiteSpace(n.GetString())
                ? n.GetString()! : tag;
            string notes = root.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "";
            string html = root.TryGetProperty("html_url", out var h) ? h.GetString() ?? "" : "";

            Version.TryParse(tag.TrimStart('v', 'V'), out var version);

            string? installer = null;
            long size = 0;
            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    string an = asset.TryGetProperty("name", out var ap) ? ap.GetString() ?? "" : "";
                    if (an.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        installer = asset.TryGetProperty("browser_download_url", out var du) ? du.GetString() : null;
                        size = asset.TryGetProperty("size", out var sz) ? sz.GetInt64() : 0;
                        break;
                    }
                }
            }

            return new ReleaseInfo(tag, name, notes.Trim(), version, installer, size, html);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Télécharge l'installeur dans %TEMP%. Retourne le chemin ou null.</summary>
    public async Task<string?> DownloadInstallerAsync(string url, IProgress<double>? progress = null)
    {
        try
        {
            string path = Path.Combine(Path.GetTempPath(), "HeurePlus-Setup-update.exe");

            using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            long? total = response.Content.Headers.ContentLength;
            await using var src = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            await using var dst = File.Create(path);

            var buffer = new byte[81920];
            long read = 0;
            int got;
            while ((got = await src.ReadAsync(buffer).ConfigureAwait(false)) > 0)
            {
                await dst.WriteAsync(buffer.AsMemory(0, got)).ConfigureAwait(false);
                read += got;
                if (total is > 0) progress?.Report((double)read / total.Value);
            }

            return path;
        }
        catch
        {
            return null;
        }
    }

    public static void RunInstaller(string path) =>
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });

    public static void OpenReleasePage(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { /* ignore */ }
    }
}
