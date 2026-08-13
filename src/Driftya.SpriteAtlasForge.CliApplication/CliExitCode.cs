namespace Driftya.SpriteAtlasForge.CliApplication;

internal enum CliExitCode
{
    Success = 0,
    InvalidArguments = 1,
    InvalidProject = 3,
    IoFailure = 4,
    Cancelled = 5,
    ProcessingFailure = 6,
}
