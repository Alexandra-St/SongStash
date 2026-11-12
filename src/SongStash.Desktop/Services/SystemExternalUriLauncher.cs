using System.Diagnostics;

namespace SongStash.Desktop.Services;

public sealed class SystemExternalUriLauncher : IExternalUriLauncher
{
    public void Open(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
    }
}
