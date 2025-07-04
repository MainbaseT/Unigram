//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Numerics;
using Telegram.Common;
using Telegram.Native;
using Telegram.Streams;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;

namespace Telegram.Controls
{
    public partial class ProfilePatternCover : Control
    {
        private readonly static OrbitGenerator.Position[] _positions = new[]
        {
            new OrbitGenerator.Position(100, -1.57079637f, 0.9593789f),
            new OrbitGenerator.Position(100, -0.5235988f, 0.7199175f),
            new OrbitGenerator.Position(100, 0.5235988f, 0.9537933f),
            new OrbitGenerator.Position(100, 1.57079637f, 0.7504041f), // Hidden by title
            new OrbitGenerator.Position(100, 2.61799383f, 0.968893051f),
            new OrbitGenerator.Position(100, 3.66519117f, 0.875341058f),
            //new ProfileGiftsCover.PositionGenerator.Position(140, -1.04719758f, 0.797602057f), // Out of bounds top
            new OrbitGenerator.Position(140, 0f, 0.7811355f),
            new OrbitGenerator.Position(140, 1.04719758f, 0.788561344f), // Hidden by subtitle
            new OrbitGenerator.Position(140, 2.09439516f, 0.9652828f), // Hidden by subtitle
            new OrbitGenerator.Position(140, 3.1415925f, 0.7321501f),
            //new ProfileGiftsCover.PositionGenerator.Position(140, 4.18879f, 0.7033648f), // Out of bounds top
            //new ProfileGiftsCover.PositionGenerator.Position(180, -1.57079637f, 0.720738232f), // Out of bounds top
            new OrbitGenerator.Position(180, -0.5235988f, 0.7289108f),
            new OrbitGenerator.Position(180, 0.5235988f, 0.7759581f),
            //new ProfileGiftsCover.PositionGenerator.Position(180, 1.57079637f, 0.718606f), // Out of bounds bottom
            new OrbitGenerator.Position(180, 2.61799383f, 0.867521644f),
            new OrbitGenerator.Position(180, 3.66519117f, 0.716817141f),
            //new ProfileGiftsCover.PositionGenerator.Position(220, -1.04719758f, 0.870925665f), // Out of bounds top
            new OrbitGenerator.Position(220, 0f, 0.9330126f),
            //new ProfileGiftsCover.PositionGenerator.Position(220, 1.04719758f, 0.8217091f), // Out of bounds bottom
            //new ProfileGiftsCover.PositionGenerator.Position(220, 2.09439516f, 0.7188775f), // Out of bounds bottom
            new OrbitGenerator.Position(220, 3.1415925f, 0.8571975f),
            //new ProfileGiftsCover.PositionGenerator.Position(220, 4.18879f, 0.9217857f), // Out of bounds top
        };

        public ProfilePatternCover()
        {
            DefaultStyleKey = typeof(ProfilePatternCover);
        }

