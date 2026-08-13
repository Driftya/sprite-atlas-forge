using Driftya.SpriteAtlasForge.ClientApplication.Models;

namespace Driftya.SpriteAtlasForge.ClientApplication.Pages
{
    public partial class ProjectDetailPage : ContentPage
    {
        public ProjectDetailPage(ProjectDetailPageModel model)
        {
            InitializeComponent();

            BindingContext = model;
        }
    }
}
