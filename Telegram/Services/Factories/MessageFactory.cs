//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Newtonsoft.Json;
using System;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Entities;
using Telegram.Native;
using Telegram.Td.Api;
using Windows.Media.Effects;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.Storage.FileProperties;

namespace Telegram.Services.Factories
{
    public static class MessageFactory
    {
        public static async Task<BaseObject> CreatePhotoAsync(StoragePhoto photo, FormattedText caption, bool highQuality, bool captionAboveMedia, bool spoiler, MessageSelfDestructType ttl, long starCount)
        {
            var conversionType = ConversionType.Compress;
            var file = photo.File;

            var generation = photo.IsEdited ? photo.EditState : null;

            var size = await ImageHelper.GetScaleAsync(file, allowMultipleFrames: ttl != null || starCount > 0, requestedMinSide: highQuality ? 2560 : 1280, generation: generation);
            if (size.Width == 0 || size.Height == 0)
            {
                // This may happen if the image is a GIF with multiple frames.
                conversionType = ConversionType.Copy;
                generation = null;
            }
            else if (highQuality)
            {
                conversionType = ConversionType.HighQuality;
            }

            var serialized = generation != null ? JsonConvert.SerializeObject(generation) : null;
            var generated = await file.ToGeneratedAsync(conversionType, serialized);
            var thumbnail = default(InputThumbnail);

            if (starCount > 0)
            {
                return new InputPaidMedia(new InputPaidMediaTypePhoto(), generated, thumbnail, Array.Empty<int>(), size.Width, size.Height);
            }
            else if (conversionType == ConversionType.Copy)
            {
                return new InputMessageDocument(generated, thumbnail, false, caption);
            }

            return new InputMessagePhoto(generated, thumbnail, Array.Empty<int>(), size.Width, size.Height, caption, captionAboveMedia, ttl, spoiler);
        }

        public static async Task<BaseObject> CreateVideoAsync(StorageVideo video, FormattedText caption, bool animated, bool captionAboveMedia, bool spoiler, MessageSelfDestructType ttl, long starCount)
        {
            var duration = video.TotalSeconds;
            var videoWidth = video.Width;
            var videoHeight = video.Height;
            var generation = video.GetGeneration();

            generation ??= new VideoGeneration
            {
                Mute = animated
            };

            if (generation.TrimStartTime is TimeSpan trimStart && generation.TrimStopTime is TimeSpan trimStop)
            {
                duration = (int)(trimStop.TotalSeconds - trimStart.TotalSeconds);
            }

            if (generation.Transform && !generation.CropRectangle.IsEmpty)
            {
                videoWidth = (int)generation.CropRectangle.Width;
                videoHeight = (int)generation.CropRectangle.Height;
            }

            var serialized = JsonConvert.SerializeObject(generation);
            var generated = await video.File.ToGeneratedAsync(ConversionType.Transcode, serialized);
            var thumbnail = await video.ToVideoThumbnailAsync(generation, ConversionType.TranscodeThumbnail, serialized);

            if (starCount > 0)
            {
                return new InputPaidMedia(new InputPaidMediaTypeVideo(null, 0, duration, true), generated, thumbnail, Array.Empty<int>(), videoWidth, videoHeight);
            }
            else if (animated && ttl == null)
            {
                return new InputMessageAnimation(generated, thumbnail, Array.Empty<int>(), duration, videoWidth, videoHeight, caption, captionAboveMedia, spoiler);
            }

            return new InputMessageVideo(generated, thumbnail, null, 0, Array.Empty<int>(), duration, videoWidth, videoHeight, true, caption, captionAboveMedia, ttl, spoiler);
        }

        public static async Task<InputMessageContent> CreateVideoNoteAsync(StorageFile file, MediaEncodingProfile profile = null, VideoTransformEffectDefinition transform = null)
        {
            var basicProps = await file.GetBasicPropertiesAsync();
            var videoProps = await file.Properties.GetVideoPropertiesAsync();

            //var thumbnail = await ImageHelper.GetVideoThumbnailAsync(file, videoProps, transform);

            var duration = (int)videoProps.Duration.TotalSeconds;
            var videoWidth = (int)videoProps.GetWidth();
            var videoHeight = (int)videoProps.GetHeight();

            if (profile != null)
            {
                videoWidth = videoProps.Orientation is VideoOrientation.Rotate180 or VideoOrientation.Normal ? (int)profile.Video.Width : (int)profile.Video.Height;
                videoHeight = videoProps.Orientation is VideoOrientation.Rotate180 or VideoOrientation.Normal ? (int)profile.Video.Height : (int)profile.Video.Width;
            }

            var conversion = new VideoGeneration();
            if (profile != null)
            {
                conversion.Transcode = true;
                conversion.Width = profile.Video.Width;
                conversion.Height = profile.Video.Height;
                conversion.VideoBitrate = profile.Video.Bitrate;

                if (profile.Audio != null)
                {
                    conversion.AudioBitrate = profile.Audio.Bitrate;
                }

                if (transform != null)
                {
                    conversion.Transform = true;
                    conversion.Rotation = transform.Rotation;
                    conversion.OutputSize = transform.OutputSize;
                    conversion.Mirror = transform.Mirror;
                    conversion.CropRectangle = transform.CropRectangle;
                }
            }

            var serialized = JsonConvert.SerializeObject(conversion);
            var generated = await file.ToGeneratedAsync(ConversionType.Transcode, serialized);
            var thumbnail = await file.ToVideoThumbnailAsync(conversion, ConversionType.TranscodeThumbnail, serialized);

            // TODO: 172 selfDestructType
            return new InputMessageVideoNote(generated, thumbnail, duration, Math.Min(videoWidth, videoHeight), null);
        }

        public static async Task<BaseObject> CreateDocumentAsync(StorageMedia media, FormattedText caption, bool asFile, bool asScreenshot)
        {
            var file = media.File;
            var generated = await file.ToGeneratedAsync(asScreenshot ? ConversionType.Screenshot : ConversionType.Copy);

            if (!asFile && media is StorageAudio audio)
            {
                var duration = audio.TotalSeconds;

                var title = audio.Title;
                var performer = audio.Performer;

                var albumCover = new InputThumbnail(await file.ToGeneratedAsync(ConversionType.AlbumCover), 0, 0);

                return new InputMessageAudio(generated, albumCover, duration, title, performer, caption);
            }

            var thumbnail = new InputThumbnail(await file.ToGeneratedAsync(ConversionType.DocumentThumbnail), 0, 0);

            if (!asFile && file.FileType.Equals(".webp", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    if (PlaceholderImageHelper.IsWebP(file.Path, out int width, out int height))
                    {
                        if ((width == 512 && height <= width) || (height == 512 && width <= height))
                        {
                            return new InputMessageSticker(generated, null, width, height, string.Empty);
                        }
                    }
                }
                catch
                {
                    // Not really a sticker, go on sending as a file
                }
            }
            else if (!asFile && file.FileType.Equals(".tgs", StringComparison.OrdinalIgnoreCase))
            {
                // TODO
            }

            return new InputMessageDocument(generated, thumbnail, true, caption);
        }
    }

    public partial class InputMessageFactory
    {
        public InputFile InputFile { get; set; }
        public Func<InputFile, FormattedText, InputMessageContent> Delegate { get; set; }
        public Func<InputFile, InputPaidMedia> PaidDelegate { get; set; }
    }
}
