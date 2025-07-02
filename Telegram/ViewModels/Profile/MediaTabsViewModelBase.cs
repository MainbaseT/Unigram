//
// Copyright Fela Ameghino & Contributors 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Rg.DiffUtils;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels.Delegates;
using Telegram.Views.Popups;
using Windows.UI.Xaml.Controls;

namespace Telegram.ViewModels.Profile
{
    public abstract partial class MediaTabsViewModelBase : MultiViewModelBase
    {
        private readonly IPlaybackService _playbackService;
        private readonly IStorageService _storageService;

        private readonly IMessageDelegate _messageDelegate;

        public MediaTabsViewModelBase(IClientService clientService, ISettingsService settingsService, IStorageService storageService, IEventAggregator aggregator, IPlaybackService playbackService)
            : base(clientService, settingsService, aggregator)
        {
            _playbackService = playbackService;
            _storageService = storageService;

            _messageDelegate = new MessageDelegate(this);

            Media = new SearchCollection<MessageWithOwner, MediaCollection>(SetSearch, new SearchMessagesFilterPhotoAndVideo(), new MessageDiffHandler());
            Files = new SearchCollection<MessageWithOwner, MediaCollection>(SetSearch, new SearchMessagesFilterDocument(), new MessageDiffHandler());
            Links = new SearchCollection<MessageWithOwner, MediaCollection>(SetSearch, new SearchMessagesFilterUrl(), new MessageDiffHandler());
            Music = new SearchCollection<MessageWithOwner, MediaCollection>(SetSearch, new SearchMessagesFilterAudio(), new MessageDiffHandler());
            Voice = new SearchCollection<MessageWithOwner, MediaCollection>(SetSearch, new SearchMessagesFilterVoiceNote(), new MessageDiffHandler());
            Animations = new SearchCollection<MessageWithOwner, MediaCollection>(SetSearch, new SearchMessagesFilterAnimation(), new MessageDiffHandler());

            SelectedItems = new MvxObservableCollection<MessageWithOwner>();
            SelectedItems.CollectionChanged += OnCollectionChanged;
        }

        private async void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            var selectedItems = SelectedItems.ToList();
            var properties = await ClientService.GetMessagePropertiesAsync(selectedItems.Select(x => new MessageId(x)));

            CanDeleteSelectedMessages = properties.Count > 0 && properties.Values.All(x => x.CanBeDeletedForAllUsers || x.CanBeDeletedOnlyForSelf);
            CanForwardSelectedMessages = properties.Count > 0 && properties.Values.All(x => x.CanBeForwarded);
        }

        public IPlaybackService PlaybackService => _playbackService;
        public IStorageService StorageService => _storageService;

        public MessageTopic Topic { get; set; }

        public bool IsDeactivated { get; set; }

        public MediaDataSource MediaSource { get; set; }
        public MediaDataSource FilesSource { get; set; }
        public MediaDataSource MusicSource { get; set; }
        public MediaDataSource VoiceSource { get; set; }

        public SearchCollection<MessageWithOwner, MediaCollection> Media { get; private set; }
        public SearchCollection<MessageWithOwner, MediaCollection> Files { get; private set; }
        public SearchCollection<MessageWithOwner, MediaCollection> Links { get; private set; }
        public SearchCollection<MessageWithOwner, MediaCollection> Music { get; private set; }
        public SearchCollection<MessageWithOwner, MediaCollection> Voice { get; private set; }
        public SearchCollection<MessageWithOwner, MediaCollection> Animations { get; private set; }

        protected bool _useMediaSource;
        public bool UseMediaSource
        {
            get => _useMediaSource;
            set
            {
                if (_useMediaSource != value)
                {
                    _useMediaSource = value;
                    RaisePropertyChanged(nameof(MediaView));
                }
            }
        }

        public object MediaView
        {
            get
            {
                if (_useMediaSource && MediaSource != null)
                {
                    return MediaSource;
                }

                return Media;
            }
        }

        protected bool _useFilesSource;
        public bool UseFilesSource
        {
            get => _useFilesSource;
            set
            {
                if (_useFilesSource != value)
                {
                    _useFilesSource = value;
                    RaisePropertyChanged(nameof(FilesView));
                }
            }
        }

        public object FilesView
        {
            get
            {
                if (_useFilesSource && FilesSource != null)
                {
                    return FilesSource;
                }

                return Files;
            }
        }