        protected override void OnApplyTemplate()
        {
            var animated = GetTemplateChild("Animated") as AnimatedImage;
            var layoutRoot = GetTemplateChild("LayoutRoot") as Border;

            animated.Ready += OnReady;

            var visual = ElementComposition.GetElementVisual(animated);
            var compositor = visual.Compositor;

            var avatarSize = new Vector2(140, 140);
            var newSize = new Vector2(1000 + 36, 320);
            var centerFrame = new RectangleF((-72 + newSize.X - avatarSize.X) / 2f, (-36 + 204 - avatarSize.Y) / 2f, avatarSize.X, avatarSize.Y);

            // Create a VisualSurface positioned at the same location as this control and feed that
            // through the color effect.
            var surfaceBrush = compositor.CreateSurfaceBrush();
            var surface = compositor.CreateVisualSurface();

            // Select the source visual and the offset/size of this control in that element's space.
            surface.SourceVisual = visual;
            surface.SourceOffset = new Vector2(0, 0);
            surface.SourceSize = new Vector2(37, 37);
            surfaceBrush.HorizontalAlignmentRatio = 0.5f;
            surfaceBrush.VerticalAlignmentRatio = 0.5f;
            surfaceBrush.Surface = surface;
            surfaceBrush.Stretch = CompositionStretch.Fill;
            surfaceBrush.BitmapInterpolationMode = CompositionBitmapInterpolationMode.NearestNeighbor;
            surfaceBrush.SnapToPixels = true;

            var container = compositor.CreateContainerVisual();
            container.Size = new Vector2(1000, 320);

            for (int i = 0; i < _positions.Length; i++)
            {
                OrbitGenerator.Position iconPosition = _positions[i];

                var itemDistanceFraction = 0.6f - Math.Max(0.0f, Math.Min(0.5f, (iconPosition.Distance - avatarSize.X / 2.0f) / 74));
                var itemScaleFraction = OrbitGenerator.PatternScaleValueAt(fraction: 0, t: itemDistanceFraction, reverse: false);

                var toAngle = MathF.PI * 0.18f;
                var centerPosition = new OrbitGenerator.Position(distance: 0.0f, angle: iconPosition.Angle + toAngle, scale: iconPosition.Scale);
                var effectivePosition = OrbitGenerator.InterpolatePosition(from: iconPosition, to: centerPosition, t: itemScaleFraction);
                var effectiveAngle = toAngle * itemScaleFraction;

                var absolutePosition = effectivePosition.GetAbsolutePosition(centerFrame.Center);

                var redirect = compositor.CreateSpriteVisual();
                redirect.Size = new Vector2(MathF.Floor((IsSmall ? 32 : 36) * (iconPosition.Scale * (1.0f - itemScaleFraction))));
                redirect.Offset = new Vector3(absolutePosition, 0);
                //redirect.Scale = new Vector3(clone.Scale * (1.0f - itemScaleFraction));
                redirect.RotationAngle = 0; // effectiveAngle;
                redirect.CenterPoint = new Vector3(redirect.Size / 2, 0);
                redirect.Opacity = 1.0f - itemScaleFraction;
                redirect.Brush = surfaceBrush;

                container.Children.InsertAtTop(redirect);
            }

            ElementCompositionPreview.SetElementChildVisual(layoutRoot, container);
        }

        private void OnReady(object sender, EventArgs e)
        {
            var layoutRoot = GetTemplateChild("LayoutRoot") as Border;
            var container = ElementCompositionPreview.GetElementChildVisual(layoutRoot) as ContainerVisual;

            var scale = container.Compositor.CreateVector3KeyFrameAnimation();
            scale.InsertKeyFrame(0, Vector3.Zero);
            scale.InsertKeyFrame(1, Vector3.One);

            var batch = container.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);

            foreach (var redirect in container.Children)
            {
                redirect.StartAnimation("Scale", scale);
            }

            batch.End();
        }

        public void Update(float avatarTransitionFraction)
        {
            var layoutRoot = GetTemplateChild("LayoutRoot") as Border;
            var container = ElementCompositionPreview.GetElementChildVisual(layoutRoot) as ContainerVisual;

            var avatarSize = new Vector2(140, 140);
            var newSize = new Vector2(1000 + 36, 320);
            var centerFrame = new RectangleF((-72 + newSize.X - avatarSize.X) / 2f, (-36 + 204 - avatarSize.Y) / 2f, avatarSize.X, avatarSize.Y);

            //var test = Generate2(0);
            //var builder = new StringBuilder();

            //foreach (var point in test)
            //{
            //    builder.AppendFormat("new ProfileGiftsCover.PositionGenerator.Position({0:R}, {1:R}f, {2:R}f),\r\n", point.Distance, point.Angle, point.Scale);
            //}

            //var yolo = builder.ToString();

            var i = 0;

            foreach (var redirect in container.Children)
            {
                if (_positions.Length <= i)
                {
                    redirect.Opacity = 0;
                    continue;
                }

                var iconPosition = _positions[i++];
                var itemDistanceFraction = 0.6f - Math.Max(0.0f, Math.Min(0.5f, (iconPosition.Distance - avatarSize.X / 2.0f) / 74));
                var itemScaleFraction = OrbitGenerator.PatternScaleValueAt(fraction: Math.Min(1.0f, avatarTransitionFraction * 1.33f), t: itemDistanceFraction, reverse: false);

                var toAngle = MathF.PI * 0.18f;
                var centerPosition = new OrbitGenerator.Position(distance: 0.0f, angle: iconPosition.Angle + toAngle, scale: iconPosition.Scale);
                var effectivePosition = OrbitGenerator.InterpolatePosition(from: iconPosition, to: centerPosition, t: itemScaleFraction);
                var effectiveAngle = toAngle * itemScaleFraction;

                var absolutePosition = effectivePosition.GetAbsolutePosition(centerFrame.Center);

                //redirect.Size = new Vector2(36, 36);
                redirect.Size = new Vector2(MathF.Floor((IsSmall ? 32 : 36) * (iconPosition.Scale * (1.0f - itemScaleFraction))));
                redirect.Offset = new Vector3(absolutePosition, 0);
                //redirect.Scale = new Vector3(iconPosition.Scale * (1.0f - itemScaleFraction));
                redirect.RotationAngle = 0; // effectiveAngle;
                redirect.Opacity = 1.0f - itemScaleFraction;
            }
        }

