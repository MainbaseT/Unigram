//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using Windows.UI;

namespace Telegram.Streams
{
    public partial class ParticlesImageSource : AnimatedImageSource
    {
        public Color Foreground { get; }

        public Color Background { get; }

        public bool IsText { get; }

        public ParticlesImageSource()
        {
            Foreground = Colors.White;
            Background = Color.FromArgb(0x54, 0, 0, 0);
            IsText = false;
        }

        public ParticlesImageSource(Color foreground)
        {
            Foreground = foreground;
            Background = Colors.Transparent;
            IsText = true;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Foreground, Background);
        }

        public override string FilePath => string.Empty;

        public override long FileSize => 0;

        public override long Id => 0;

        public override long Offset => 0;

        public override void ReadCallback(long count)
        {

        }

        public override void SeekCallback(long offset)
        {

        }
    }
}
