//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.Graphics.Canvas.Effects;
using System;
using System.Numerics;
using Telegram.Native;
using Telegram.Navigation;
using Windows.Graphics.Effects;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Media
{
    public partial class TiledBrush : XamlCompositionBrushBase
    {
        public LoadedImageSurface ImageSource { get; set; }

        public GiftPatterns Patterns { get; set; }

        public AnimatedImage Symbol { get; set; }

        public int Model { get; set; }

        public bool IsNegative { get; set; }

        public byte Intensity { get; set; } = 255;

        protected override void OnConnected()
        {
            try
            {
                CreateResources();
            }
            catch
            {
                OnDisconnected();
                CreateResources();
            }
        }

        private CompositionSurfaceBrush CreateSurfaceBrush()
        {
            var surface = ImageSource;
            var logical = surface.DecodedSize.ToVector2();
            var physical = surface.DecodedPhysicalSize.ToVector2();

            var surfaceBrush = BootStrapper.Current.Compositor.CreateSurfaceBrush(surface);
            surfaceBrush.Stretch = CompositionStretch.None;
            surfaceBrush.SnapToPixels = true;
            surfaceBrush.Scale = logical / physical;
            surfaceBrush.HorizontalAlignmentRatio = 0;
            surfaceBrush.VerticalAlignmentRatio = 0;

            var background = Patterns;
            if (background != null)
            {
                var compositor = BootStrapper.Current.Compositor;
                var factor = logical / physical;

                var visual = BootStrapper.Current.Compositor.CreateSpriteVisual();
                visual.Size = background.Size * factor;
                visual.Brush = surfaceBrush;

                var symbolSurfaceBrush = compositor.CreateSurfaceBrush();
                var symbolSurface = compositor.CreateVisualSurface();

                var symbolVisual = ElementComposition.GetElementVisual(Symbol);

                symbolSurface.SourceVisual = symbolVisual;
                symbolSurface.SourceOffset = new Vector2(0, 0);
                symbolSurfaceBrush.HorizontalAlignmentRatio = 0.5f;
                symbolSurfaceBrush.VerticalAlignmentRatio = 0.5f;
                symbolSurfaceBrush.Surface = symbolSurface;
                symbolSurfaceBrush.Stretch = CompositionStretch.Fill;
                symbolSurfaceBrush.BitmapInterpolationMode = CompositionBitmapInterpolationMode.NearestNeighbor;
                symbolSurfaceBrush.SnapToPixels = true;

                var maxWidth = 0f;

                for (int i = 0; i < background.Patterns.Count; i++)
                {
                    if (i == Model)
                    {
                        //continue;
                    }

                    var pattern = background.Patterns[i];
                    var sprite = visual.Compositor.CreateSpriteVisual();
                    sprite.Size = pattern.Size * factor;
                    sprite.Offset = new Vector3(pattern.Offset * factor, 0);
                    sprite.RotationAngle = pattern.RotationAngle;
                    sprite.Brush = symbolSurfaceBrush;

                    visual.Children.InsertAtTop(sprite);

                    maxWidth = Math.Max(maxWidth, sprite.Size.X);
                }

                symbolSurface.SourceSize = new Vector2(maxWidth, maxWidth);
                Symbol.Width = maxWidth;
                Symbol.Height = maxWidth;
                Symbol.FrameSize = new Windows.Foundation.Size(maxWidth, maxWidth);

                var visualSurfaceBrush = compositor.CreateSurfaceBrush();
                var visualSurface = compositor.CreateVisualSurface();

                visualSurface.SourceVisual = visual;
                visualSurface.SourceOffset = new Vector2(0, 0);
                visualSurface.SourceSize = background.Size * factor;
                visualSurfaceBrush.HorizontalAlignmentRatio = 0;
                visualSurfaceBrush.VerticalAlignmentRatio = 0;
                visualSurfaceBrush.Surface = visualSurface;
                visualSurfaceBrush.Stretch = CompositionStretch.None;
                visualSurfaceBrush.BitmapInterpolationMode = CompositionBitmapInterpolationMode.NearestNeighbor;
                visualSurfaceBrush.SnapToPixels = true;

                return visualSurfaceBrush;
            }

            return surfaceBrush;
        }

        private void CreateResources()
        {
            _connected = true;
            _negative = IsNegative;

            if (_recreate || (CompositionBrush == null && ImageSource != null))
            {
                _recreate = false;

                var surfaceBrush = CreateSurfaceBrush();
                var borderEffect = new BorderEffect()
                {
                    Source = new CompositionEffectSourceParameter("Source"),
                    ExtendX = Microsoft.Graphics.Canvas.CanvasEdgeBehavior.Wrap,
                    ExtendY = Microsoft.Graphics.Canvas.CanvasEdgeBehavior.Wrap
                };

                IGraphicsEffect effect;
                IGraphicsEffect blend;
                if (IsNegative)
                {
                    var tintEffect = _tintEffect = new TintEffect
                    {
                        Name = "Tint",
                        Source = borderEffect,
                        Color = Color.FromArgb(Intensity, 0, 0, 0)
                    };

                    blend = null;

                    effect = new ColorMatrixEffect
                    {
                        Source = tintEffect,
                        ColorMatrix = new Matrix5x4
                        {
                            M11 = 1,
                            M22 = 1,
                            M33 = 1,
                            M44 = -1,
                            M54 = 1
                        }
                    };
                }
                else
                {
                    var tintEffect = _tintEffect = new TintEffect
                    {
                        Name = "Tint",
                        Source = borderEffect,
                        Color = Color.FromArgb(Intensity, 0, 0, 0)
                    };

                    effect = blend = new BlendEffect
                    {
                        Background = tintEffect,
                        Foreground = new CompositionEffectSourceParameter("Backdrop"),
                        Mode = BlendEffectMode.Overlay
                    };

                    //effect = borderEffect;
                }

                var borderEffectFactory = BootStrapper.Current.Compositor.CreateEffectFactory(effect, new[] { "Tint.Color" });
                var borderEffectBrush = borderEffectFactory.CreateBrush();
                borderEffectBrush.SetSourceParameter("Source", surfaceBrush);

                if (blend != null)
                {
                    var backdrop = BootStrapper.Current.Compositor.CreateBackdropBrush();
                    borderEffectBrush.SetSourceParameter("Backdrop", backdrop);
                }

                CompositionBrush = borderEffectBrush;
            }
        }

        protected override void OnDisconnected()
        {
            _connected = false;
            _tintEffect = null;

            if (CompositionBrush != null)
            {
                CompositionBrush.Dispose();
                CompositionBrush = null;
            }

            if (ImageSource != null)
            {
                //ImageSource.Dispose();
                //ImageSource = null;
            }
        }

        private bool _connected;
        private bool _negative;
        private bool _recreate;
        private TintEffect _tintEffect;

        public void Update()
        {
            if (_connected && CompositionBrush != null && ImageSource != null)
            {
                if (_negative != IsNegative)
                {
                    _recreate = true;
                    OnConnected();
                    return;
                }

                try
                {
                    if (CompositionBrush is CompositionEffectBrush effectBrush)
                    {
                        effectBrush.SetSourceParameter("Source", CreateSurfaceBrush());

                        if (_tintEffect != null)
                        {
                            effectBrush.Properties.InsertColor("Tint.Color", Color.FromArgb(Intensity, 0, 0, 0));
                        }
                    }
                }
                catch
                {
                    _recreate = true;
                    OnConnected();
                }
            }
        }
    }
}
