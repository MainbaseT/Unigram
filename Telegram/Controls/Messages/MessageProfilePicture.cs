//
// Copyright (c) Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using Telegram.Common;
using Telegram.Controls.Media;
using Telegram.Services;
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
            if (source != null && IsLoaded)
            {
                var presentation = new MessageProfilePicturePresentation(source, Size);

                if (_presenter == null || _presenter.Presentation != presentation)
                {
                    _presenter?.Unload(this);
                    _presenter = MessageProfilePictureLoader.Current.GetOrCreate(presentation);
                }

                _presenter.Load(this);
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

        #region Source

        public ProfilePictureSource Source
        {
            get => (ProfilePictureSource)GetValue(SourceProperty);
            set => SetValue(SourceProperty, value);
        }

        public static readonly DependencyProperty SourceProperty =
            DependencyProperty.Register("Source", typeof(ProfilePictureSource), typeof(MessageProfilePicture), new PropertyMetadata(null, OnPropertyChanged));

        #endregion

        #region Size

        public int Size
        {
            get { return (int)GetValue(SizeProperty); }
            set { SetValue(SizeProperty, value); }
        }

        public static readonly DependencyProperty SizeProperty =
            DependencyProperty.Register("Size", typeof(int), typeof(MessageProfilePicture), new PropertyMetadata(0, OnPropertyChanged));

        #endregion

        private static void OnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (e.Property == SizeProperty)
            {
                ((MessageProfilePicture)d).InvalidateMeasure();
            }

            ((MessageProfilePicture)d).Load();
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
                picture.Invalidate(_source);

                Invalidate(State.Download);
            }

            private void Invalidate(State state)
            {
                var source = _presentation.Source;
                if (source is ProfilePictureSourceChat sourceChat)
                {
                    SetChat(source.ClientService, sourceChat.Chat, sourceChat.Chat.Photo?.Small, _presentation.Size, state);
                }
                else if (source is ProfilePictureSourceUser sourceUser)
                {
                    SetUser(source.ClientService, sourceUser.User, sourceUser.User.ProfilePhoto?.Small, _presentation.Size, state);
                }
                else if (source is ProfilePictureSourceText sourceText)
                {
                    // Local handling within MessageProfilePicture would be better...
                    UpdateManager.Unsubscribe(this, ref _fileToken);

                    _fileId = null;
                    Source = sourceText;
                }
            }

            private void SetChat(IClientService clientService, Chat chat, File file, int side, State state = State.Download)
            {
                var fileId = file?.Id ?? 0;
                if (fileId != _fileId || /*Source == null ||*/ state != State.Download)
                {
                    UpdateManager.Unsubscribe(this, ref _fileToken);

                    _fileId = file?.Id;
                    Source = GetChat(clientService, chat, file, side, out var shape, state);
                }
            }

            private object GetChat(IClientService clientService, Chat chat, File file, int side, out ProfilePictureShape shape, State state = State.Download)
            {
                // TODO: this method may throw a NullReferenceException in some conditions

                shape = ProfilePictureShape.Ellipse;

                if (chat.Id == clientService.Options.MyId)
                {
                    _controller?.Recycle();
                    return ProfilePictureSourceText.GetGlyph(Icons.BookmarkFilled, 5);
                }
                else if (chat.Id == clientService.Options.RepliesBotChatId)
                {
                    _controller?.Recycle();
                    return ProfilePictureSourceText.GetGlyph(Icons.ArrowReplyFilled, 5);
                }

                //if (IsShapeEnabled && clientService.TryGetSupergroup(chat, out Supergroup supergroup))
                //{
                //    if (supergroup.IsForum)
                //    {
                //        shape = ProfilePictureShape.Superellipse;
                //    }
                //    else if (supergroup.IsDirectMessagesGroup)
                //    {
                //        shape = ProfilePictureShape.Tail;
                //    }
                //}

                if (file != null)
                {
                    if (file.Local.IsDownloadingCompleted)
                    {
                        _controller.Bitmap(file.Local.Path, side, side, chat.Id);

                        return _texture;
                    }
                    else
                    {
                        if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive && state != State.Update)
                        {
                            clientService.DownloadFile(file.Id, 1);
                        }

                        UpdateManager.Subscribe(this, clientService, file, ref _fileToken, UpdateFile, true);
                    }

                    var minithumbnail = chat.Photo?.Minithumbnail;
                    if (minithumbnail != null)
                    {
                        _controller.Blur(minithumbnail.Data, 3, chat.Id);

                        return _texture;
                    }
                }

                _controller.Recycle();

                if (clientService.TryGetUser(chat, out User user))
                {
                    if (user.Type is UserTypeDeleted)
                    {
                        return ProfilePictureSourceText.GetGlyph(Icons.GhostFilled, long.MinValue);
                    }

                    return ProfilePictureSourceText.GetUser(clientService, user);
                }

                return ProfilePictureSourceText.GetChat(clientService, chat);
            }

            private void SetUser(IClientService clientService, User user, File file, int side, State state = State.Download)
            {
                var fileId = file?.Id ?? 0;
                if (fileId != _fileId || /*Source == null ||*/ state != State.Download)
                {
                    UpdateManager.Unsubscribe(this, ref _fileToken);

                    _fileId = fileId;
                    Source = GetUser(clientService, user, file, side, state);
                }
            }

            private object GetUser(IClientService clientService, User user, File file, int side, State state = State.Download)
            {
                if (file != null)
                {
                    if (file.Local.IsDownloadingCompleted)
                    {
                        _controller.Bitmap(file.Local.Path, side, side, user.Id);

                        return _texture;
                    }
                    else
                    {
                        if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive && state != State.Update)
                        {
                            clientService.DownloadFile(file.Id, 1);
                        }

                        UpdateManager.Subscribe(this, clientService, file, ref _fileToken, UpdateFile, true);
                    }

                    var minithumbnail = user.ProfilePhoto?.Minithumbnail;
                    if (minithumbnail != null)
                    {
                        _controller.Blur(minithumbnail.Data, 3, user.Id);

                        return _texture;
                    }
                }

                _controller.Recycle();

                if (user.Type is UserTypeDeleted)
                {
                    return ProfilePictureSourceText.GetGlyph(Icons.GhostFilled, long.MinValue);
                }

                return ProfilePictureSourceText.GetUser(clientService, user);
            }

            public object Source
            {
                get => _source;
                set
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
            }

            private void UpdateFile(object target, File file)
            {
                _dispatcherQueue.TryEnqueue(() => Invalidate(State.Update));
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