        protected bool _useMusicSource;
        public bool UseMusicSource
        {
            get => _useMusicSource;
            set
            {
                if (_useMusicSource != value)
                {
                    _useMusicSource = value;
                    RaisePropertyChanged(nameof(MusicView));
                }
            }
        }

        public object MusicView
        {
            get
            {
                if (_useMusicSource && MusicSource != null)
                {
                    return MusicSource;
                }

                return Music;
            }
        }

        protected bool _useVoiceSource;
        public bool UseVoiceSource
        {
            get => _useVoiceSource;
            set
            {
                if (_useVoiceSource != value)
                {
                    _useVoiceSource = value;
                    RaisePropertyChanged(nameof(VoiceView));
                }
            }
        }

        public object VoiceView
        {
            get
            {
                if (_useVoiceSource && VoiceSource != null)
                {
                    return VoiceSource;
                }

                return Voice;
            }
        }

        public virtual MediaCollection SetSearch(object sender, string query)
        {
            if (sender is SearchMessagesFilter filter && !IsDeactivated)
            {
                return new MediaCollection(ClientService, filter, query);
            }

            return null;
        }

        public ObservableCollection<MessageWithOwner> SelectedItems { get; }

        public partial class MessageDiffHandler : IDiffHandler<MessageWithOwner>
        {
            public bool CompareItems(MessageWithOwner oldItem, MessageWithOwner newItem)
            {
                return oldItem?.Id == newItem?.Id && oldItem?.ChatId == newItem?.ChatId;
            }

            public void UpdateItem(MessageWithOwner oldItem, MessageWithOwner newItem)
            {
            }
        }

        public override void Subscribe()
        {
            Aggregator.Subscribe<UpdateDeleteMessages>(this, Handle);
        }

        public void Handle(UpdateDeleteMessages update)
        {
            if (update.FromCache)
            {
                return;
            }

            if (ShouldHandleDeleteMessages(update))
            {
                var table = update.MessageIds.ToHashSet();

                BeginOnUIThread(() =>
                {
                    UpdateDeleteMessages(Media.Source, table);
                    UpdateDeleteMessages(Files.Source, table);
                    UpdateDeleteMessages(Links.Source, table);
                    UpdateDeleteMessages(Music.Source, table);
                    UpdateDeleteMessages(Voice.Source, table);
                    UpdateDeleteMessages(Animations.Source, table);
                });
            }
        }

        protected virtual bool ShouldHandleDeleteMessages(UpdateDeleteMessages update)
        {
            return true;
        }

        private void UpdateDeleteMessages(MediaCollection target, HashSet<long> table)
        {
            //target.Cancel();

            for (int i = 0; i < target.Count; i++)
            {
                var message = target[i];
                if (table.Contains(message.Id))
                {
                    target.RemoveAt(i);
                    i--;

                    break;
                }
            }
        }

        #region View

        public void ViewMessage(MessageWithOwner message)
        {
            NavigationService.NavigateToChat(message.Chat, message.Id, Topic);
        }

        #endregion

        #region Save file as

        public async void SaveMessageMedia(MessageWithOwner message)
        {
            var file = message.GetFile();
            if (file != null)
            {
                await _storageService.SaveFileAsAsync(file);
            }
        }

        #endregion

        #region Open with

        public async void OpenMessageWith(MessageWithOwner message)
        {
            var file = message.GetFile();
            if (file != null)
            {
                await _storageService.OpenFileWithAsync(file);
            }
        }

        #endregion

        #region Show in folder

        public async void OpenMessageFolder(MessageWithOwner message)
        {
            var file = message.GetFile();
            if (file != null)
            {
                await _storageService.OpenFolderAsync(file);
            }
        }

        #endregion

        #region Delete

        public void DeleteMessage(MessageWithOwner message)
        {
            if (message == null)
            {
                return;
            }

            var chat = ClientService.GetChat(message.ChatId);
            if (chat == null)
            {
                return;
            }

            //if (message != null && message.Media is TLMessageMediaGroup groupMedia)
            //{
            //    ExpandSelection(new[] { message });
            //    MessagesDeleteExecute();
            //    return;
            //}

            DeleteMessages(chat, new[] { message });
        }

