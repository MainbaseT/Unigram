//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using Telegram.Common;
using Telegram.Controls.Media;
using Telegram.Converters;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.Views;
using Windows.UI.Xaml;

namespace Telegram.Controls.Cells
{
    public sealed partial class SharedVoiceCell : GridEx
    {
        private MessageWithOwner _message;
        public MessageWithOwner Message => _message;

        private long _fileToken;

        public SharedVoiceCell()
        {
            InitializeComponent();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            TypeResolver.Current.Playback.SourceChanged -= OnPlaybackStateChanged;
            TypeResolver.Current.Playback.StateChanged -= OnPlaybackStateChanged;
            TypeResolver.Current.Playback.PositionChanged -= OnPositionChanged;
        }

        private bool _hidden;

        public void Hide()
        {
            if (_hidden)
            {
                return;
            }

            _hidden = true;
            ButtonRoot.Opacity = 0;
            TextRoot.Opacity = 0;
        }

        public void UpdateMessage(MessageWithOwner message)
        {
            if (_hidden)
            {
                _hidden = false;
                ButtonRoot.Opacity = 1;
                TextRoot.Opacity = 1;
            }

            _message = message;

            TypeResolver.Current.Playback.SourceChanged -= OnPlaybackStateChanged;

            var voiceNote = GetContent(message.Content);
            if (voiceNote == null)
            {
                return;
            }

            TypeResolver.Current.Playback.SourceChanged += OnPlaybackStateChanged;

            if (message.ClientService.TryGetUser(message.SenderId, out User user))
            {
                Title.Text = user.FullName();
            }
            else if (message.ClientService.TryGetChat(message.SenderId, out Chat chat))
            {
                Title.Text = chat.Title;
            }
            else
            {
                Title.Text = string.Empty;
            }

            UpdateManager.Subscribe(this, message, voiceNote.Voice, ref _fileToken, UpdateFile);
            UpdateFile(message, voiceNote.Voice);
        }

        #region Playback

        private void OnPlaybackStateChanged(IPlaybackService sender, object args)
        {
            var voiceNote = GetContent(_message?.Content);
            if (voiceNote == null)
            {
                return;
            }

            this.BeginOnUIThread(() => UpdateFile(_message, voiceNote.Voice));
        }

        private void OnPositionChanged(IPlaybackService sender, PlaybackPositionChangedEventArgs args)
        {
            var position = args.Position;
            var duration = args.Duration;

            this.BeginOnUIThread(() => UpdatePosition(position, duration));
        }

        private void UpdatePosition(TimeSpan position, TimeSpan duration)
        {
            var message = _message;
            if (message == null)
            {
                return;
            }

            if (message.AreTheSame(TypeResolver.Current.Playback.CurrentItem) /*&& !_pressed*/)
            {
                Subtitle.Text = FormatTime(position) + " / " + FormatTime(duration);
            }
        }

        private string FormatTime(TimeSpan span)
        {
            if (span.TotalHours >= 1)
            {
                return span.ToString("h\\:mm\\:ss");
            }
            else
            {
                return span.ToString("mm\\:ss");
            }
        }

        #endregion

        private void UpdateFile(object target, File file)
        {
            UpdateFile(_message, file);
        }

        private void UpdateFile(MessageWithOwner message, File file)
        {
            TypeResolver.Current.Playback.StateChanged -= OnPlaybackStateChanged;
            TypeResolver.Current.Playback.PositionChanged -= OnPositionChanged;

            var voiceNote = GetContent(message.Content);
            if (voiceNote == null)
            {
                return;
            }

            if (voiceNote.Voice.Id != file.Id)
            {
                return;
            }

            if (message.AreTheSame(TypeResolver.Current.Playback.CurrentItem))
            {
                if (TypeResolver.Current.Playback.PlaybackState == PlaybackState.Paused)
                {
                    //Button.Glyph = Icons.Play;
                    Button.SetGlyph(file.Id, MessageContentState.Play);
                }
                else
                {
                    //Button.Glyph = Icons.Pause;
                    Button.SetGlyph(file.Id, MessageContentState.Pause);
                }

                UpdatePosition(TypeResolver.Current.Playback.Position, TypeResolver.Current.Playback.Duration);

                TypeResolver.Current.Playback.StateChanged += OnPlaybackStateChanged;
                TypeResolver.Current.Playback.PositionChanged += OnPositionChanged;
            }
            else
            {
                //Button.Glyph = Icons.Play;
                Button.SetGlyph(file.Id, MessageContentState.Play);
                Button.Progress = 1;

                Subtitle.Text = string.Format("{0} {1} {2}", voiceNote.GetDuration(), Icons.Bullet, Formatter.DateAt(message.Date));
            }

            Button.Progress = 1;
        }

        private VoiceNote GetContent(MessageContent content)
        {
            if (content is MessageVoiceNote voiceNote)
            {
                return voiceNote.VoiceNote;
            }
            else if (content is MessageText text && text.LinkPreview?.Type is LinkPreviewTypeVoiceNote previewVoiceNote)
            {
                return previewVoiceNote.VoiceNote;
            }

            return null;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var voiceNote = GetContent(_message?.Content);
            if (voiceNote == null)
            {
                return;
            }

            var file = voiceNote.Voice;
            if (file.Local.IsDownloadingActive)
            {
                _message.ClientService.CancelDownloadFile(file, false);
            }
            else if (file.Remote.IsUploadingActive || _message.SendingState is MessageSendingStateFailed)
            {
                _message.ClientService.Send(new DeleteMessages(_message.ChatId, new[] { _message.Id }, true));
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive && !file.Local.IsDownloadingCompleted)
            {
                //_clientService.DownloadFile(file.Id, 32);
                TypeResolver.Current.Playback.Play(_message);
            }
            else
            {
                if (_message.AreTheSame(TypeResolver.Current.Playback.CurrentItem))
                {
                    if (TypeResolver.Current.Playback.PlaybackState == PlaybackState.Paused)
                    {
                        TypeResolver.Current.Playback.Play();
                    }
                    else
                    {
                        TypeResolver.Current.Playback.Pause();
                    }
                }
                else
                {
                    TypeResolver.Current.Playback.Play(_message);
                }
            }
        }
    }
}
