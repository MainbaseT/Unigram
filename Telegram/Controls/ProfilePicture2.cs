//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using Telegram.Common;
using Telegram.Controls.Media;
using Telegram.Converters;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls
{
    public enum ProfilePictureShape
    {
        None,
        Ellipse,
        Superellipse,
        Tail,
        Auto
    }

    public partial class ProfilePicture2 : Control
    {
        public enum State
        {
            Template,
            Download,
            Update
        }

        private long _fileToken;
        private int? _fileId;
        private long? _referenceId;

        private int _fontSize;
        private bool _glyph;

        private bool _tail;

        private object _parameters;

        private ThumbnailController _controller;

        private Border LayoutRoot;
        private ImageBrush Texture;
        private LinearGradientBrush Gradient;

        // TODO: consider lazy loading
        private TextBlock Initials;

        private bool _templateApplied;

        public ProfilePicture2()
        {
            DefaultStyleKey = typeof(ProfilePicture2);
        }

        protected override void OnApplyTemplate()
        {
            LayoutRoot = GetTemplateChild(nameof(LayoutRoot)) as Border;

            Initials = GetTemplateChild(nameof(Initials)) as TextBlock;
            Texture = GetTemplateChild(nameof(Texture)) as ImageBrush;

            Gradient = new LinearGradientBrush();
            Gradient.StartPoint = new Windows.Foundation.Point(0, 0);
            Gradient.EndPoint = new Windows.Foundation.Point(0, 1);
            Gradient.GradientStops.Add(new GradientStop { Offset = 0 });
            Gradient.GradientStops.Add(new GradientStop { Offset = 1 });

            //UpdateCornerRadius();
            //UpdateFontSize();

            _templateApplied = true;

            ApplyParameters(State.Template);

            base.OnApplyTemplate();
        }

        private void ApplyParameters(State state)
        {
            if (_parameters is ChatParameters chat)
            {
                SetChat(chat.ClientService, chat.Chat, chat.Side, state);
            }
            else if (_parameters is UserParameters user)
            {
                SetUser(user.ClientService, user.User, user.Side, state);
            }
            else if (_parameters is ChatInviteParameters chatInvite)
            {
                SetChat(chatInvite.ClientService, chatInvite.Chat, chatInvite.Side, state);
            }
            else if (_parameters is ChatPhotoParameters chatPhoto)
            {
                SetChatPhoto(chatPhoto.ClientService, chatPhoto.Photo, chatPhoto.Side, state);
            }
            else if (_parameters is StoryParameters story)
            {
                SetStory(story.ClientService, story.Story, story.Side, state);
            }
            else if (state != State.Update)
            {
                OnSourceChanged(Source);
            }
        }

        private void UpdateCornerRadius()
        {
            if (LayoutRoot == null || double.IsNaN(Width))
            {
                return;
            }

            var shape = Shape;
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
                polygon.Path = GetTail((float)Width / 2);

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
                ProfilePictureShape.Superellipse => Width / 4,
                ProfilePictureShape.Ellipse => Width / 2,
                _ => 0
            });
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

        #region Shape

        public bool IsShapeEnabled { get; set; } = true;

        public ProfilePictureShape Shape
        {
            get { return (ProfilePictureShape)GetValue(ShapeProperty); }
            set { SetValue(ShapeProperty, value); }
        }

        public static readonly DependencyProperty ShapeProperty =
            DependencyProperty.Register("Shape", typeof(ProfilePictureShape), typeof(ProfilePicture2), new PropertyMetadata(ProfilePictureShape.Ellipse, OnShapeChanged));

        private static void OnShapeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ProfilePicture2)d).UpdateCornerRadius();
        }

        #endregion

        public void Clear()
        {
            UpdateManager.Unsubscribe(this, ref _fileToken);

            _fileId = null;
            _referenceId = null;

            _parameters = null;

            Source = null;
        }

        #region Source

        public object Source
        {
            get => (object)GetValue(SourceProperty);
            set => SetValue(SourceProperty, value);
        }

        public static readonly DependencyProperty SourceProperty =
            DependencyProperty.Register("Source", typeof(object), typeof(ProfilePicture2), new PropertyMetadata(null, OnSourceChanged));

        private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ProfilePicture2)d).OnSourceChanged((object)e.NewValue);
        }

        private void OnSourceChanged(object newValue)
        {
            if (LayoutRoot == null)
            {
                return;
            }

            if (newValue is ProfilePictureSourceText or null)
            {
                UpdateManager.Unsubscribe(this, ref _fileToken);

                _fileId = null;
                _referenceId = null;

                _parameters = null;
            }

            if (newValue is ProfilePictureSourceText placeholder)
            {
                Gradient.GradientStops[0].Color = placeholder.TopColor;
                Gradient.GradientStops[1].Color = placeholder.BottomColor;

                LayoutRoot.Background = Gradient;

                Initials.Visibility = Visibility.Visible;
                Initials.Text = placeholder.Initials;

                if (_glyph != placeholder.IsGlyph)
                {
                    _glyph = placeholder.IsGlyph;
                    Initials.Margin = new Thickness(0, 1, 0, _glyph ? 0 : 2);
                }
            }
            else if (newValue is ImageSource source)
            {
                Texture.ImageSource = source;

                LayoutRoot.Background = Texture;

                Initials.Visibility = Visibility.Collapsed;
            }
            else if (newValue is ThumbnailController)
            {
                LayoutRoot.Background = Texture;

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

        #endregion

        private void UpdateFile(object target, File file)
        {
            ApplyParameters(State.Update);
        }

        #region MessageSender

        public void SetMessageSender(IClientService clientService, MessageSender sender, int side)
        {
            if (clientService.TryGetUser(sender, out User user))
            {
                SetUser(clientService, user, side, State.Download);
            }
            else if (clientService.TryGetChat(sender, out Chat chat))
            {
                SetChat(clientService, chat, side, State.Download);
            }
        }

        #endregion

        #region Story

        struct StoryParameters
        {
            public IClientService ClientService;
            public Story Story;
            public int Side;

            public StoryParameters(IClientService clientService, Story story, int side)
            {
                ClientService = clientService;
                Story = story;
                Side = side;
            }
        }

        public void SetStory(IClientService clientService, Story story, int side, State state = State.Download)
        {
            if (!_templateApplied)
            {
                _parameters = new StoryParameters(clientService, story, side);
                return;
            }

            if (story.Content is StoryContentPhoto photo)
            {
                SetStory(clientService, story, photo.Photo.Sizes[0].Photo.Id, photo.Photo.GetSmall()?.Photo, side, state);
            }
            else if (story.Content is StoryContentVideo video)
            {
                SetStory(clientService, story, video.Video.Video.Id, video.Video.Thumbnail?.File, side, state);
            }
        }

        private void SetStory(IClientService clientService, Story story, int fileId, File file, int side, State state = State.Download)
        {
            if (_referenceId != story.Id || _fileId != file?.Id || Source == null || state != State.Download)
            {
                _referenceId = story.Id;
                _fileId = file?.Id;

                UpdateManager.Unsubscribe(this, ref _fileToken);

                Source = GetStory(clientService, story, fileId, file, side, out var shape, state);
                Shape = shape;
            }
        }

        private object GetStory(IClientService clientService, Story story, int fileId, File file, int side, out ProfilePictureShape shape, State state = State.Download)
        {
            System.Diagnostics.Debug.Assert(side == Width);

            shape = ProfilePictureShape.Ellipse;

            if (file != null)
            {
                if (file.Local.IsDownloadingCompleted)
                {
                    _controller ??= new ThumbnailController(Texture);
                    _controller.Bitmap(file.Local.Path, fileId);

                    return _controller;
                }
                else
                {
                    if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive && state != State.Update)
                    {
                        clientService.DownloadFile(file.Id, 1);
                    }

                    _parameters = new StoryParameters(clientService, story, side);
                    UpdateManager.Subscribe(this, clientService, file, ref _fileToken, UpdateFile, true);
                }
            }

            if (story.Content is StoryContentPhoto photo && photo.Photo.Minithumbnail != null)
            {
                _controller ??= new ThumbnailController(Texture);
                _controller.Blur(photo.Photo.Minithumbnail.Data, 3, fileId);

                return _controller;
            }
            else if (story.Content is StoryContentVideo video && video.Video.Minithumbnail != null)
            {
                _controller ??= new ThumbnailController(Texture);
                _controller.Blur(video.Video.Minithumbnail.Data, 3, fileId);

                return _controller;
            }

            _controller?.Recycle();
            return null;
        }

        #endregion

        #region Chat

        struct ChatParameters
        {
            public IClientService ClientService;
            public Chat Chat;
            public int Side;

            public ChatParameters(IClientService clientService, Chat chat, int side)
            {
                ClientService = clientService;
                Chat = chat;
                Side = side;
            }
        }

        public void SetChat(IClientService clientService, Chat chat, int side, State state = State.Download)
        {
            if (!_templateApplied)
            {
                _parameters = new ChatParameters(clientService, chat, side);
                return;
            }

            SetChat(clientService, chat, chat.Photo?.Small, side, state);
        }

        private void SetChat(IClientService clientService, Chat chat, File file, int side, State state = State.Download)
        {
            if (_referenceId != chat.Id || _fileId != file?.Id || Source == null || state != State.Download)
            {
                _referenceId = chat.Id;
                _fileId = file?.Id;

                UpdateManager.Unsubscribe(this, ref _fileToken);

                Source = GetChat(clientService, chat, file, side, out var shape, state);
                Shape = shape;
            }
        }

        private object GetChat(IClientService clientService, Chat chat, File file, int side, out ProfilePictureShape shape, State state = State.Download)
        {
            // TODO: this method may throw a NullReferenceException in some conditions

            System.Diagnostics.Debug.Assert(side == Width);

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

            if (IsShapeEnabled && clientService.TryGetSupergroup(chat, out Supergroup supergroup))
            {
                if (supergroup.IsForum)
                {
                    shape = ProfilePictureShape.Superellipse;
                }
                else if (supergroup.IsDirectMessagesGroup)
                {
                    shape = ProfilePictureShape.Tail;
                }
            }

            if (file != null)
            {
                if (file.Local.IsDownloadingCompleted)
                {
                    _controller ??= new ThumbnailController(Texture);
                    _controller.Bitmap(file.Local.Path, side, side, chat.Id);

                    return _controller;
                }
                else
                {
                    if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive && state != State.Update)
                    {
                        clientService.DownloadFile(file.Id, 1);
                    }

                    _parameters = new ChatParameters(clientService, chat, side);
                    UpdateManager.Subscribe(this, clientService, file, ref _fileToken, UpdateFile, true);
                }

                var minithumbnail = chat.Photo?.Minithumbnail;
                if (minithumbnail != null)
                {
                    _controller ??= new ThumbnailController(Texture);
                    _controller.Blur(minithumbnail.Data, 3, chat.Id);

                    return _controller;
                }
            }

            _controller?.Recycle();

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

        #endregion

        #region User

        struct UserParameters
        {
            public IClientService ClientService;
            public User User;
            public int Side;

            public UserParameters(IClientService clientService, User user, int side)
            {
                ClientService = clientService;
                User = user;
                Side = side;
            }
        }

        public void SetUser(IClientService clientService, User user, int side, State state = State.Download)
        {
            if (!_templateApplied)
            {
                _parameters = new UserParameters(clientService, user, side);
                return;
            }

            SetUser(clientService, user, user.ProfilePhoto?.Small, side, state);
        }

        private void SetUser(IClientService clientService, User user, File file, int side, State state = State.Download)
        {
            if (_referenceId != user.Id || _fileId != file?.Id || Source == null || state != State.Download)
            {
                _referenceId = user.Id;
                _fileId = file?.Id;

                UpdateManager.Unsubscribe(this, ref _fileToken);

                Source = GetUser(clientService, user, file, side, state);
                Shape = ProfilePictureShape.Ellipse;
            }
        }

        private object GetUser(IClientService clientService, User user, File file, int side, State state = State.Download)
        {
            System.Diagnostics.Debug.Assert(side == Width);

            if (file != null)
            {
                if (file.Local.IsDownloadingCompleted)
                {
                    _controller ??= new ThumbnailController(Texture);
                    _controller.Bitmap(file.Local.Path, side, side, user.Id);

                    return _controller;
                }
                else
                {
                    if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive && state != State.Update)
                    {
                        clientService.DownloadFile(file.Id, 1);
                    }

                    _parameters = new UserParameters(clientService, user, side);
                    UpdateManager.Subscribe(this, clientService, file, ref _fileToken, UpdateFile, true);
                }

                var minithumbnail = user.ProfilePhoto?.Minithumbnail;
                if (minithumbnail != null)
                {
                    _controller ??= new ThumbnailController(Texture);
                    _controller.Blur(minithumbnail.Data, 3, user.Id);

                    return _controller;
                }
            }

            _controller?.Recycle();

            if (user.Type is UserTypeDeleted)
            {
                return ProfilePictureSourceText.GetGlyph(Icons.GhostFilled, long.MinValue);
            }

            return ProfilePictureSourceText.GetUser(clientService, user);
        }


        #endregion

        #region Chat invite

        struct ChatInviteParameters
        {
            public IClientService ClientService;
            public ChatInviteLinkInfo Chat;
            public int Side;

            public ChatInviteParameters(IClientService clientService, ChatInviteLinkInfo chat, int side)
            {
                ClientService = clientService;
                Chat = chat;
                Side = side;
            }
        }

        public void SetChat(IClientService clientService, ChatInviteLinkInfo chat, int side, State state = State.Download)
        {
            if (!_templateApplied)
            {
                _parameters = new ChatInviteParameters(clientService, chat, side);
                return;
            }

            SetChat(clientService, chat, chat.Photo?.Small, side, state);
        }

        private void SetChat(IClientService clientService, ChatInviteLinkInfo chat, File file, int side, State state = State.Download)
        {
            UpdateManager.Unsubscribe(this, ref _fileToken);

            Source = GetChat(clientService, chat, file, side, state);
            Shape = ProfilePictureShape.Ellipse;
        }

        private object GetChat(IClientService clientService, ChatInviteLinkInfo chat, File file, int side, State state = State.Download)
        {
            if (file != null)
            {
                if (file.Local.IsDownloadingCompleted)
                {
                    _controller ??= new ThumbnailController(Texture);
                    _controller.Bitmap(file.Local.Path, side, side, chat.ChatId);

                    return _controller;
                }
                else
                {
                    if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive && state != State.Update)
                    {
                        clientService.DownloadFile(file.Id, 1);
                    }

                    _parameters = new ChatInviteParameters(clientService, chat, side);
                    UpdateManager.Subscribe(this, clientService, file, ref _fileToken, UpdateFile, true);
                }
            }

            if (chat.Photo?.Minithumbnail != null)
            {
                _controller ??= new ThumbnailController(Texture);
                _controller.Blur(chat.Photo.Minithumbnail.Data, 3, chat.ChatId);

                return _controller;
            }

            _controller?.Recycle();
            return ProfilePictureSourceText.GetChat(clientService, chat);
        }

        #endregion

        #region Chat photo

        struct ChatPhotoParameters
        {
            public IClientService ClientService;
            public ChatPhoto Photo;
            public int Side;

            public ChatPhotoParameters(IClientService clientService, ChatPhoto photo, int side)
            {
                ClientService = clientService;
                Photo = photo;
                Side = side;
            }
        }

        public void SetChatPhoto(IClientService clientService, ChatPhoto photo, int side, State state = State.Download)
        {
            if (!_templateApplied)
            {
                _parameters = new ChatPhotoParameters(clientService, photo, side);
                return;
            }

            SetChatPhoto(clientService, photo, photo.GetBig()?.Photo, side, state);
        }

        private void SetChatPhoto(IClientService clientService, ChatPhoto photo, File file, int side, State state = State.Download)
        {
            UpdateManager.Unsubscribe(this, ref _fileToken);

            Source = GetChatPhoto(clientService, photo, file, side, state);
            Shape = ProfilePictureShape.Ellipse;
        }

        private object GetChatPhoto(IClientService clientService, ChatPhoto photo, File file, int side, State state = State.Download)
        {
            if (file != null)
            {
                if (file.Local.IsDownloadingCompleted)
                {
                    _controller ??= new ThumbnailController(Texture);
                    _controller.Bitmap(file.Local.Path, side, side, photo.Id);

                    return _controller;
                }
                else
                {
                    if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive && state != State.Update)
                    {
                        clientService.DownloadFile(file.Id, 1);
                    }

                    _parameters = new ChatPhotoParameters(clientService, photo, side);
                    UpdateManager.Subscribe(this, clientService, file, ref _fileToken, UpdateFile, true);
                }
            }

            if (photo.Minithumbnail != null)
            {
                _controller ??= new ThumbnailController(Texture);
                _controller.Blur(photo.Minithumbnail.Data, 3, photo.Id);

                return _controller;
            }

            _controller?.Recycle();
            return null;
        }

        #endregion
    }

    public abstract record ProfilePictureSource(ProfilePictureShape Shape)
    {
        public static ProfilePictureSource Message(MessageViewModel message)
        {
            if (message.IsSaved || message.IsVerificationCode)
            {
                if (message.ForwardInfo?.Origin is MessageOriginUser fromUser && message.ClientService.TryGetUser(fromUser.SenderUserId, out User fromUserUser))
                {
                    return ProfilePictureSource.User(message.ClientService, fromUserUser);
                }
                else if (message.ForwardInfo?.Origin is MessageOriginChat fromChat && message.ClientService.TryGetChat(fromChat.SenderChatId, out Chat fromChatChat))
                {
                    return ProfilePictureSource.Chat(message.ClientService, fromChatChat);
                }
                else if (message.ForwardInfo?.Origin is MessageOriginChannel fromChannel && message.ClientService.TryGetChat(fromChannel.ChatId, out Chat fromChannelChat))
                {
                    return ProfilePictureSource.Chat(message.ClientService, fromChannelChat);
                }
                else if (message.ForwardInfo?.Origin is MessageOriginHiddenUser fromHiddenUser)
                {
                    return ProfilePictureSourceText.GetNameForUser(fromHiddenUser.SenderName, long.MinValue);
                }
                else if (message.ImportInfo != null)
                {
                    return ProfilePictureSourceText.GetNameForUser(message.ImportInfo.SenderName, long.MinValue);
                }
            }
            else if (message.ClientService.TryGetUser(message.SenderId, out User senderUser))
            {
                return ProfilePictureSource.User(message.ClientService, senderUser);
            }
            else if (message.ClientService.TryGetChat(message.SenderId, out Chat senderChat))
            {
                return ProfilePictureSource.Chat(message.ClientService, senderChat);
            }

            return null;
        }

        public static ProfilePictureSource MessageSender(IClientService clientService, MessageSender sender)
        {
            if (clientService.TryGetUser(sender, out User user))
            {
                return ProfilePictureSource.User(clientService, user);
            }
            else if (clientService.TryGetChat(sender, out Chat chat))
            {
                return ProfilePictureSource.Chat(clientService, chat);
            }

            return null;
        }

        public static ProfilePictureSource User(IClientService clientService, User user)
        {
            ProfilePictureSourceText text;
            if (user.Type is UserTypeDeleted)
            {
                text = ProfilePictureSourceText.GetGlyph(Icons.GhostFilled, long.MinValue);
            }
            else
            {
                text = ProfilePictureSourceText.GetUser(clientService, user);
            }

            var photo = user.ProfilePhoto;
            if (photo != null)
            {
                return new ProfilePictureSourcePhoto(clientService, user.Id, photo.Small, photo.Minithumbnail, text, ProfilePictureShape.Ellipse);
            }

            return text;
        }

        public static ProfilePictureSource ChatPhoto(IClientService clientService, User user, ChatPhoto chatPhoto, bool big)
        {
            ProfilePictureSourceText text;
            if (user.Type is UserTypeDeleted)
            {
                text = ProfilePictureSourceText.GetGlyph(Icons.GhostFilled, long.MinValue);
            }
            else
            {
                text = ProfilePictureSourceText.GetUser(clientService, user);
            }

            var photo = big ? chatPhoto?.GetBig() : chatPhoto?.GetSmall();
            if (photo != null)
            {
                return new ProfilePictureSourcePhoto(clientService, user.Id, photo.Photo, chatPhoto.Minithumbnail, text, ProfilePictureShape.Ellipse);
            }

            return text;
        }

        public static ProfilePictureSource ChatPhoto(IClientService clientService, Chat chat, ChatPhoto chatPhoto, bool big)
        {
            ProfilePictureSourceText text;
            text = ProfilePictureSourceText.GetChat(clientService, chat);

            var photo = big ? chatPhoto?.GetBig() : chatPhoto?.GetSmall();
            if (photo != null)
            {
                return new ProfilePictureSourcePhoto(clientService, chat.Id, photo.Photo, chatPhoto.Minithumbnail, text, ProfilePictureShape.Ellipse);
            }

            return text;
        }

        public static ProfilePictureSource Chat(IClientService clientService, Chat chat)
        {
            if (chat.Id == clientService.Options.MyId)
            {
                return ProfilePictureSourceText.GetGlyph(Icons.BookmarkFilled, 5);
            }
            else if (chat.Id == clientService.Options.RepliesBotChatId)
            {
                return ProfilePictureSourceText.GetGlyph(Icons.ArrowReplyFilled, 5);
            }

            var shape = ProfilePictureShape.Ellipse;
            if (clientService.TryGetSupergroup(chat, out Supergroup supergroup))
            {
                if (supergroup.IsForum)
                {
                    shape = ProfilePictureShape.Superellipse;
                }
                else if (supergroup.IsDirectMessagesGroup)
                {
                    shape = ProfilePictureShape.Tail;
                }
            }

            ProfilePictureSourceText text;
            if (supergroup == null && clientService.TryGetUser(chat, out User user))
            {
                if (user.Type is UserTypeDeleted)
                {
                    text = ProfilePictureSourceText.GetGlyph(Icons.GhostFilled, long.MinValue);
                }
                else
                {
                    text = ProfilePictureSourceText.GetUser(clientService, user);
                }
            }
            else
            {
                text = ProfilePictureSourceText.GetChat(clientService, chat, shape);
            }

            var photo = chat.Photo;
            if (photo != null)
            {
                return new ProfilePictureSourcePhoto(clientService, chat.Id, photo.Small, photo.Minithumbnail, text, shape);
            }

            return text;
        }

        public static ProfilePictureSource Chat(IClientService clientService, ChatInviteLinkInfo chat)
        {
            ProfilePictureSourceText text;
            text = ProfilePictureSourceText.GetChat(clientService, chat);

            var photo = chat.Photo;
            if (photo != null)
            {
                return new ProfilePictureSourcePhoto(clientService, chat.ChatId, photo.Small, photo.Minithumbnail, text);
            }

            return text;
        }

        public static ProfilePictureSource Story(IClientService clientService, Story story)
        {
            if (story.Content is StoryContentPhoto photo)
            {
                var file = photo.Photo.GetSmall()?.Photo;
                if (file != null)
                {
                    return new ProfilePictureSourcePhoto(clientService, photo.Photo.Sizes[0].Photo.Id, file, photo.Photo.Minithumbnail);
                }
            }
            else if (story.Content is StoryContentVideo video)
            {
                var file = video.Video.Thumbnail?.File;
                if (file != null)
                {
                    return new ProfilePictureSourcePhoto(clientService, video.Video.Video.Id, file, video.Video.Minithumbnail);
                }
            }

            if (story.PosterId != null)
            {
                return ProfilePictureSource.MessageSender(clientService, story.PosterId);
            }

            if (clientService.TryGetChat(story.PosterChatId, out Chat chat))
            {
                return ProfilePictureSource.Chat(clientService, chat);
            }

            return null;
        }
    }

    public record ProfilePictureSourceBitmap(ImageSource Bitmap, ProfilePictureShape Shape = ProfilePictureShape.Ellipse)
        : ProfilePictureSource(Shape);

    public record ProfilePictureSourcePhoto(IClientService ClientService, long Id, File Photo, Minithumbnail Minithumbnail, ProfilePictureSourceText Text = null, ProfilePictureShape Shape = ProfilePictureShape.Ellipse)
        : ProfilePictureSource(Shape);

    public record ProfilePictureSourceText(string Initials, bool IsGlyph, Color TopColor, Color BottomColor, ProfilePictureShape Shape = ProfilePictureShape.Ellipse)
        : ProfilePictureSource(Shape)
    {
        private static readonly Color[] _colorsTop = new Color[7]
        {
            Color.FromArgb(0xFF, 0xEF, 0x8E, 0x67), // orange
            Color.FromArgb(0xFF, 0xF7, 0xCE, 0x79), // yellow
            Color.FromArgb(0xFF, 0x8C, 0xAF, 0xF9), // violet
            Color.FromArgb(0xFF, 0xAC, 0xDC, 0x89), // green
            Color.FromArgb(0xFF, 0x81, 0xE9, 0xD6), // teal
            Color.FromArgb(0xFF, 0x8A, 0xD3, 0xF9), // blue
            Color.FromArgb(0xFF, 0xFF, 0xAF, 0xC7), // rose
        };

        private static readonly Color[] _colors = new Color[7]
        {
            Color.FromArgb(0xFF, 0xEC, 0x5F, 0x6D), // orange
            Color.FromArgb(0xFF, 0xF2, 0xAC, 0x6A), // yellow
            Color.FromArgb(0xFF, 0x65, 0x60, 0xF6), // violet
            Color.FromArgb(0xFF, 0x75, 0xC8, 0x73), // green
            Color.FromArgb(0xFF, 0x62, 0xC6, 0xB7), // teal
            Color.FromArgb(0xFF, 0x51, 0x9D, 0xEA), // blue
            Color.FromArgb(0xFF, 0xF2, 0x74, 0x9A), // rose
        };

        private static readonly Color _disabledTop = Color.FromArgb(0xFF, 0xA6, 0xAB, 0xB7);
        private static readonly Color _disabled = Color.FromArgb(0xFF, 0x86, 0x89, 0x92);

        public static Color GetColor(long i)
        {
            if (i == -1)
            {
                return _disabled;
            }

            return _colors[Math.Abs(i % _colors.Length)];
        }

        public static SolidColorBrush GetBrush(long i, double opacity = 1)
        {
            return new SolidColorBrush(_colors[Math.Abs(i % _colors.Length)])
            {
                Opacity = opacity
            };
        }

        public static CompositionBrush GetBrush(Compositor compositor, long i)
        {
            return compositor.CreateColorBrush(_colors[Math.Abs(i % _colors.Length)]);
        }

        public static ProfilePictureSourceText GetChat(IClientService clientService, Chat chat, ProfilePictureShape shape = ProfilePictureShape.None)
        {
            if (shape == ProfilePictureShape.None)
            {
                shape = ProfilePictureShape.Ellipse;

                if (clientService.TryGetSupergroup(chat, out Supergroup supergroup))
                {
                    if (supergroup.IsForum)
                    {
                        shape = ProfilePictureShape.Superellipse;
                    }
                    else if (supergroup.IsDirectMessagesGroup)
                    {
                        shape = ProfilePictureShape.Tail;
                    }
                }
            }

            return ProfilePictureSourceText.FromNameColor(InitialNameStringConverter.Convert(chat.Title), false, clientService.GetAccentColor(chat.AccentColorId), shape);
        }

        public static ProfilePictureSourceText GetChat(IClientService clientService, ChatInviteLinkInfo chat)
        {
            return ProfilePictureSourceText.FromNameColor(InitialNameStringConverter.Convert(chat.Title), false, clientService.GetAccentColor(chat.AccentColorId));
        }

        public static ProfilePictureSourceText GetUser(IClientService clientService, User user)
        {
            return ProfilePictureSourceText.FromNameColor(InitialNameStringConverter.Convert(user.FirstName, user.LastName), false, clientService.GetAccentColor(user.AccentColorId));
        }

        public static ProfilePictureSourceText GetNameForUser(string firstName, string lastName, long id = 5, ProfilePictureShape shape = ProfilePictureShape.Ellipse)
        {
            return ProfilePictureSourceText.FromId(InitialNameStringConverter.Convert(firstName, lastName), false, id);
        }

        public static ProfilePictureSourceText GetNameForUser(string name, long id = 5, ProfilePictureShape shape = ProfilePictureShape.Ellipse)
        {
            return ProfilePictureSourceText.FromId(InitialNameStringConverter.Convert(name), false, id);
        }

        public static ProfilePictureSourceText GetNameForChat(string title, long id = 5, ProfilePictureShape shape = ProfilePictureShape.Ellipse)
        {
            return ProfilePictureSourceText.FromId(InitialNameStringConverter.Convert(title), false, id);
        }

        public static ProfilePictureSourceText GetGlyph(string glyph, long id = 5, ProfilePictureShape shape = ProfilePictureShape.Ellipse)
        {
            return ProfilePictureSourceText.FromId(glyph, true, id);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ProfilePictureSourceText FromNameColor(string initials, bool isGlyph, NameColor color, ProfilePictureShape shape = ProfilePictureShape.Ellipse)
        {
            if (color == null)
            {
                return new ProfilePictureSourceText(initials, isGlyph, _disabledTop, _disabled, shape);
            }
            else
            {
                return new ProfilePictureSourceText(initials, isGlyph, _colorsTop[Math.Abs(color.BuiltInAccentColorId % _colors.Length)], _colors[Math.Abs(color.BuiltInAccentColorId % _colors.Length)], shape);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ProfilePictureSourceText FromId(string initials, bool isGlyph, long id, ProfilePictureShape shape = ProfilePictureShape.Ellipse)
        {
            if (id == long.MinValue)
            {
                return new ProfilePictureSourceText(initials, isGlyph, _disabledTop, _disabled, shape);
            }
            else
            {
                return new ProfilePictureSourceText(initials, isGlyph, _colorsTop[Math.Abs(id % _colors.Length)], _colors[Math.Abs(id % _colors.Length)], shape);
            }
        }
    }
}