        private async void DeleteMessages(Chat chat, IList<MessageWithOwner> messages)
        {
            var first = messages.FirstOrDefault();
            if (first == null)
            {
                return;
            }

            var items = messages
                .DistinctBy(x => x.Id)
                .ToList();

            var properties = await ClientService.GetMessagePropertiesAsync(items.Select(x => new MessageId(x)));

            var updated = items
                .Where(x => properties.ContainsKey(new MessageId(x)))
                .ToList();

            if (updated.Empty())
            {
                return;
            }

            var popup = new DeleteMessagesPopup(ClientService, chat, Topic, updated, properties);

            var confirm = await ShowPopupAsync(popup);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            UnselectMessages();

            ClientService.Send(new DeleteMessages(chat.Id, messages.Select(x => x.Id).ToList(), popup.Revoke));

            foreach (var sender in popup.DeleteAll)
            {
                ClientService.Send(new DeleteChatMessagesBySender(chat.Id, sender));
            }

            foreach (var sender in popup.BanUser)
            {
                ClientService.Send(new SetChatMemberStatus(chat.Id, sender, popup.SelectedStatus));
            }

            if (chat.Type is ChatTypeSupergroup supertype)
            {
                foreach (var sender in popup.ReportSpam)
                {
                    var messageIds = messages
                        .Where(x => x.SenderId.AreTheSame(sender))
                        .Select(x => x.Id)
                        .ToList();

                    ClientService.Send(new ReportSupergroupSpam(supertype.SupergroupId, messageIds));
                }
            }
        }

        #endregion

        #region Forward

        public void ForwardMessage(MessageWithOwner message)
        {
            var selectedItems = new[] { message }.ToDictionary(x => new MessageId(x));

            UnselectMessages();
            ForwardMessages(selectedItems);
        }

        #endregion

        #region Multiple Delete

        public void DeleteSelectedMessages()
        {
            var messages = new List<MessageWithOwner>(SelectedItems);

            var first = messages.FirstOrDefault();
            if (first == null)
            {
                return;
            }

            var chat = ClientService.GetChat(first.ChatId);
            if (chat == null)
            {
                return;
            }

            DeleteMessages(chat, messages);
        }

        private bool _canDeleteSelectedMessages;
        public bool CanDeleteSelectedMessages
        {
            get => _canDeleteSelectedMessages;
            set => Set(ref _canDeleteSelectedMessages, value);
        }

        #endregion

        #region Multiple Forward

        public void ForwardSelectedMessages()
        {
            var selectedItems = SelectedItems.ToDictionary(x => new MessageId(x));

            UnselectMessages();
            ForwardMessages(selectedItems);
        }

        private async void ForwardMessages(Dictionary<MessageId, MessageWithOwner> selectedItems)
        {
            var properties = await ClientService.GetMessagePropertiesAsync(selectedItems.Select(x => x.Key));

            var messages = properties.Where(x => x.Value.CanBeForwarded).OrderBy(x => x.Key.Id).ToList();
            if (messages.Count > 0)
            {
                var messagesToShare = new List<MessageToShare>(messages.Count);

                foreach (var property in messages)
                {
                    if (selectedItems.TryGetValue(property.Key, out var message))
                    {
                        messagesToShare.Add(new MessageToShare(message, property.Value, message.ChatId != ClientService.Options.MyId || message.ForwardInfo != null));
                    }
                }

                ShowPopup(new ChooseChatsPopup(), new ChooseChatsConfigurationShareMessages(messagesToShare));
            }
        }

        private bool _canForwardSelectedMessages;
        public bool CanForwardSelectedMessages
        {
            get => _canForwardSelectedMessages;
            set => Set(ref _canForwardSelectedMessages, value);
        }

        #endregion

        #region Select

        public void SelectMessage(MessageWithOwner message)
        {
            SelectedItems.Add(message);
        }

        #endregion

        #region Unselect

        public void UnselectMessages()
        {
            SelectedItems.Clear();
        }

        #endregion

        #region Delegate

        public IMessageDelegate MessageDelegate => _messageDelegate;

        public void OpenUsername(string username)
        {
            _messageDelegate.OpenUsername(username);
        }

        public void OpenUser(long userId)
        {
            _messageDelegate.OpenUser(userId);
        }

        public void OpenUrl(string url, bool untrust)
        {
            _messageDelegate.OpenUrl(url, untrust);
        }

        #endregion

        protected double _headerHeight;
        public virtual double HeaderHeight
        {
            get => _headerHeight;
            set => Set(ref _headerHeight, value);
        }
    }
}
