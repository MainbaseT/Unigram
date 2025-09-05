//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Collections.Generic;
using System.Linq;
using Telegram.Controls;
using Telegram.Controls.Cells;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels.Profile;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Popups
{
    public sealed partial class ChooseGiftsPopup : ContentPopup
    {
        private readonly IClientService _clientService;

        public ChooseGiftsPopup(ProfileGiftsTabViewModel viewModel)
        {
            InitializeComponent();

            _clientService = viewModel.ClientService;

            Title = Strings.StoriesAlbumMenuAddStories;
            PrimaryButtonText = Strings.StoriesAlbumMenuAddStories;
            SecondaryButtonText = Strings.Cancel;

            ScrollingHost.ItemsSource = viewModel.Items;
        }

        public IEnumerable<ReceivedGift> SelectedItems => ScrollingHost.SelectedItems
            .Cast<ReceivedGift>()
            .OrderByDescending(x => x.Date);

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new GridViewItem();
                args.ItemContainer.Style = sender.ItemContainerStyle;
                args.ItemContainer.ContentTemplate = sender.ItemTemplate;
            }

            args.IsContainerPrepared = true;
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }
            else if (args.ItemContainer.ContentTemplateRoot is ReceivedGiftCell cell && args.Item is ReceivedGift gift)
            {
                cell.UpdateGift(_clientService, gift);
                args.Handled = true;
            }
        }
    }
}
