namespace SongStash.Desktop.ViewModels;

public enum ImportWorkflowState
{
    Idle,
    Loading,
    PreviewReady,
    Importing,
    Completed,
    SourceUnavailable,
    UnsupportedSchema,
    Error,
    Cancelled
}
