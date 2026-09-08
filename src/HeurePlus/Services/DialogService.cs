using System.IO;
using Microsoft.Win32;

namespace HeurePlus.Services;

/// <summary>Fenêtres de dialogue fichier / dossier (isolées des ViewModels).</summary>
public sealed class DialogService
{
    public string? SaveFile(string filter, string defaultFileName, string? initialDirectory = null)
    {
        var dialog = new SaveFileDialog
        {
            Filter = filter,
            FileName = defaultFileName,
            AddExtension = true,
            OverwritePrompt = true
        };
        if (Directory.Exists(initialDirectory)) dialog.InitialDirectory = initialDirectory;
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? OpenFile(string filter, string? initialDirectory = null)
    {
        var dialog = new OpenFileDialog { Filter = filter, CheckFileExists = true };
        if (Directory.Exists(initialDirectory)) dialog.InitialDirectory = initialDirectory;
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickFolder(string? initialDirectory = null)
    {
        var dialog = new OpenFolderDialog();
        if (Directory.Exists(initialDirectory)) dialog.InitialDirectory = initialDirectory;
        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }
}
