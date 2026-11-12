using CommunityToolkit.Mvvm.Input;
using SongStash.Application.Backup;

namespace SongStash.Desktop.ViewModels;

public sealed class BackupListRowViewModel(BackupInfo info, string dateLabel, string sizeLabel,
    string restoreLabel, string showInFolderLabel, string deleteLabel,
    Action<BackupInfo> restore, Action<BackupInfo> showInFolder, Action<BackupInfo> delete)
{
    public BackupInfo Info { get; } = info;
    public string DateLabel { get; } = dateLabel;
    public string FileName => Path.GetFileName(Info.Path);
    public string SizeLabel { get; } = sizeLabel;
    public string RestoreLabel { get; } = restoreLabel;
    public string ShowInFolderLabel { get; } = showInFolderLabel;
    public string DeleteLabel { get; } = deleteLabel;
    public RelayCommand RestoreCommand { get; } = new(() => restore(info));
    public RelayCommand ShowInFolderCommand { get; } = new(() => showInFolder(info));
    public RelayCommand DeleteCommand { get; } = new(() => delete(info));
}
