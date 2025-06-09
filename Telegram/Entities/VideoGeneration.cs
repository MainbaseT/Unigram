using System;
using Windows.Foundation;
using Windows.Media.MediaProperties;

namespace Telegram.Entities
{
    public partial class VideoGeneration
    {
        public bool Transcode { get; set; }
        public bool Mute { get; set; }
        public uint Width { get; set; }
        public uint Height { get; set; }
        public uint VideoBitrate { get; set; }
        public uint AudioBitrate { get; set; }

        public TimeSpan? TrimStartTime { get; set; }
        public TimeSpan? TrimStopTime { get; set; }

        public bool Transform { get; set; }
        public ImageRotation Rotation { get; set; }
        public ImageFlip Flip { get; set; }
        public Size OutputSize { get; set; }
        public Rect CropRectangle { get; set; }
    }

}
