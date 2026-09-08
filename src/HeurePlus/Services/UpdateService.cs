using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace HeurePlus.Services;

/// <summary>
/// Met à jour l'application depuis le dépôt git (branche <c>0.1</c>).
/// Fonctionne uniquement quand l'exécutable tourne depuis le dépôt source
/// (contexte développement) ; sinon, propose une mise à jour manuelle.
/// </summary>
public sealed class UpdateService
{
    public const string Branch = "0.1";

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

    public bool IsAvailable => RepoRoot is not null;

    public sealed record UpdateStatus(bool RepoFound, string Branch, string ShortCommit,
        string CommitDate, int Behind, string Message);

    public UpdateStatus Check()
    {
        if (RepoRoot is null)
            return new UpdateStatus(false, "—", "—", "", 0,
                "Dépôt git introuvable : l'application n'est pas lancée depuis les sources. Mise à jour manuelle.");

        string branch = Git("rev-parse --abbrev-ref HEAD").output;
        string commit = Git("rev-parse --short HEAD").output;
        string date = Git("show -s --format=%cd --date=format:%d/%m/%Y HEAD").output;

        bool online = Git($"fetch --quiet origin {Branch}", 15000).code == 0;

        int behind = int.TryParse(Git($"rev-list --count HEAD..origin/{Branch}").output, out var b) ? b : 0;

        string message = behind > 0
            ? $"Mise à jour disponible : {behind} nouveau(x) commit(s) sur origin/{Branch}."
            : online
                ? "L'application est à jour."
                : "Dépôt distant injoignable — comparaison avec la dernière version connue : à jour.";

        return new UpdateStatus(true, branch, commit, date, behind, message);
    }

    /// <summary>Fait un fast-forward de la branche courante sur origin/0.1.</summary>
    public (bool ok, string output) Update()
    {
        if (RepoRoot is null) return (false, "Dépôt git introuvable.");

        string current = Git("rev-parse --abbrev-ref HEAD").output;
        if (!string.Equals(current, Branch, StringComparison.OrdinalIgnoreCase))
        {
            var co = Git($"checkout {Branch}", 30000);
            if (co.code != 0) return (false, "Bascule sur " + Branch + " impossible.\n" + co.output);
        }

        var (code, output) = Git($"merge --ff-only origin/{Branch}", 60000);
        if (code != 0) return (false, string.IsNullOrWhiteSpace(output) ? "Échec de la mise à jour." : output);

        string newCommit = Git("rev-parse --short HEAD").output;
        return (true, $"Mise à jour appliquée (commit {newCommit}). Recompilez / relancez l'application.");
    }

    private (int code, string output) Git(string arguments, int timeoutMs = 15000)
    {
        try
        {
            var psi = new ProcessStartInfo("git", arguments)
            {
                WorkingDirectory = RepoRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            // Ne jamais demander d'identifiants de façon interactive : échec rapide sinon.
            psi.Environment["GIT_TERMINAL_PROMPT"] = "0";
            psi.Environment["GCM_INTERACTIVE"] = "Never";
            psi.Environment["GIT_ASKPASS"] = "echo";

            using var process = Process.Start(psi);
            if (process is null) return (-1, "git introuvable.");

            var sb = new StringBuilder();
            process.OutputDataReceived += (_, e) => { if (e.Data is not null) sb.AppendLine(e.Data); };
            process.ErrorDataReceived += (_, e) => { if (e.Data is not null) sb.AppendLine(e.Data); };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            if (!process.WaitForExit(timeoutMs))
            {
                try { process.Kill(entireProcessTree: true); } catch { /* ignore */ }
                return (-1, "Délai dépassé.");
            }
            process.WaitForExit();
            return (process.ExitCode, sb.ToString().Trim());
        }
        catch (Exception ex)
        {
            return (-1, ex.Message);
        }
    }
}
