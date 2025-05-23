//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml.Controls;
using System;
using System.Linq;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Media;
using Telegram.Converters;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td;
using Telegram.Td.Api;
using Telegram.Views.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace Telegram.Views.Stars.Popups
{
    public sealed partial class ReceivedGiftPopup : ContentPopup
    {
        private readonly IClientService _clientService;
        private readonly INavigationService _navigationService;
        private readonly IEventAggregator _aggregator;

        private readonly StarTransaction _transaction;

        private readonly string _transactionId;

        private readonly ReceivedGift _gift;
        private readonly MessageSender _receiverId;

        private readonly MessageSender _sendGiftTo;

        private GiftUpgradePreview _preview;
        private int _index;

        public ReceivedGiftPopup(IClientService clientService, INavigationService navigationService, ReceivedGift gift, MessageSender receiverId, MessageSender sendGiftTo)
        {
            InitializeComponent();

            _clientService = clientService;
            _navigationService = navigationService;
            _aggregator = TypeResolver.Current.Resolve<IEventAggregator>(clientService.SessionId);

            _gift = gift;
            _receiverId = receiverId;
            _sendGiftTo = sendGiftTo;

            if (gift.Gift is SentGiftRegular regular)
            {
                if (gift.CanBeUpgraded && IsOwned(clientService, receiverId))
                {
                    InitializeGift();
                }

                InitializeRegular(clientService, gift, regular.Gift, receiverId);
            }
            else if (gift.Gift is SentGiftUpgraded upgraded)
            {
                InitializeUpgraded(clientService, gift, upgraded.Gift);
            }
        }

        private bool IsOwned(IClientService clientService, MessageSender receiverId)
        {
            if (receiverId.IsUser(clientService.Options.MyId))
            {
                return true;
            }
            else if (clientService.TryGetSupergroup(receiverId, out Supergroup supergroup))
            {
                return supergroup.CanPostMessages();
            }

            return false;
        }

        private void InitializeRegular(IClientService clientService, ReceivedGift receivedGift, Gift gift, MessageSender receiverId)
        {
            DismissButtonRequestedTheme = ElementTheme.Default;
            UpgradedHeader.Visibility = Visibility.Collapsed;
            UpgradedRoot.Visibility = Visibility.Collapsed;
            MoreButton.Visibility = Visibility.Collapsed;

            if (clientService.TryGetUser(receivedGift.SenderId, out User user))
            {
                FromPhoto.SetUser(clientService, user, 24);
                FromPhoto.Visibility = Visibility.Visible;
                FromTitle.Text = user.FullName();
            }
            else if (clientService.TryGetChat(receivedGift.SenderId, out Chat chat))
            {
                FromPhoto.SetChat(clientService, chat, 24);
                FromPhoto.Visibility = Visibility.Visible;
                FromTitle.Text = chat.Title;
            }
            else
            {
                FromPhoto.Source = PlaceholderImage.GetGlyph(Icons.AuthorHiddenFilled, 5);
                FromPhoto.Visibility = Visibility.Visible;
                FromTitle.Text = Strings.StarsTransactionHidden;
            }

            From.Header = Strings.Gift2From;
            Title.Text = Strings.Gift2TitleReceived;

            if (IsOwned(clientService, receiverId))
            {
                if (receivedGift.IsSaved)
                {
                    if (receivedGift.Date + clientService.Options.GiftSellPeriod > DateTime.Now.ToTimestamp())
                    {
                        TextBlockHelper.SetMarkdown(Subtitle, Strings.Gift2InfoPinned);
                        Convert.Glyph = Locale.Declension(Strings.R.Gift2ButtonSell, receivedGift.SellStarCount);
                    }
                    else
                    {
                        TextBlockHelper.SetMarkdown(Subtitle, Locale.Declension(Strings.R.Gift2Info2Expired, gift.StarCount));
                        Convert.Visibility = Visibility.Collapsed;
                    }

                    Info.Text = Strings.Gift2ProfileVisible3;
                    PurchaseText.Text = Strings.OK;
                }
                else
                {
                    if (receivedGift.SellStarCount > 0 && receivedGift.Date + clientService.Options.GiftSellPeriod > DateTime.Now.ToTimestamp())
                    {
                        TextBlockHelper.SetMarkdown(Subtitle, Locale.Declension(Strings.R.Gift2Info, receivedGift.SellStarCount));
                        Convert.Glyph = Locale.Declension(Strings.R.Gift2ButtonSell, receivedGift.SellStarCount);
                    }
                    else
                    {
                        TextBlockHelper.SetMarkdown(Subtitle, Locale.Declension(Strings.R.Gift2Info2Expired, gift.StarCount));
                        Convert.Visibility = Visibility.Collapsed;
                    }

                    Info.Text = Strings.Gift2ProfileInvisible3;
                    PurchaseText.Text = Strings.OK;
                }

                if (receivedGift.CanBeUpgraded && receivedGift.PrepaidUpgradeStarCount > 0)
                {
                    TextBlockHelper.SetMarkdown(Subtitle, Strings.Gift2InfoInFreeUpgrade);

                    PurchaseText.Text = Strings.Gift2UpgradeButtonFree;
                }

                Info.Visibility = Visibility.Visible;
            }
            else
            {
                Subtitle.Visibility = Visibility.Collapsed;
                Convert.Visibility = Visibility.Collapsed;
                Status.Visibility = Visibility.Collapsed;
                Info.Visibility = Visibility.Collapsed;

                PurchaseText.Text = Strings.OK;

                if (receivedGift.CanBeUpgraded)
                {
                    TextBlockHelper.SetMarkdown(Subtitle, string.Format(Strings.Gift2ActionUpgradeOut, user.FullName(true)));
                }
                else
                {
                    TextBlockHelper.SetMarkdown(Subtitle, string.Format(Strings.Gift2Info2OutExpired, user.FullName(true)));
                }
            }

            AnimatedPhoto.LoopCount = 0;

            AnimatedPhoto.Source = new DelayedFileSource(clientService, gift.Sticker);

            StarCount.Text = gift.StarCount.ToString("N0");

            if (gift.TotalCount > 0)
            {
                Availability.Visibility = Visibility.Visible;
                Availability.Content = gift.RemainingText();
            }

            if (receivedGift.CanBeUpgraded && IsOwned(clientService, receiverId))
            {
                Status.Visibility = Visibility.Visible;
            }

            Date.Content = Formatter.DateAt(receivedGift.Date);

            if (receivedGift.Text?.Text.Length > 0)
            {
                TableRoot.BorderThickness = new Thickness(1, 1, 1, 0);
                TableRoot.CornerRadius = new CornerRadius(4, 4, 0, 0);

                CaptionRoot.Visibility = Visibility.Visible;
                Caption.SetText(clientService, receivedGift.Text);
            }
        }

        private void InitializeUpgraded(IClientService clientService, ReceivedGift receivedGift, UpgradedGift gift)
        {
            DismissButtonRequestedTheme = ElementTheme.Dark;
            Header.Visibility = Visibility.Collapsed;
            RegularRoot.Visibility = Visibility.Collapsed;
            MoreButton.Visibility = Visibility.Visible;

            var source = DelayedFileSource.FromSticker(clientService, gift.Symbol.Sticker);
            var centerColor = gift.Backdrop.Colors.CenterColor.ToColor();
            var edgeColor = gift.Backdrop.Colors.EdgeColor.ToColor();

            UpgradedHeader.Update(source, centerColor, edgeColor);
            UpgradedAnimatedPhoto.Source = DelayedFileSource.FromSticker(clientService, gift.Model.Sticker);
            UpgradedTitle.Text = gift.Title;
            UpgradedSubtitle.Text = Locale.Declension(Strings.R.Gift2CollectionNumber, gift.Number);

            if (clientService.TryGetUser(gift.OwnerId, out User user))
            {
                UpgradedFromPhoto.SetUser(clientService, user, 24);
                UpgradedFromPhoto.Visibility = Visibility.Visible;
                UpgradedFromTitle.Text = user.FullName();
            }
            else if (clientService.TryGetChat(gift.OwnerId, out Chat chat))
            {
                UpgradedFromPhoto.SetChat(clientService, chat, 24);
                UpgradedFromPhoto.Visibility = Visibility.Visible;
                UpgradedFromTitle.Text = chat.Title;
            }
            else
            {
                UpgradedFromPhoto.Visibility = Visibility.Collapsed;
                UpgradedFromText.Text = gift.OwnerName;
            }

            From.Header = Strings.Gift2From;
            Title.Text = Strings.Gift2TitleReceived;

            UpgradedModel.Text = gift.Model.Name;
            UpgradedModelRarity.Glyph = (gift.Model.RarityPerMille / 10d).ToString("0.##") + "%";
            UpgradedBackdrop.Text = gift.Backdrop.Name;
            UpgradedBackdropRarity.Glyph = (gift.Backdrop.RarityPerMille / 10d).ToString("0.##") + "%";
            UpgradedSymbol.Text = gift.Symbol.Name;
            UpgradedSymbolRarity.Glyph = (gift.Symbol.RarityPerMille / 10d).ToString("0.##") + "%";

            UpgradedQuantity.Content =
                Locale.Declension(Strings.R.Gift2QuantityIssued1, gift.TotalUpgradedCount) +
                Locale.Declension(Strings.R.Gift2QuantityIssued2, gift.MaxUpgradedCount);

            if (gift.OriginalDetails != null)
            {
                UpgradedTableRoot.BorderThickness = new Thickness(1, 1, 1, 0);
                UpgradedTableRoot.CornerRadius = new CornerRadius(4, 4, 0, 0);

                UpgradedCaptionRoot.Visibility = Visibility.Visible;

                var senderName = clientService.GetTitle(gift.OriginalDetails.SenderId);
                var senderText = senderName != null
                    ? new FormattedText(senderName, new[] { new TextEntity(0, senderName.Length, gift.OriginalDetails.SenderId.ToTextEntityType()) })
                    : null;

                var receiverName = clientService.GetTitle(gift.OriginalDetails.ReceiverId);
                var receiverText = receiverName != null
                    ? new FormattedText(receiverName, new[] { new TextEntity(0, receiverName.Length, gift.OriginalDetails.ReceiverId.ToTextEntityType()) })
                    : null;

                var date = Formatter.Date(gift.OriginalDetails.Date);
                var dateText = date.AsFormattedText();

                FormattedText text = null;

                if (gift.OriginalDetails.Text.Text.Length > 0)
                {
                    if (senderName.Length > 0 && receiverName.Length > 0)
                    {
                        text = ClientEx.Format(Strings.Gift2AttributeOriginalDetailsComment, senderText, receiverText, dateText, gift.OriginalDetails.Text);
                    }
                    else if (senderName.Length > 0)
                    {
                        text = ClientEx.Format(Strings.Gift2AttributeOriginalDetailsSelfComment, senderText, dateText, gift.OriginalDetails.Text);
                    }
                    else if (receiverName.Length > 0)
                    {
                        text = ClientEx.Format(Strings.Gift2AttributeOriginalDetailsNoSenderComment, receiverText, dateText, gift.OriginalDetails.Text);
                    }
                }
                else if (senderName.Length > 0 && receiverName.Length > 0)
                {
                    text = ClientEx.Format(Strings.Gift2AttributeOriginalDetails, senderText, receiverText, dateText);
                }
                else if (senderName.Length > 0)
                {
                    text = ClientEx.Format(Strings.Gift2AttributeOriginalDetailsSelf, senderText, dateText);
                }
                else if (receiverName.Length > 0)
                {
                    text = ClientEx.Format(Strings.Gift2AttributeOriginalDetailsNoSender, receiverText, dateText);
                }

                UpgradedCaption.SetText(clientService, text);
            }

            if (IsOwned(clientService, gift.OwnerId) && receivedGift.ReceivedGiftId.Length > 0)
            {
                if (receivedGift.IsSaved)
                {
                    Info.Text = gift.OwnerId is MessageSenderUser
                        ? Strings.Gift2ProfileVisible3
                        : Strings.Gift2ChannelProfileVisible3;
                }
                else
                {
                    Info.Text = gift.OwnerId is MessageSenderUser
                        ? Strings.Gift2ProfileInvisible3
                        : Strings.Gift2ChannelProfileInvisible3;
                }

                UpgradedButtons.Visibility = Visibility.Visible;

                if (gift.ResaleStarCount > 0)
                {
                    ResaleStarCountRoot.Visibility = Visibility.Visible;
                    ResaleStarCount.Text = gift.ResaleStarCount.ToString("N0");

                    ResellButton.Glyph = Icons.TagOffFilled;
                    ResellButton.Content = Strings.Gift2ActionUnlist;
                }
                else
                {
                    ResellButton.Glyph = Icons.TagFilled;
                    ResellButton.Content = Strings.Gift2ActionResell;
                }

                if (user != null)
                {
                    if (user.EmojiStatus?.Type is EmojiStatusTypeUpgradedGift upgradedGift && upgradedGift.UpgradedGiftId == gift.Id)
                    {
                        WearButton.Glyph = Icons.CrownOffFilled;
                        WearButton.Content = Strings.Gift2Unwear;
                    }
                    else
                    {
                        WearButton.Glyph = Icons.CrownFilled;
                        WearButton.Content = Strings.Gift2Wear;
                    }
                }

                PurchaseText.Text = Strings.OK;
            }
            else
            {
                Info.Visibility = Visibility.Collapsed;

                if (gift.ResaleStarCount > 0)
                {
                    ResaleStarCountRoot.Visibility = Visibility.Visible;
                    ResaleStarCount.Text = gift.ResaleStarCount.ToString("N0");

                    PurchaseText.Text = Locale.Declension(Strings.R.ResellGiftBuy, gift.ResaleStarCount).Replace("\u2B50", Icons.Premium + Icons.Spacing);
                }
                else
                {
                    PurchaseText.Text = Strings.OK;
                }
            }
        }

        public ReceivedGiftPopup(IClientService clientService, INavigationService navigationService, Gift gift)
        {
            InitializeComponent();

            _clientService = clientService;
            _navigationService = navigationService;

            DismissButtonRequestedTheme = ElementTheme.Default;
            UpgradedHeader.Visibility = Visibility.Collapsed;
            UpgradedRoot.Visibility = Visibility.Collapsed;

            From.Visibility = Visibility.Collapsed;

            Title.Text = Strings.Gift2SoldOutSheetTitle;
            Subtitle.Text = Strings.Gift2SoldOutSheetSubtitle;
            Subtitle.Foreground = BootStrapper.Current.Resources["SystemFillColorCriticalBrush"] as Brush;

            FirstSale.Visibility = Visibility.Visible;
            LastSale.Visibility = Visibility.Visible;

            FirstSale.Content = Formatter.DateAt(gift.FirstSendDate);
            LastSale.Content = Formatter.DateAt(gift.LastSendDate);

            Convert.Visibility = Visibility.Collapsed;
            Info.Visibility = Visibility.Collapsed;

            AnimatedPhoto.LoopCount = 0;
            AnimatedPhoto.Source = new DelayedFileSource(clientService, gift.Sticker);

            Date.Visibility = Visibility.Collapsed;

            StarCount.Text = gift.StarCount.ToString("N0");

            Availability.Visibility = Visibility.Visible;
            Availability.Content = gift.RemainingText();

            PurchaseText.Text = Strings.OK;
        }

        private void Purchase_Click(object sender, RoutedEventArgs e)
        {
            if (_submitted)
            {
                return;
            }

            _submitted = true;

            if (_gift?.Gift is SentGiftRegular && (_gift.PrepaidUpgradeStarCount > 0 || !_upgradeCollapsed))
            {
                Upgrade2();
            }
            else if (_gift?.Gift is SentGiftUpgraded { Gift.ResaleStarCount: > 0 } && !IsOwned(_clientService, _receiverId))
            {
                BuyResale();
            }
            else
            {
                Hide(ContentDialogResult.Primary);
            }
        }

        private bool _submitted;

        private async void Upgrade2()
        {
            if (_gift.Gift is not SentGiftRegular regular)
            {
                return;
            }

            PurchaseRing.Visibility = Windows.UI.Xaml.Visibility.Visible;

            var visual1 = ElementComposition.GetElementVisual(PurchaseText);
            var visual2 = ElementComposition.GetElementVisual(PurchaseRing);

            ElementCompositionPreview.SetIsTranslationEnabled(PurchaseText, true);
            ElementCompositionPreview.SetIsTranslationEnabled(PurchaseRing, true);

            var translate1 = visual1.Compositor.CreateScalarKeyFrameAnimation();
            translate1.InsertKeyFrame(0, 0);
            translate1.InsertKeyFrame(1, -32);

            var translate2 = visual1.Compositor.CreateScalarKeyFrameAnimation();
            translate2.InsertKeyFrame(0, 32);
            translate2.InsertKeyFrame(1, 0);

            visual1.StartAnimation("Translation.Y", translate1);
            visual2.StartAnimation("Translation.Y", translate2);

            //await Task.Delay(2000);

            var response = await _clientService.SendAsync(new UpgradeGift(string.Empty, _gift.ReceivedGiftId, KeepOriginalDetails.IsChecked is true, _gift.PrepaidUpgradeStarCount > 0 ? 0 : regular.Gift.UpgradeStarCount));
            if (response is UpgradeGiftResult result)
            {
                var id = _gift.ReceivedGiftId;

                _gift.ReceivedGiftId = result.ReceivedGiftId;
                _gift.ExportDate = result.ExportDate;
                _gift.TransferStarCount = result.TransferStarCount;
                _gift.CanBeTransferred = result.CanBeTransferred;
                _gift.IsSaved = result.IsSaved;
                _gift.Gift = new SentGiftUpgraded(result.Gift);

                _aggregator.Publish(new UpdateGiftUpgraded(id, _gift));

                UpgradedAnimatedPhoto.LoopCompleted -= OnLoopCompleted;

                DismissButtonRequestedTheme = ElementTheme.Dark;
                UpgradedHeader.Visibility = Visibility.Visible;
                UpgradedRoot.Visibility = Visibility.Visible;

                DetailRoot.Visibility = Visibility.Visible;
                UpgradeRoot.Visibility = Visibility.Collapsed;

                InitializeUpgraded(_clientService, _gift, result.Gift);
            }
            else if (response is Error error)
            {
                ToastPopup.ShowError(XamlRoot, error);
            }

            _submitted = false;

            translate1.InsertKeyFrame(0, 32);
            translate1.InsertKeyFrame(1, 0);

            translate2.InsertKeyFrame(0, 0);
            translate2.InsertKeyFrame(1, -32);

            visual1.StartAnimation("Translation.Y", translate1);
            visual2.StartAnimation("Translation.Y", translate2);

            //Hide();
            //ViewModel.Submit();
        }

        private async void Convert_Click(object sender, RoutedEventArgs e)
        {
            if (_gift?.Gift is SentGiftRegular regular)
            {
                var expiration = Formatter.ToLocalTime(_gift.Date + _clientService.Options.GiftSellPeriod);
                var diff = expiration - DateTime.Now;

                var message = Locale.Declension(Strings.R.Gift2ConvertText2, (long)diff.TotalDays, _clientService.GetTitle(_gift.SenderId), Locale.Declension(Strings.R.StarsCount, regular.Gift.StarCount));

                var confirm = await MessagePopup.ShowAsync(XamlRoot, target: null, message, Strings.Gift2ConvertTitle, Strings.Gift2ConvertButton, Strings.Cancel);
                if (confirm == ContentDialogResult.Primary)
                {
                    var response = await _clientService.SendAsync(new SellGift(string.Empty, _gift.ReceivedGiftId));
                    if (response is Ok)
                    {
                        Hide(ContentDialogResult.Secondary);

                        _aggregator.Publish(new UpdateGiftIsSold(_gift.ReceivedGiftId));
                        _navigationService.Navigate(typeof(StarsPage));

                        ToastPopup.Show(XamlRoot, string.Format("**{0}**\n{1}", Strings.Gift2ConvertedTitle, Locale.Declension(Strings.R.Gift2Converted, regular.Gift.StarCount)), ToastPopupIcon.StarsTopup);
                    }
                }
            }
        }

        private async void Visibility_Click(object sender, TextUrlClickEventArgs e)
        {
            var response = await _clientService.SendAsync(new ToggleGiftIsSaved(_gift.ReceivedGiftId, !_gift.IsSaved));
            if (response is Ok)
            {
                _gift.IsSaved = !_gift.IsSaved;
                _aggregator.Publish(new UpdateGiftIsSaved(_gift.ReceivedGiftId, _gift.IsSaved));

                if (_gift.Gift is SentGiftRegular regular)
                {
                    InitializeRegular(_clientService, _gift, regular.Gift, _receiverId);
                }

                if (_gift.IsSaved)
                {
                    ToastPopup.Show(XamlRoot, string.Format("**{0}**\n{1}", Strings.Gift2MadePublicTitle, Strings.Gift2MadePublic), new DelayedFileSource(_clientService, _gift.GetSticker()));
                }
                else
                {
                    ToastPopup.Show(XamlRoot, string.Format("**{0}**\n{1}", Strings.Gift2MadePrivateTitle, Strings.Gift2MadePrivate), new DelayedFileSource(_clientService, _gift.GetSticker()));
                }
            }
        }

        private void UpgradedModelRarity_Click(object sender, RoutedEventArgs e)
        {
            if (_gift.Gift is SentGiftUpgraded upgraded)
            {
                ToastPopup.Show(UpgradedModelRarity, string.Format(Strings.Gift2RarityHint, (upgraded.Gift.Model.RarityPerMille / 10d).ToString("0.##") + "%"), TeachingTipPlacementMode.Top);
            }
        }

        private void UpgradedBackdropRarity_Click(object sender, RoutedEventArgs e)
        {
            if (_gift.Gift is SentGiftUpgraded upgraded)
            {
                ToastPopup.Show(UpgradedBackdropRarity, string.Format(Strings.Gift2RarityHint, (upgraded.Gift.Backdrop.RarityPerMille / 10d).ToString("0.##") + "%"), TeachingTipPlacementMode.Top);
            }
        }

        private void UpgradedSymbolRarity_Click(object sender, RoutedEventArgs e)
        {
            if (_gift.Gift is SentGiftUpgraded upgraded)
            {
                ToastPopup.Show(UpgradedSymbolRarity, string.Format(Strings.Gift2RarityHint, (upgraded.Gift.Symbol.RarityPerMille / 10d).ToString("0.##") + "%"), TeachingTipPlacementMode.Top);
            }
        }

        private bool _upgradeCollapsed = true;

        private void ShowHideUpgrade(bool show)
        {
            if (_upgradeCollapsed != show)
            {
                return;
            }

            _upgradeCollapsed = !show;


            DismissButtonRequestedTheme = show ? ElementTheme.Dark : ElementTheme.Default;
            Header.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
            UpgradedHeader.Visibility = show ? Visibility.Visible : Visibility.Collapsed;

            DetailRoot.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
            UpgradeRoot.Visibility = show ? Visibility.Visible : Visibility.Collapsed;

            UpgradedTitle.Text = Strings.Gift2UpgradeTitle;
            UpgradedSubtitle.Text = Strings.Gift2UpgradeText;

            if (show)
            {
                UpdateGift();

                if (_gift.Gift is SentGiftRegular regular)
                {
                    PurchaseText.Text = _gift.PrepaidUpgradeStarCount > 0
                        ? Strings.Gift2UpgradeButtonFree
                        : string.Format(Strings.Gift2UpgradeButton, regular.Gift.UpgradeStarCount).Replace("\u2B50", Icons.Premium);
                }
            }
        }

        private void Upgrade_Click(object sender, RoutedEventArgs e)
        {
            ShowHideUpgrade(true);
        }

        private async void InitializeGift()
        {
            UpgradedAnimatedPhoto.LoopCompleted += OnLoopCompleted;

            var gift = _gift.Gift as SentGiftRegular;

            var response = await _clientService.SendAsync(new GetGiftUpgradePreview(gift.Gift.Id));
            if (response is GiftUpgradePreview preview)
            {
                foreach (var item in preview.Models.Reverse())
                {
                    _clientService.DownloadFile(item.Sticker.StickerValue.Id, 32);
                }

                foreach (var item in preview.Symbols.Reverse())
                {
                    _clientService.DownloadFile(item.Sticker.StickerValue.Id, 31);
                }

                _preview = preview;

                if (_upgradeCollapsed is false)
                {
                    UpdateGift();
                }
            }
        }

        private void OnLoopCompleted(object sender, AnimatedImageLoopCompletedEventArgs e)
        {
            this.BeginOnUIThread(UpdateGift);
        }

        private void UpdateGift()
        {
            if (_preview == null)
            {
                return;
            }

            var random = new Random(_index++);

            var model = _preview.Models[random.Next(_preview.Models.Count)];
            var symbol = _preview.Symbols[random.Next(_preview.Symbols.Count)];
            var backdrop = _preview.Backdrops[random.Next(_preview.Backdrops.Count)];

            var pattern = new DelayedFileSource(_clientService, symbol.Sticker);
            var centerColor = backdrop.Colors.CenterColor.ToColor();
            var edgeColor = backdrop.Colors.EdgeColor.ToColor();

            UpgradedAnimatedPhoto.Source = new DelayedFileSource(_clientService, model.Sticker);
            UpgradedHeader.Update(pattern, centerColor, edgeColor);
        }

        protected override void OnDismissButtonClick()
        {
            if (_upgradeCollapsed)
            {
                base.OnDismissButtonClick();
            }
            else
            {
                ShowHideUpgrade(false);

                if (_gift.Gift is SentGiftRegular regular)
                {
                    InitializeRegular(_clientService, _gift, regular.Gift, _receiverId);
                }
            }
        }

        private void More_ContextRequested(object sender, RoutedEventArgs e)
        {
            var flyout = new MenuFlyout();

            flyout.CreateFlyoutItem(CopyLink, Strings.CopyLink, Icons.Link);
            flyout.CreateFlyoutItem(Share, Strings.ShareFile, Icons.Share);

            if (_gift.Gift is SentGiftUpgraded upgraded && upgraded.Gift.ResaleStarCount > 0 && upgraded.Gift.OwnerId.AreTheSame(_clientService.MyId))
            {
                flyout.CreateFlyoutItem(ChangePrice, Strings.Gift2ChangePrice, Icons.Tag);
            }

            if (_gift.CanBeTransferred)
            {
                flyout.CreateFlyoutItem(Transfer, Strings.Gift2TransferOption, Icons.Replace);
            }

            flyout.ShowAt(sender as UIElement, FlyoutPlacementMode.BottomEdgeAlignedRight);
        }

        private void CopyLink()
        {
            if (_gift.Gift is SentGiftUpgraded upgraded)
            {
                MessageHelper.CopyLink(_clientService, XamlRoot, new InternalLinkTypeUpgradedGift(upgraded.Gift.Name));
            }
        }

        private void Share()
        {
            if (_gift.Gift is SentGiftUpgraded upgraded)
            {
                Hide();
                _navigationService.ShowPopup(new ChooseChatsPopup(), new ChooseChatsConfigurationPostLink(new InternalLinkTypeUpgradedGift(upgraded.Gift.Name)));
            }
        }

        private async void Resell()
        {
            if (_gift.Gift is not SentGiftUpgraded upgraded)
            {
                return;
            }

            var now = DateTime.Now.ToTimestamp();
            if (now < _gift.NextResaleDate)
            {
                var date = Formatter.ToLocalTime(_gift.NextResaleDate);
                var diff = date - DateTime.Now;

                string message;
                if (diff.TotalDays >= 1)
                {
                    message = Formatter.DateAt(_gift.NextResaleDate);
                    message = string.Format(Strings.Gift2ResellTimeoutDate, message);
                }
                else
                {
                    message = Locale.FormatTtl(_gift.NextResaleDate - now);
                    message = string.Format(Strings.Gift2ResellTimeout, message);
                }

                _navigationService.ShowPopup(message, Strings.Gift2ResellTimeoutTitle, Strings.OK);
            }
            else if (upgraded.Gift.ResaleStarCount > 0)
            {
                var confirm = await _navigationService.ShowPopupAsync(Strings.Gift2UnlistText, string.Format(Strings.Gift2UnlistTitle, upgraded.Gift.ToName()), Strings.Gift2ActionUnlist, Strings.Cancel);
                if (confirm == ContentDialogResult.Primary)
                {
                    var response = await _clientService.SendAsync(new SetGiftResalePrice(_gift.ReceivedGiftId, 0));
                    if (response is Ok)
                    {
                        upgraded.Gift.ResaleStarCount = 0;

                        ResaleStarCountRoot.Visibility = Visibility.Collapsed;

                        ResellButton.Glyph = Icons.TagFilled;
                        ResellButton.Content = Strings.Gift2ActionResell;
                    }
                    else if (response is Error error)
                    {
                        ToastPopup.ShowError(XamlRoot, error);
                    }
                }
            }
            else
            {
                ChangePrice();
            }
        }

        private async void ChangePrice()
        {
            if (_gift.Gift is not SentGiftUpgraded upgraded)
            {
                return;
            }

            var popup = new InputTeachingTip(InputPopupType.Stars);
            popup.Value = Math.Clamp(upgraded.Gift.ResaleStarCount, _clientService.Options.GiftResaleStarCountMin, _clientService.Options.GiftResaleStarCountMax);
            //popup.Minimum = _clientService.Options.GiftResaleStarCountMin;
            popup.Maximum = _clientService.Options.GiftResaleStarCountMax;

            popup.Title = Strings.ResellGiftTitle;
            popup.Header = Strings.ResellGiftPriceTitle;
            popup.ActionButtonContent = Strings.ResellGiftButton;
            popup.ActionButtonStyle = BootStrapper.Current.Resources["AccentButtonStyle"] as Style;
            popup.CloseButtonContent = Strings.Cancel;
            popup.PreferredPlacement = TeachingTipPlacementMode.Center;
            popup.IsLightDismissEnabled = false;
            popup.ShouldConstrainToRootBounds = true;

            popup.ValueChanged += (s, args) =>
            {
                if (args.Value < _clientService.Options.GiftResaleStarCountMin)
                {
                    args.Footer = Locale.Declension(Strings.R.ResellGiftInfoMin, _clientService.Options.GiftResaleStarCountMin);
                }
                else
                {
                    string ConvertResaleFee(long value)
                    {
                        var xtr = value / 1000d;
                        var usd = xtr * _clientService.Options.GiftResaleEarningsPerMille;

                        return Formatter.FormatAmount((long)usd, "USD");
                    }

                    args.Footer = string.Format("{0} ~{1}", Locale.Declension(Strings.R.ResellGiftInfo, args.Value), ConvertResaleFee(args.Value));
                }
            };

            popup.Validating += (s, args) =>
            {
                if (args.Value < _clientService.Options.GiftResaleStarCountMin)
                {
                    _navigationService.ShowToast(Locale.Declension(Strings.R.ResellGiftInfoMin, _clientService.Options.GiftResaleStarCountMin), ToastPopupIcon.Info);
                    args.Cancel = true;
                }
            };

            var confirm = await popup.ShowAsync();
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            var response = await _clientService.SendAsync(new SetGiftResalePrice(_gift.ReceivedGiftId, popup.Value));
            if (response is Ok)
            {
                upgraded.Gift.ResaleStarCount = popup.Value;

                _aggregator.Publish(new UpdateGiftUpgraded(_gift.ReceivedGiftId, _gift));

                ResaleStarCountRoot.Visibility = Visibility.Visible;
                ResaleStarCount.Text = upgraded.Gift.ResaleStarCount.ToString("N0");

                ResellButton.Glyph = Icons.TagOffFilled;
                ResellButton.Content = Strings.Gift2ActionUnlist;
            }
            else if (response is Error error)
            {
                ToastPopup.ShowError(XamlRoot, error);
            }
        }

        private void Transfer()
        {
            if (_gift.Gift is not SentGiftUpgraded upgraded)
            {
                return;
            }

            var now = DateTime.Now.ToTimestamp();
            if (now < _gift.NextTransferDate)
            {
                var date = Formatter.ToLocalTime(_gift.NextTransferDate);
                var diff = date - DateTime.Now;

                string message;
                if (diff.TotalDays >= 1)
                {
                    message = Formatter.DateAt(_gift.NextTransferDate);
                    message = string.Format(Strings.Gift2TransferTimeoutDate, message);
                }
                else
                {
                    message = Locale.FormatTtl(_gift.NextTransferDate - now);
                    message = string.Format(Strings.Gift2TransferTimeout, message);
                }

                _navigationService.ShowPopup(message, Strings.Gift2TransferTimeoutTitle, Strings.OK);
            }
            else
            {
                Hide();
                _navigationService.ShowPopup(new ChooseChatsPopup(), new ChooseChatsConfigurationTransferGift(_gift));
            }
        }

        private async void BuyResale()
        {
            if (_gift.Gift is not SentGiftUpgraded upgraded)
            {
                return;
            }

            var chat = await _clientService.GetChatFromMessageSenderAsync(_sendGiftTo);

            var confirm = await TransferGiftPopup.ShowAsync(XamlRoot, _clientService, _gift, chat, true);
            if (confirm == ContentDialogResult.Primary)
            {
                var response = await _clientService.SendPaymentAsync(upgraded.Gift.ResaleStarCount, new SendResoldGift(upgraded.Gift.Name, _sendGiftTo, upgraded.Gift.ResaleStarCount));
                if (response is Ok)
                {
                    _aggregator.Publish(new UpdateGiftIsSold(_gift.ReceivedGiftId));
                    Hide(ContentDialogResult.Primary);

                    if (chat != null)
                    {
                        _navigationService.NavigateToChat(chat.Id);
                        ToastPopup.Show(XamlRoot, string.Format("**{0}**\n{1}", Strings.BoughtResoldGiftToTitle, string.Format(Strings.BoughtResoldGiftToText, chat.Title)), new DelayedFileSource(_clientService, upgraded.Gift.Model.Sticker));
                    }
                    else
                    {
                        ToastPopup.Show(XamlRoot, string.Format("**{0}**\n{1}", Strings.BoughtResoldGiftTitle, string.Format(Strings.BoughtResoldGiftText, upgraded.Gift.ToName())), new DelayedFileSource(_clientService, upgraded.Gift.Model.Sticker));
                    }
                }
                else if (response is Error error)
                {
                    _submitted = false;
                    ToastPopup.ShowError(XamlRoot, error);
                }
                else if (response is ErrorStarsNeeded)
                {
                    Hide();
                    await _navigationService.ShowPopupAsync(new BuyPopup(), BuyStarsArgs.ForChannel(upgraded.Gift.ResaleStarCount, 0));
                }
            }
            else
            {
                _submitted = false;
            }
        }

        private void From_Click(Hyperlink sender, HyperlinkClickEventArgs args)
        {
            Hide();
            _navigationService.NavigateToSender(_gift.SenderId);
        }

        private void Owner_Click(Hyperlink sender, HyperlinkClickEventArgs args)
        {
            Hide();
            _navigationService.NavigateToSender(_receiverId);
        }

        private void TransferButton_Click(object sender, RoutedEventArgs e)
        {
            Transfer();
        }

        private void WearButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ResellButton_Click(object sender, RoutedEventArgs e)
        {
            Resell();
        }
    }
}
