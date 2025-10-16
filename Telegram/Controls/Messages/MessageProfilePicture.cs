//
// Copyright (c) Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.Collections.Generic;
using System.Numerics;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Td.Api;
using Windows.Foundation;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls
{
    // This is an extended version of ProfilePicture optimized for chat history.
    // The logic kind of matches the one from AnimatedImage, but simplified as this all run on a single thread.
    // Different MessageProfilePictures can be bound to the same MessageProfilePicturePresenter
    // to reduce the amount of loaded textures and workload in general. This also guarantees the control
    // to be immediately ready as soon as its loaded (if the presenter exists) without the delays caused by
    // opening the photo file, decoding it and loading it into a texture.
    // The new implementation tries to get rid of some non-great design from the original implementation,
    // by replacing Set* methods with a Source property that accepts ProfilePictureSource objects.
    // The long term plan is to replace ProfilePicture with this implementation.
    // TODO: support profile photo shapes:
    // These will be passed by MessageProfilePicturePresenter to MessageProfilePicture.Invalidate
    // + ProfilePictureSourceTexture(ImageBrush Texture, ProfilePictureShape Shape);
    // * ProfilePictureSourceText(..., ProfilePictureShape Shape);
    public class MessageProfilePicture : ControlEx
    {
        private Border LayoutRoot;
        private LinearGradientBrush Gradient;
        private TextBlock Initials;

        private ProfilePictureShape _appliedShape;
        private int _appliedSize;

        private int _fontSize;
        private bool _glyph;
        private bool _tail;

        private bool _templateApplied;

        private MessageProfilePicturePresenter _presenter;

        public MessageProfilePicture()
        {
            DefaultStyleKey = typeof(MessageProfilePicture);

            Connected += OnLoaded;
            Disconnected += OnUnloaded;
        }

        protected override void OnApplyTemplate()
        {
            LayoutRoot = GetTemplateChild(nameof(LayoutRoot)) as Border;
            Initials = GetTemplateChild(nameof(Initials)) as TextBlock;

            Gradient = new LinearGradientBrush();
            Gradient.StartPoint = new Windows.Foundation.Point(0, 0);
            Gradient.EndPoint = new Windows.Foundation.Point(0, 1);
            Gradient.GradientStops.Add(new GradientStop { Offset = 0 });
            Gradient.GradientStops.Add(new GradientStop { Offset = 1 });

            _templateApplied = true;
            InvalidateShape();

            base.OnApplyTemplate();
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            availableSize = new Size(Size, Size);

            LayoutRoot.Measure(availableSize);
            return availableSize;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            LayoutRoot.Arrange(new Rect(0, 0, finalSize.Width, finalSize.Height));
            return finalSize;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Load();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Unload();
        }

        private void Load()
        {
            var source = Source;
            if (source != null && IsConnected)
            {
                if (source is ProfilePictureSourceText)
                {
                    _presenter?.Unload(this);
                    _presenter = null;

                    Invalidate(source);
                }
                else
                {
                    var presentation = new MessageProfilePicturePresentation(source, Size);

                    if (_presenter == null || _presenter.Presentation != presentation)
                    {
                        _presenter?.Unload(this);
                        _presenter = MessageProfilePictureLoader.Current.GetOrCreate(presentation);
                        _presenter.Load(this);
                    }
                }
            }
            else if (source == null)
            {
                _presenter?.Unload(this);
                _presenter = null;
            }
        }

        private void Unload()
        {
            _presenter?.Unload(this);
            _presenter = null;
        }

        private ProfilePictureSource _source;
        public ProfilePictureSource Source
        {
            get => _source;
            set
            {
                if (_source != value)
                {
                    _source = value;
                    InvalidateShape();
                    Load();
                }
            }
        }

        private int _size;
        public int Size
        {
            get => _size;
            set
            {
                if (_size != value)
                {
                    _size = value;
                    InvalidateMeasure();
                    InvalidateShape();
                    Load();
                }
            }
        }

        private ProfilePictureShape _shape = ProfilePictureShape.Auto;
        public ProfilePictureShape Shape
        {
            get => _shape;
            set
            {
                if (_shape != value)
                {
                    _shape = value;
                    InvalidateShape();
                }
            }
        }

        public ProfilePictureShape CalculatedShape
        {
            get
            {
                if (_shape == ProfilePictureShape.Auto)
                {
                    if (_source != null)
                    {
                        return _source.Shape;
                    }

                    return ProfilePictureShape.Ellipse;
                }

                return _shape;
            }
        }

        private void Invalidate(object newValue)
        {
            if (LayoutRoot == null)
            {
                return;
            }

            if (newValue is ProfilePictureSourceText text)
            {
                Gradient.GradientStops[0].Color = text.TopColor;
                Gradient.GradientStops[1].Color = text.BottomColor;

                LayoutRoot.Background = Gradient;

                Initials.Visibility = Visibility.Visible;
                Initials.Text = text.Initials;

                if (_glyph != text.IsGlyph)
                {
                    _glyph = text.IsGlyph;
                    Initials.Margin = new Thickness(0, 1, 0, _glyph ? 0 : 2);
                }

                InvalidateFontSize();
            }
            else if (newValue is ImageBrush texture)
            {
                LayoutRoot.Background = texture;
                Initials.Visibility = Visibility.Collapsed;
            }
            else
            {
                LayoutRoot.Background = null;
                Initials.Visibility = Visibility.Collapsed;
            }
        }

        private void InvalidateShape()
        {
            var shape = CalculatedShape;
            var size = Size;

            if (shape == _appliedShape && size == _appliedSize)
            {
                return;
            }

            if (LayoutRoot == null || size == 0)
            {
                return;
            }

            _appliedShape = shape;
            _appliedSize = size;

            if (shape == ProfilePictureShape.Tail)
            {
                _tail = true;

                static CompositionPath GetTail(float radius)
                {
                    CanvasGeometry result;
                    using (var builder = new CanvasPathBuilder(null))
                    {
                        var cy = radius;
                        var cx = radius;
                        var r = radius;

                        float b = cy + r;
                        float x = r / 81.0f;

                        float startAngle = -180 * (MathF.PI / 180);
                        float sweepAngle = 270 * (MathF.PI / 180);

                        float x1 = cx + MathF.Cos(startAngle) * r;
                        float y1 = cy + MathF.Sin(startAngle) * r;

                        float x2 = cx + MathF.Cos(startAngle + sweepAngle) * r;
                        float y2 = cy + MathF.Sin(startAngle + sweepAngle) * r;

                        builder.BeginFigure(new Vector2(x1, y1));
                        builder.AddArc(new Vector2(x2, y2), r, r, 0, CanvasSweepDirection.Clockwise, CanvasArcSize.Large);
                        builder.AddCubicBezier(new Vector2(cx - 13 * x, b), new Vector2(cx - 25 * x, b - 3 * x), new Vector2(cx - 36f * x, b - 8.42f * x));
                        builder.AddCubicBezier(new Vector2(cx - 52 * x, b - x), new Vector2(cx - 56.5f * x, b - x), new Vector2(cx - 78.02f * x, b - x));
                        builder.AddCubicBezier(new Vector2(cx - 80 * x, b - x), new Vector2(cx - 81 * x, b - 3 * x), new Vector2(cx - 79.52f * x, b - 4.5f * x));
                        builder.AddCubicBezier(new Vector2(cx - 78 * x, b - 6 * x), new Vector2(cx - 63.73f * x, b - 15 * x), new Vector2(cx - 63.73f * x, b - 31 * x));
                        builder.AddCubicBezier(new Vector2(cx - 74.5f * x, b - 44.75f * x), new Vector2(cx - r, cy + 18.87f * x), new Vector2(cx - r, cy));
                        builder.EndFigure(CanvasFigureLoop.Closed);
                        result = CanvasGeometry.CreatePath(builder);
                    }
                    return new CompositionPath(result);
                }

                var compositor = BootStrapper.Current.Compositor;

                var polygon = compositor.CreatePathGeometry();
                polygon.Path = GetTail(Size / 2f);

                var visual = ElementComposition.GetElementVisual(this);
                visual.Clip = compositor.CreateGeometricClip(polygon);
            }
            else if (_tail)
            {
                _tail = false;

                var visual = ElementComposition.GetElementVisual(this);
                visual.Clip = null;
            }

            LayoutRoot.CornerRadius = new CornerRadius(shape switch
            {
                ProfilePictureShape.Superellipse => size / 4d,
                ProfilePictureShape.Ellipse => size / 2d,
                _ => 0
            });
        }

        private void InvalidateFontSize()
        {
            if (Initials == null || Size == 0)
            {
                return;
            }

            var fontSize = Size switch
            {
                < 20 => 10,
                < 30 => 12,
                < 36 => 14,
                < 48 => 16,
                < 64 => 20,
                < 96 => 24,
                < 120 => 32,
                _ => 64
            };

            if (_fontSize != fontSize)
            {
                _fontSize = fontSize;
                Initials.FontSize = fontSize;
            }
        }

        private record MessageProfilePicturePresentation(ProfilePictureSource Source, int Size);

        private class MessageProfilePicturePresenter
        {
            public enum State
            {
                Download,
                Update
            }

            private readonly MessageProfilePictureLoader _loader;
            private readonly MessageProfilePicturePresentation _presentation;

            private readonly ImageBrush _texture;
            private readonly ThumbnailController _controller;

            private readonly DispatcherQueue _dispatcherQueue;

            private readonly HashSet<MessageProfilePicture> _pictures = new();

            private int? _fileId;
            private long _fileToken;

            private object _source;

            public MessageProfilePicturePresenter(MessageProfilePictureLoader loader, MessageProfilePicturePresentation presentation)
            {
                _loader = loader;
                _presentation = presentation;

                _texture = new ImageBrush
                {
                    Stretch = Stretch.UniformToFill,
                    AlignmentX = AlignmentX.Center,
                    AlignmentY = AlignmentY.Center
                };

                _controller = new ThumbnailController(_texture);

                _dispatcherQueue = loader.DispatcherQueue;
            }

            public MessageProfilePicturePresentation Presentation => _presentation;

            public void Load(MessageProfilePicture picture)
            {
                _pictures.Add(picture);
                picture.Invalidate(_source);

                Load(State.Download);
            }

            private void Load(State state)
            {
                var source = _presentation.Source;
                if (source is ProfilePictureSourcePhoto sourcePhoto && (_fileId != sourcePhoto.Photo.Id || state != State.Download))
                {
                    _fileId = sourcePhoto.Photo.Id;
                    UpdateManager.Unsubscribe(this, ref _fileToken);

                    Invalidate(sourcePhoto, _presentation.Size, state);
                }
                else if (source is ProfilePictureSourceText sourceText)
                {
                    _fileId = null;
                    UpdateManager.Unsubscribe(this, ref _fileToken);

                    Invalidate(sourceText);
                }
            }

            private void Invalidate(ProfilePictureSourcePhoto photo, int side, State state = State.Download)
            {
                if (photo.Photo.Local.IsDownloadingCompleted)
                {
                    _controller.Bitmap(photo.Photo.Local.Path, side, side, photo.Id);
                    Invalidate(_texture);

                    return;
                }
                else
                {
                    if (photo.Photo.Local.CanBeDownloaded && !photo.Photo.Local.IsDownloadingActive && state != State.Update)
                    {
                        photo.ClientService.DownloadFile(photo.Photo.Id, 1);
                    }

                    UpdateManager.Subscribe(this, photo.ClientService, photo.Photo, ref _fileToken, UpdateFile, true);
                }

                if (photo.Minithumbnail != null)
                {
                    _controller.Blur(photo.Minithumbnail.Data, 3, photo.Id);
                    Invalidate(_texture);

                    return;
                }

                _controller.Recycle();
                Invalidate(photo.Text);
            }

            private void Invalidate(object value)
            {
                if (_source != value)
                {
                    _source = value;

                    foreach (var picture in _pictures)
                    {
                        picture.Invalidate(value);
                    }
                }
            }

            private void UpdateFile(object target, File file)
            {
                _dispatcherQueue.TryEnqueue(() => Load(State.Update));
            }

            public void Unload(MessageProfilePicture picture)
            {
                _pictures.Remove(picture);
                picture.Invalidate(null);

                if (_pictures.Empty())
                {
                    UpdateManager.Unsubscribe(this, ref _fileToken);

                    _controller.Recycle();
                    _loader.Unload(this);
                }
            }
        }

        private class MessageProfilePictureLoader
        {
            [ThreadStatic]
            private static MessageProfilePictureLoader _current;
            public static MessageProfilePictureLoader Current => _current ??= new();

            private readonly Dictionary<MessageProfilePicturePresentation, MessageProfilePicturePresenter> _presenters = new();
            private readonly DispatcherQueue _dispatcherQueue;

            private MessageProfilePictureLoader()
            {
                _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            }

            public DispatcherQueue DispatcherQueue => _dispatcherQueue;

            public MessageProfilePicturePresenter GetOrCreate(MessageProfilePicturePresentation presentation)
            {
                if (_presenters.TryGetValue(presentation, out var presenter))
                {
                    return presenter;
                }

                presenter = new MessageProfilePicturePresenter(this, presentation);
                _presenters.Add(presentation, presenter);

                return presenter;
            }

            public void Unload(MessageProfilePicturePresenter presenter)
            {
                _presenters.Remove(presenter.Presentation);
            }
        }
    }
}
