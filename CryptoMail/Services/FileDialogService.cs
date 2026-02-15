using Microsoft.Win32;

namespace CryptoMail.Services;

public sealed class FileDialogService
{
    public string? OpenFile(string filter = "All files (*.*)|*.*")
    {
        var dialog = new OpenFileDialog
        {
            CheckFileExists = true,
            Multiselect = false,
            Filter = filter
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? SaveFile(string fileName, string filter = "All files (*.*)|*.*")
    {
        var dialog = new SaveFileDialog
        {
            FileName = fileName,
            Filter = filter,
            AddExtension = true
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
