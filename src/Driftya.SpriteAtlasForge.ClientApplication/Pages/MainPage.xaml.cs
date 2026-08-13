using Driftya.SpriteAtlasForge.ClientApplication.Models;
using Driftya.SpriteAtlasForge.ClientApplication.PageModels;

namespace Driftya.SpriteAtlasForge.ClientApplication.Pages
{
    public partial class MainPage : ContentPage
    {
        public MainPage(MainPageModel model)
        {
            InitializeComponent();
            BindingContext = model;
        }
    }
}