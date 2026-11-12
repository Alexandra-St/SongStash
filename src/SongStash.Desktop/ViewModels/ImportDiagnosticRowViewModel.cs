using CommunityToolkit.Mvvm.ComponentModel;
using SongStash.Application.Importing;

namespace SongStash.Desktop.ViewModels;

public sealed class ImportDiagnosticRowViewModel(ImportDiagnostic diagnostic, Func<ImportUiText> getStrings) : ObservableObject
{
    public string Message => getStrings().Diagnostic(diagnostic.Code, diagnostic.Message);

    public void RefreshStrings() => OnPropertyChanged(nameof(Message));
}
