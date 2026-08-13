using CommunityToolkit.Mvvm.ComponentModel;
using Driftya.SpriteAtlasForge.Application;

namespace Driftya.SpriteAtlasForge.ClientApplication.PageModels;

public partial class WorkspacePageModel : ObservableObject
{
    public WorkspacePageModel(AtlasForgeApplicationInfo applicationInfo)
    {
        Title = applicationInfo.Name;
        Description = applicationInfo.Description;
        NativeProjectExtension = applicationInfo.NativeProjectExtension;
    }

    public string Title { get; }

    public string Description { get; }

    public string NativeProjectExtension { get; }

    [ObservableProperty]
    public partial string Status { get; set; } = "Phase 0 foundation ready";
}
