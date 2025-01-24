//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Linq;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views.Stars.Popups;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Profile
{
    public partial class ProfileGiftsTabViewModel : ViewModelBase, IHandle, IIncrementalCollectionOwner
    {
        private MessageSender _senderId;
        private string _nextOffsetId = string.Empty;

        public ProfileGiftsTabViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            Items = new IncrementalCollection<ReceivedGift>(this);
        }

        protected override Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            if (parameter is long chatId)
            {
                var chat = ClientService.GetChat(chatId);
                if (chat == null)
                {
                    return Task.CompletedTask;
                }

                var user = ClientService.GetUser(chat);
                if (user == null)
                {
                    _senderId = new MessageSenderChat(chat.Id);
                }
                else
                {
                    _senderId = new MessageSenderUser(user.Id);
                }
            }

            return Task.CompletedTask;
        }

        public override void Subscribe()
        {
            Aggregator.Subscribe<UpdateGiftIsSaved>(this, Handle)
                .Subscribe<UpdateGiftIsSold>(Handle)
                .Subscribe<UpdateGiftUpgraded>(Handle);
        }

        private void Handle(UpdateGiftIsSaved update)
        {
            BeginOnUIThread(() =>
            {
                var receivedGift = Items.FirstOrDefault(x => x.ReceivedGiftId == update.ReceivedGiftId);
                if (receivedGift == null)
                {
                    return;
                }

                receivedGift.IsSaved = update.IsSaved;

                var index = Items.IndexOf(receivedGift);
                Items.Remove(receivedGift);
                Items.Insert(index, receivedGift);
            });
        }

        private void Handle(UpdateGiftIsSold update)
        {
            BeginOnUIThread(() =>
            {
                var receivedGift = Items.FirstOrDefault(x => x.ReceivedGiftId == update.ReceivedGiftId);
                if (receivedGift == null)
                {
                    return;
                }

                Items.Remove(receivedGift);
            });
        }

        private void Handle(UpdateGiftUpgraded update)
        {
            BeginOnUIThread(() =>
            {
                var receivedGift = Items.FirstOrDefault(x => x.ReceivedGiftId == update.ReceivedGiftId);
                if (receivedGift == null)
                {
                    return;
                }

                var index = Items.IndexOf(receivedGift);
                Items.Remove(receivedGift);
                Items.Insert(index, update.Gift);
            });
        }

        private bool _excludeUnsaved;
        public bool ExcludeUnsaved
        {
            get => _excludeUnsaved;
            set
            {
                if (Set(ref _excludeUnsaved, value))
                {
                    HasMoreItems = true;
                    Items.Clear();
                }
            }
        }

        private bool _excludeSaved;
        public bool ExcludeSaved
        {
            get => _excludeSaved;
            set
            {
                if (Set(ref _excludeSaved, value))
                {
                    HasMoreItems = true;
                    Items.Clear();
                }
            }
        }

        private bool _excludeUnlimited;
        public bool ExcludeUnlimited
        {
            get => _excludeUnlimited;
            set
            {
                if (Set(ref _excludeUnlimited, value))
                {
                    HasMoreItems = true;
                    Items.Clear();
                }
            }
        }

        private bool _excludeLimited;
        public bool ExcludeLimited
        {
            get => _excludeLimited;
            set
            {
                if (Set(ref _excludeLimited, value))
                {
                    HasMoreItems = true;
                    Items.Clear();
                }
            }
        }

        private bool _excludeUpgraded;
        public bool ExcludeUpgraded
        {
            get => _excludeUpgraded;
            set
            {
                if (Set(ref _excludeUpgraded, value))
                {
                    HasMoreItems = true;
                    Items.Clear();
                }
            }
        }

        private bool _sortByPrice;
        public bool SortByPrice
        {
            get => _sortByPrice;
            set
            {
                if (Set(ref _sortByPrice, value))
                {
                    HasMoreItems = true;
                    Items.Clear();
                }
            }
        }

        public IncrementalCollection<ReceivedGift> Items { get; private set; }

        public async Task<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            var total = 0u;

            var response = await ClientService.SendAsync(new GetReceivedGifts(_senderId, _excludeUnsaved, _excludeSaved, _excludeUnlimited, _excludeLimited, _excludeUpgraded, _sortByPrice, _nextOffsetId, 50));
            if (response is ReceivedGifts gifts)
            {
                _nextOffsetId = gifts.NextOffset;

                foreach (var gift in gifts.Gifts)
                {
                    Items.Add(gift);
                    total++;
                }
            }

            HasMoreItems = !string.IsNullOrEmpty(_nextOffsetId);

            return new LoadMoreItemsResult
            {
                Count = total
            };
        }

        public bool HasMoreItems { get; private set; } = true;

        public void OpenGift(ReceivedGift receivedGift)
        {
            if (receivedGift == null)
            {
                return;
            }

            ShowPopup(new ReceivedGiftPopup(ClientService, NavigationService, receivedGift, _senderId));
        }
    }
}
