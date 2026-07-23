using System;
using System.IO;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ScreenshotApp.Models
{
    public partial class GalleryItem : ObservableObject
    {
        public string FilePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public DateTime CreatedTime { get; set; }
        
        [ObservableProperty]
        private BitmapImage? _thumbnail;

        [ObservableProperty]
        private bool _isSelected;

        public static GalleryItem? CreateFromFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath)) return null;

                var fileInfo = new FileInfo(filePath);
                var bitmap = new BitmapImage();
                
                using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = stream;
                    bitmap.DecodePixelWidth = 400; // Optimize decoding size for thumbnail grid
                    bitmap.EndInit();
                }
                bitmap.Freeze(); // Freezing allows cross-thread UI access and frees unmanaged stream reference

                return new GalleryItem
                {
                    FilePath = filePath,
                    FileName = fileInfo.Name,
                    CreatedTime = fileInfo.CreationTime,
                    Thumbnail = bitmap
                };
            }
            catch
            {
                return null;
            }
        }
    }
}
