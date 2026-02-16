using System.Collections.ObjectModel;

namespace CryptoMail.ViewModels;

public sealed class LogViewModel
{
    public ObservableCollection<string> Lines { get; } = new();

    public void Add(string message)
    {
        var stamp = DateTime.Now.ToString("HH:mm:ss");
        Lines.Add($"[{stamp}] {message}");
    }

    public void Clear()
    {
        Lines.Clear();
    }
}
