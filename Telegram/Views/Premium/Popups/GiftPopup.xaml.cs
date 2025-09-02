//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Rg.DiffUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Cells;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views.Stars.Popups;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Media.Animation;

namespace Telegram.Views.Premium.Popups
{
    public enum GiftGroupType
    {
        All,
        Mine,
        Limited,
        InStock,
        Resale,
        StarCount
    }

    public partial class GiftGroup : KeyedList<GiftGroupType, AvailableGift>
    {
        public GiftGroup(GiftGroupType key, IEnumerable<AvailableGift> source)
            : base(key, source)
        {
            if (key == GiftGroupType.StarCount)
            {
                StarCount = this[0].Gift.StarCount;
            }
        }

        public long StarCount { get; }
    }

    public sealed partial class GiftPopup : ContentPopup
    {
        private readonly IClientService _clientService;
        private readonly INavigationService _navigationService;

        private readonly MessageSender _receiverId;

        private readonly DiffObservableCollection<AvailableGift> _gifts = new(Constants.DiffOptions);

        public GiftPopup(IClientService clientService, INavigationService navigationService, User user, UserFullInfo fullInfo)
        {
            InitializeComponent();

            _clientService = clientService;
            _navigationService = navigationService;

            _receiverId = new MessageSenderUser(user.Id);

            Photo.SetUser(clientService, user, 96);

            if (user.Id != clientService.Options.MyId)
            {
                TextBlockHelper.SetMarkdown(PremiumInfo, string.Format(Strings.Gift2PremiumInfo, user.FirstName));

                StarsTitle.Text = Strings.Gift2Stars;
                TextBlockHelper.SetMarkdown(StarsInfo, string.Format(Strings.Gift2StarsInfo, user.FirstName));

                AddLink(PremiumInfo, Strings.Gift2PremiumInfoLink, PremiumInfoLink_Click);
                AddLink(StarsInfo, Strings.Gift2StarsInfoLink, StarsInfoLink_Click);

                InitializeOptions(clientService);
            }
            else
            {
                PremiumTitle.Visibility = Visibility.Collapsed;
                PremiumInfo.Visibility = Visibility.Collapsed;

                StarsTitle.Text = Strings.Gift2StarsSelf;
                TextBlockHelper.SetMarkdown(StarsInfo, Strings.Gift2StarsSelfInfo1 + "\n\n" + Strings.Gift2StarsSelfInfo2);
            }

            ScrollingHost.ItemsSource = _gifts;

            InitializeGifts(clientService, fullInfo.Birthdate?.Day == DateTime.Today.Day
                    && fullInfo.Birthdate?.Month == DateTime.Today.Month);
        }

        public GiftPopup(IClientService clientService, INavigationService navigationService, Chat chat)
        {
            InitializeComponent();

            _clientService = clientService;
            _navigationService = navigationService;

            _receiverId = chat.ToMessageSender();

            Photo.SetChat(clientService, chat, 96);

            PremiumTitle.Visibility = Visibility.Collapsed;
            PremiumInfo.Visibility = Visibility.Collapsed;

            StarsTitle.Text = Strings.Gift2StarsChannel;
            TextBlockHelper.SetMarkdown(StarsInfo, string.Format(Strings.Gift2StarsChannelInfo, chat.Title));

            AddLink(StarsInfo, Strings.Gift2StarsInfoLink, StarsInfoLink_Click);

            ScrollingHost.ItemsSource = _gifts;

            InitializeGifts(clientService, false);
        }

        private void AddLink(TextBlock block, string text, TypedEventHandler<Hyperlink, HyperlinkClickEventArgs> handler)
        {
            var hyperlink = new Hyperlink();
            hyperlink.UnderlineStyle = UnderlineStyle.None;
            hyperlink.Inlines.Add(text);
            hyperlink.Click += handler;

            block.Inlines.Add(" ");
            block.Inlines.Add(hyperlink);
        }

        private void PremiumInfoLink_Click(Hyperlink sender, HyperlinkClickEventArgs args)
        {
            Hide();
            _navigationService.ShowPromo();
        }

        private void StarsInfoLink_Click(Hyperlink sender, HyperlinkClickEventArgs args)
        {
            // TODO: stars promo
        }

        private async void InitializeOptions(IClientService clientService)
        {
            PremiumOptions.ItemsSource = new[]
            {
                new PremiumGiftPaymentOption(string.Empty, 0, 0, 0, 0, string.Empty, null),
                new PremiumGiftPaymentOption(string.Empty, 0, 0, 0, 0, string.Empty, null),
                new PremiumGiftPaymentOption(string.Empty, 0, 0, 0, 0, string.Empty, null),
            };

            var response = await clientService.SendAsync(new GetPremiumGiftPaymentOptions());
            if (response is PremiumGiftPaymentOptions options)
            {
                PremiumOptions.ItemsSource = options.Options
                    .OrderBy(x => x.MonthCount)
                    .ToList();
            }
        }

