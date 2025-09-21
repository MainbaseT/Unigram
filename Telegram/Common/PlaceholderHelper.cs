//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Native;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace Telegram.Common
{
    public static class PlaceholderHelper
    {
        [ThreadStatic]
        private static PlaceholderImageHelper _foreground;

        public static PlaceholderImageHelper Foreground
        {
            get
            {
                if (_foreground == null)
                {
                    try
                    {
                        _foreground = new PlaceholderImageHelper();
                    }
                    catch
                    {
                        Logger.Error(Environment.StackTrace);
                        throw;
                    }
                }

                _foreground.HandleDeviceLost();
                return _foreground;
            }
        }

        private static PlaceholderImageHelper _background;
        private static readonly object _backgroundLock = new();

        public static PlaceholderImageHelper Background
        {
            get
            {
                lock (_backgroundLock)
                {
                    if (_background == null)
                    {
                        try
                        {
                            _background = new PlaceholderImageHelper();
                        }
                        catch
                        {
                            Logger.Error(Environment.StackTrace);
                            throw;
                        }
                    }

                    _background.HandleDeviceLost();
                    return _background;
                }
            }
        }


        public static ImageSource GetBitmap(IClientService clientService, PhotoSize photoSize)
        {
            return GetBitmap(clientService, photoSize.Photo, photoSize.Width, photoSize.Height);
        }

        public static ImageSource GetBitmap(IClientService clientService, File file, int width, int height)
        {
            if (file.Local.IsDownloadingCompleted)
            {
                return UriEx.ToBitmap(file.Local.Path, width, height);
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive && clientService != null)
            {
                clientService.DownloadFile(file.Id, 1);
            }

            return null;
        }

        public static async Task<LoadedImageSurface> LoadBitmapAsync(File file)
        {
            try
            {
                var item = await StorageFile.GetFileFromPathAsync(file.Local.Path);
                using (var stream = await item.OpenReadAsync())
                {
                    return LoadedImageSurface.StartLoadFromStream(stream);
                }
            }
            catch
            {
                return null;
            }
        }

        private static readonly DisposableMutex _patternSurfaceLock = new();

        public static async Task<(LoadedImageSurface Surface, GiftPatterns Patterns)> LoadPatternBitmapAsync(File file, double rasterizationScale)
        {
            using var locked = await _patternSurfaceLock.WaitAsync();

            var bitmap = default(LoadedImageSurface);
            var patterns = default(GiftPatterns);
            var scale = (int)(rasterizationScale * 100);

            rasterizationScale = 0.25 * rasterizationScale;

            using (var stream = new InMemoryRandomAccessStream())
            {
                patterns = await Background.DrawSvgAsync(file.Local.Path, Colors.White, stream, rasterizationScale);
                bitmap = LoadedImageSurface.StartLoadFromStream(stream, new Size(360, 740));
            }

            return (bitmap, patterns);
        }

        public static async void GetBlurred(SoftwareBitmapSource source, string path, float amount = 3)
        {
            try
            {
                var bitmap = await Task.Run(() => Background.DrawBlurred(path, amount));
                await source.SetBitmapAsync(bitmap);
            }
            catch { }
        }

        public static async void GetBlurred(SoftwareBitmapSource source, IList<byte> bytes, float amount = 3)
        {
            try
            {
                var bitmap = await Task.Run(() => Background.DrawBlurred(bytes, amount));
                await source.SetBitmapAsync(bitmap);
            }
            catch { }
        }
    }
}