        private IList<OrbitGenerator.Position> Generate(float avatarTransitionFraction)
        {
            var results = new List<OrbitGenerator.Position>();

            var avatarPatternFrame = new Vector2(1000 - 36, 86 + 48 * 2);
            //var avatarPatternFrame = new Vector2(1000 - 36, 24 + 140 + 24);

            var lokiRng = new LokiRng(seed0: 123, seed1: 0, seed2: 0);
            var numRows = 4;

            for (int row = 0; row < numRows; row++)
            {
                int avatarPatternCount = 7;
                float avatarPatternAngleSpan = MathF.PI * 2.0f / (avatarPatternCount - 1f);

                for (int i = 0; i < avatarPatternCount - 1; i++)
                {
                    float baseItemDistance;
                    float itemDistanceFraction;
                    float itemScaleFraction;
                    float itemDistance;

                    if (IsSmall)
                    {
                        baseItemDistance = 72.0f + row * 28.0f;

                        itemDistanceFraction = MathF.Max(0.0f, MathF.Min(1.0f, baseItemDistance / 140.0f));
                        itemScaleFraction = PatternScaleValueAt(fraction: avatarTransitionFraction, t: itemDistanceFraction, reverse: false);
                        itemDistance = baseItemDistance * (1.0f - itemScaleFraction) + 20.0f * itemScaleFraction;
                    }
                    else
                    {
                        baseItemDistance = 100.0f + row * 40.0f;

                        itemDistanceFraction = MathF.Max(0.0f, MathF.Min(1.0f, baseItemDistance / 196.0f));
                        itemScaleFraction = PatternScaleValueAt(fraction: avatarTransitionFraction, t: itemDistanceFraction, reverse: false);
                        itemDistance = baseItemDistance * (1.0f - itemScaleFraction) + 28.0f * itemScaleFraction;
                    }


                    float itemAngle = -MathF.PI * 0.5f + i * avatarPatternAngleSpan;

                    if (row % 2 != 0)
                    {
                        itemAngle += avatarPatternAngleSpan * 0.5f;
                    }

                    Vector2 itemPosition = new Vector2(avatarPatternFrame.X * 0.5f + MathF.Cos(itemAngle) * itemDistance, avatarPatternFrame.Y * 0.5f + MathF.Sin(itemAngle) * itemDistance);

                    float itemScale = 0.7f + lokiRng.Next() * (1.0f - 0.7f);
                    float itemSize = MathF.Floor((IsSmall ? 32 : 36) * itemScale);

                    //results.Add(new Vector4(itemPosition.X, itemPosition.Y, itemSize, 1.0f - itemScaleFraction));
                    results.Add(new OrbitGenerator.Position(itemDistance, itemAngle, itemScale));
                }
            }

            return results;
        }

        private float WindowFunction(float t)
        {
            return BezierPoint.Calculate(0.6f, 0.0f, 0.4f, 1.0f, t);
        }

        private float PatternScaleValueAt(float fraction, float t, bool reverse)
        {
            float windowSize = 0.8f;

            float effectiveT;
            float windowStartOffset;
            float windowEndOffset;
            if (reverse)
            {
                effectiveT = 1.0f - t;
                windowStartOffset = 1.0f;
                windowEndOffset = -windowSize;
            }
            else
            {
                effectiveT = t;
                windowStartOffset = -0.3f;
                windowEndOffset = 1.0f;
            }

            float windowPosition = (1.0f - fraction) * windowStartOffset + fraction * windowEndOffset;
            float windowT = MathF.Max(0.0f, MathF.Min(windowSize, effectiveT - windowPosition)) / windowSize;
            float localT = 1.0f - WindowFunction(t: windowT);

            return localT;
        }

        public bool IsSmall { get; set; } = false;

        #region Source

        public AnimatedImageSource Source
        {
            get { return (AnimatedImageSource)GetValue(SourceProperty); }
            set { SetValue(SourceProperty, value); }
        }

        public static readonly DependencyProperty SourceProperty =
            DependencyProperty.Register("Source", typeof(AnimatedImageSource), typeof(ProfilePatternCover), new PropertyMetadata(null));

        #endregion
    }
}