        private async void InitializeGifts(IClientService clientService, bool birthday)
        {
            var response = await clientService.SendAsync(new GetAvailableGifts());
            if (response is AvailableGifts gifts)
            {
                var all = new List<AvailableGift>();
                var remaining = new List<AvailableGift>();

                foreach (var gift in gifts.Gifts)
                {
                    if (gift.Gift.IsForBirthday && birthday)
                    {
                        all.Add(gift);
                    }
                    else
                    {
                        remaining.Add(gift);
                    }
                }

                all.AddRange(remaining);

                var navigation = new List<GiftGroup>();
                navigation.Add(new GiftGroup(GiftGroupType.All, all));

                if (all.Any(x => x.Gift.OverallLimits != null))
                {
                    navigation.Add(new GiftGroup(GiftGroupType.Limited, all.Where(x => x.Gift.OverallLimits != null)));
                }

                if (all.Any(x => x.Gift.OverallLimits == null || x.Gift.OverallLimits.RemainingCount > 0))
                {
                    navigation.Add(new GiftGroup(GiftGroupType.InStock, all.Where(x => x.Gift.OverallLimits == null || x.Gift.OverallLimits.RemainingCount > 0)));
                }

                if (all.Any(x => x.MinResaleStarCount > 0))
                {
                    navigation.Add(new GiftGroup(GiftGroupType.Resale, all.Where(x => x.MinResaleStarCount > 0)));
                }

                var groups = all
                    .GroupBy(x => x.Gift.StarCount)
                    .OrderBy(x => x.Key);

                foreach (var group in groups)
                {
                    navigation.Add(new GiftGroup(GiftGroupType.StarCount, group));
                }

                Navigation.ItemsSource = navigation;
                Navigation.SelectedIndex = 0;
            }
        }

        private async void OnItemClick(object sender, ItemClickEventArgs e)
        {
            ContentDialogResult confirm = ContentDialogResult.Primary;
            Hide();

            if (e.ClickedItem is AvailableGift gift)
            {
                if (gift.Gift.OverallLimits == null || gift.Gift.OverallLimits.RemainingCount > 0)
                {
                    await _clientService.SendAsync(new CreatePrivateChat(_clientService.Options.MyId, false));
                    confirm = await _navigationService.ShowPopupAsync(new SendGiftPopup(_clientService, _navigationService, gift.Gift, _receiverId));
                }
                else if (gift.MinResaleStarCount > 0)
                {
                    confirm = await _navigationService.ShowPopupAsync(new ResoldGiftsPopup(_clientService, _navigationService, gift, _receiverId));
                }
                else
                {
                    await _navigationService.ShowPopupAsync(new ReceivedGiftPopup(_clientService, _navigationService, gift.Gift));
                    confirm = ContentDialogResult.None;
                }
            }
            else if (e.ClickedItem is PremiumGiftPaymentOption option && _receiverId is MessageSenderUser user)
            {
                confirm = await _navigationService.ShowPopupAsync(new SendGiftPopup(_clientService, _navigationService, option, user.UserId));
            }

            if (confirm != ContentDialogResult.Primary)
            {
                await this.ShowQueuedAsync(XamlRoot);
            }
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }
            else if (args.ItemContainer.ContentTemplateRoot is ReceivedGiftCell receivedGiftCell && args.Item is AvailableGift gift)
            {
                receivedGiftCell.UpdateGift(_clientService, gift);
            }
            else if (args.ItemContainer.ContentTemplateRoot is PremiumGiftCell premiumGiftCell && args.Item is PremiumGiftPaymentOption option)
            {
                premiumGiftCell.UpdatePremiumGift(_clientService, option);
            }

            args.Handled = true;
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Navigation.SelectedItem is GiftGroup group)
            {
                _gifts.ReplaceDiff(group);
                return;

                ScrollingHost.ItemContainerTransitions.Clear();

                var diffResult = DiffUtil.CalculateDiff(_gifts, group, _gifts.DefaultDiffHandler, _gifts.DefaultOptions);
                if (diffResult.MovedItems.Count == 0)
                {
                    ScrollingHost.ItemContainerTransitions.Add(new AddDeleteThemeTransition());
                }

                ScrollingHost.ItemContainerTransitions.Add(new RepositionThemeTransition());

                _gifts.ReplaceDiff(diffResult);
            }
        }

        public static Visibility ConvertGiftGroupStartCountVisibility(GiftGroupType type)
        {
            return type == GiftGroupType.StarCount
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        public static string ConvertGiftGroupStarCountText(GiftGroupType type, long starCount)
        {
            return type switch
            {
                GiftGroupType.All => Strings.Gift2TabAll,
                GiftGroupType.Mine => Strings.Gift2TabMine,
                GiftGroupType.Limited => Strings.Gift2TabLimited,
                GiftGroupType.InStock => Strings.Gift2TabInStock,
                GiftGroupType.Resale => Strings.Gift2TabResale,
                _ => starCount.ToString("N0")
            };
        }
    }
}
