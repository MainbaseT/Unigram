//
// Copyright (c) Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views.Settings;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;

namespace Telegram.ViewModels.Settings
{
    public partial class SettingsPasskeysViewModel : ViewModelBase, IIncrementalCollectionOwner
    {
        public SettingsPasskeysViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            Items = new IncrementalCollection<Passkey>(this);
        }

        public ObservableCollection<Passkey> Items { get; private set; }

        public async void Info()
        {
            var supported = await BridgeApplicationContext.IsPasskeySupported();
            if (!supported)
            {
                ShowPopup(Strings.PasskeyNotSupportedText, Strings.AppName, Strings.OK);
                return;
            }

            var confirm = await ShowPopupAsync(new SettingsPasskeysIntroPopup());
            if (confirm == ContentDialogResult.Primary)
            {
                CreateImpl();
            }
        }

        public async void Create()
        {
            var supported = await BridgeApplicationContext.IsPasskeySupported();
            if (!supported)
            {
                ShowPopup(Strings.PasskeyNotSupportedText, Strings.AppName, Strings.OK);
                return;
            }

            CreateImpl();
        }

        private async void CreateImpl()
        {
            var response = await BridgeApplicationContext.AddPasskeyAsync(ClientService);
            if (response is Passkey passkey)
            {
                Items.Insert(0, passkey);
                ShowToast(string.Format("**{0}**\n{1}", Strings.PasskeyAddedTitle, string.Format(Strings.PasskeyAddedText, passkey.Name)));
            }
            else if (response is Error { Code: not -2147023673 } error)
            {
                ShowToast(error);
            }
        }

        public async void Delete(Passkey passkey)
        {
            var confirm = await ShowPopupAsync(Strings.PasskeyDeleteText, Strings.PasskeyDeleteTitle, Strings.Delete, Strings.Cancel, destructive: true);
            if (confirm == ContentDialogResult.Primary)
            {
                Items.Remove(passkey);
                ClientService.Send(new RemoveAddedPasskey(passkey.Id));
            }
        }

        public async Task<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            var totalCount = 0u;

            var response = await ClientService.SendAsync(new GetAddedPasskeys());
            if (response is Passkeys passkeys)
            {
                foreach (var passkey in passkeys.PasskeysValue)
                {
                    Items.Add(passkey);
                    totalCount++;
                }
            }

            HasMoreItems = false;
            return new LoadMoreItemsResult
            {
                Count = totalCount
            };
        }

        public bool HasMoreItems { get; private set; } = true;
    }
}
