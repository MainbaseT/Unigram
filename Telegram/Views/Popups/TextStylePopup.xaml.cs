//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Controls;
using Telegram.Controls.Drawers;
using Telegram.Controls.Messages;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels.Drawers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Popups
{
    public sealed partial class TextStylePopup : ContentPopup
    {
        private readonly IClientService _clientService;
        private readonly INavigationService _navigationService;

        private readonly TextCompositionStyle _style;

        private long _customEmojiId;

        public TextStylePopup(IClientService clientService, INavigationService navigationService)
        {
            InitializeComponent();

            _clientService = clientService;
            _navigationService = navigationService;

            TitleText.Text = Strings.AIEditorNewStyle;

            PrimaryButtonText = Strings.Create;
            SecondaryButtonText = Strings.Cancel;
        }

        public TextStylePopup(IClientService clientService, INavigationService navigationService, TextCompositionStyle style)
        {
            InitializeComponent();

            _clientService = clientService;
            _navigationService = navigationService;

            _style = style;

            _customEmojiId = style.CustomEmojiId;
            Icon.Source = new CustomEmojiFileSource(_clientService, style.CustomEmojiId);
            IconPlaceholder.Visibility = Visibility.Collapsed;

            TitleText.Text = Strings.AIEditorEditStyle;

            Title.Text = style.Title;
            Prompt.Text = style.Prompt;
            AddLink.IsChecked = style.CreatorUserId != 0;

            PrimaryButtonText = Strings.Save;
            SecondaryButtonText = Strings.Cancel;
        }

        public override void OnNavigatedTo(object parameter)
        {

        }

        private void Title_Loaded(object sender, RoutedEventArgs e)
        {
            Title.Focus(FocusState.Keyboard);
        }

        private void Emoji_ItemClick(object sender, EmojiDrawerItemClickEventArgs e)
        {

        }

        private async void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            IsPrimaryButtonPending = true;

            var deferral = args.GetDeferral();

            Function function;
            if (_style != null)
            {
                function = new EditTextCompositionStyle(_style.Name, Title.Text, _customEmojiId, Prompt.Text, AddLink.IsChecked == true);
            }
            else
            {
                function = new CreateTextCompositionStyle(Title.Text, _customEmojiId, Prompt.Text, AddLink.IsChecked == true);
            }

            var response = await _clientService.SendAsync(function);
            if (response is Error error)
            {
                IsPrimaryButtonPending = false;

                ToastPopup.ShowError(XamlRoot, error);
                args.Cancel = true;
            }

            deferral.Complete();
        }

        private void Icon_Click(object sender, RoutedEventArgs e)
        {
            var flyout = EmojiMenuFlyout.ShowAt(_clientService, EmojiDrawerMode.UserPhoto, Icon, EmojiFlyoutAlignment.TopLeft);
            flyout.EmojiSelected += Flyout_EmojiSelected;
        }

        private void Flyout_EmojiSelected(object sender, EmojiSelectedEventArgs e)
        {
            if (e.Type is not ReactionTypeCustomEmoji customEmoji)
            {
                return;
            }

            _customEmojiId = customEmoji.CustomEmojiId;
            Icon.Source = new CustomEmojiFileSource(_clientService, customEmoji.CustomEmojiId);
            IconPlaceholder.Visibility = Visibility.Collapsed;
        }
    }
}
