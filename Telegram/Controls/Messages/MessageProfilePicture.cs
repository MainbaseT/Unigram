//
// Copyright (c) Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using Telegram.Common;
using Telegram.Td.Api;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
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

        private int _fontSize;
        private bool _glyph;

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
                var presentation = new MessageProfilePicturePresentation(source, Size);

                if (_presenter == null || _presenter.Presentation != presentation)
                {
                    _presenter?.Unload(this);
                    _presenter = MessageProfilePictureLoader.Current.GetOrCreate(presentation);
                    _presenter.Load(this);
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
                    Load();
                }
            }
        }

        private void Invalidate(object newValue, ProfilePictureShape shape)
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

            UpdateCornerRadius();
            UpdateFontSize();
        }

        private void UpdateCornerRadius()
        {
            if (LayoutRoot == null || Size == 0)
            {
                return;
            }

            LayoutRoot.CornerRadius = new CornerRadius(Size / 2d);
        }

        private void UpdateFontSize()
        {
            if (Initials == null || double.IsNaN(Width))
            {
                return;
            }

            var fontSize = Width switch
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
                Template,
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
            private ProfilePictureShape _shape;

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

                _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            }

            public MessageProfilePicturePresentation Presentation => _presentation;

            public void Load(MessageProfilePicture picture)
            {
                _pictures.Add(picture);
                picture.Invalidate(_source, _presentation.Source.Shape);

                Invalidate(State.Download);
            }

            private void Invalidate(State state)
            {
                var source = _presentation.Source;
                if (source is ProfilePictureSourcePhoto sourcePhoto && _fileId != sourcePhoto.Photo.Id)
                {
                    _fileId = sourcePhoto.Photo.Id;
                    UpdateManager.Unsubscribe(this, ref _fileToken);

                    Invalidate(sourcePhoto, _presentation.Size, state);
                }
                else if (source is ProfilePictureSourceText sourceText)
                {
                    _fileId = null;
                    UpdateManager.Unsubscribe(this, ref _fileToken);

                    Invalidate(sourceText, sourceText.Shape);
                }
            }

            private void Invalidate(ProfilePictureSourcePhoto photo, int side, State state = State.Download)
            {
                if (photo.Photo.Local.IsDownloadingCompleted)
                {
                    _controller.Bitmap(photo.Photo.Local.Path, side, side, photo.Id);
                    Invalidate(_texture, photo.Shape);

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
                    Invalidate(_texture, photo.Shape);

                    return;
                }

                _controller.Recycle();
                Invalidate(photo.Text, photo.Shape);
            }

            private void Invalidate(object value, ProfilePictureShape shape)
            {
                if (_source != value || _shape != shape)
                {
                    _source = value;
                    _shape = shape;

                    foreach (var picture in _pictures)
                    {
                        picture.Invalidate(value, shape);
                    }
                }
            }

            private void UpdateFile(object target, File file)
            {
                _dispatcherQueue.TryEnqueue(() => Invalidate(State.Update));
            }

            public void Unload(MessageProfilePicture picture)
            {
                _pictures.Remove(picture);
                picture.Invalidate(null, _shape);

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
