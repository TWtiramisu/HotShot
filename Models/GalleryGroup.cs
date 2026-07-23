using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ScreenshotApp.Models
{
    public partial class GalleryGroup : ObservableObject
    {
        public string DateTitle { get; set; } = string.Empty;

        [ObservableProperty]
        private ObservableCollection<GalleryItem> _items = new();
    }
}
