using CommunityToolkit.Mvvm.Input;
using Driftya.SpriteAtlasForge.ClientApplication.Models;

namespace Driftya.SpriteAtlasForge.ClientApplication.PageModels
{
    public interface IProjectTaskPageModel
    {
        IAsyncRelayCommand<ProjectTask> NavigateToTaskCommand { get; }
        bool IsBusy { get; }
    }
}