//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Common;
using Telegram.Controls.Cells;
using Telegram.Controls.Media;
using Telegram.Td.Api;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Telegram.Views.Profile
{
    public sealed partial class ProfileGiftsTabPage : ProfileTabPage
    {
        public ProfileGiftsTabPage()
        {
            InitializeComponent();
        }

        private new void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new GridViewItem();
                args.ItemContainer.Style = sender.ItemContainerStyle;
                args.ItemContainer.ContentTemplate = sender.ItemTemplate;

                args.ItemContainer.ContextRequested += OnContextRequested;
            }

            args.IsContainerPrepared = true;
        }

        private void OnContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var gift = ScrollingHost.ItemFromContainer(sender) as ReceivedGift;

            var flyout = new MenuFlyout();

            if (gift.Gift is SentGiftUpgraded)
            {
                if (ViewModel.GiftsTab.IsOwned())
                {
                    flyout.CreateFlyoutItem(ViewModel.GiftsTab.PinGift, gift, gift.IsPinned ? Strings.Gift2Unpin : Strings.Gift2Pin, gift.IsPinned ? Icons.PinOff : Icons.Pin);
                }

                flyout.CreateFlyoutItem(ViewModel.GiftsTab.CopyGift, gift, Strings.CopyLink, Icons.Link);
                flyout.CreateFlyoutItem(ViewModel.GiftsTab.ShareGift, gift, Strings.ShareFile, Icons.Share);
            }

            if (ViewModel.GiftsTab.IsOwned())
            {
                flyout.CreateFlyoutItem(ViewModel.GiftsTab.ToggleGift, gift, gift.IsSaved ? Strings.Gift2HideGift : Strings.Gift2ShowGift, gift.IsSaved ? Icons.EyeOff : Icons.Eye);
            }

            if (gift.CanBeTransferred)
            {
                flyout.CreateFlyoutItem(ViewModel.GiftsTab.TransferGift, gift, Strings.Gift2TransferOption, Icons.ArrowExit);
            }

            flyout.ShowAt(sender, args);
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }
            else if (args.ItemContainer.ContentTemplateRoot is ReceivedGiftCell content && args.Item is ReceivedGift gift)
            {
                content.UpdateGift(ViewModel.ClientService, gift);
            }

            args.Handled = true;
        }

        private void OnItemClick(object sender, ItemClickEventArgs e)
        {
            ViewModel.GiftsTab.OpenGift(e.ClickedItem as ReceivedGift);
        }
    }
}
