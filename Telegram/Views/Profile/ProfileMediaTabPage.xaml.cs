//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Common;
using Telegram.Controls;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.ViewModels.Chats;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

namespace Telegram.Views.Profile
{
    public sealed partial class ProfileMediaTabPage : ProfileTabPage
    {
        public ProfileMediaTabPage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (!IsProfile)
            {
                ScrollingHost.Padding = new Thickness(12, 0, 4, 8);
            }

            if (ViewModel.Media.Empty())
            {
                ScrollingHost.ItemContainerTransitions.Add(new EntranceThemeTransition { IsStaggeringEnabled = false });
            }
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }
            else if (args.ItemContainer.ContentTemplateRoot is Grid content && args.Item is MessageWithOwner message)
            {
                AutomationProperties.SetName(args.ItemContainer, Automation.GetSummaryWithName(message, true));

                var photo = content.Children[0] as ImageView;

                // TODO: justified because of Photo_Click
                photo.Tag = message;

                var particles = content.Children[1] as AnimatedImage;
                var overlay = content.Children[2] as Border;
                var duration = overlay.Child as TextBlock;

                if (message.Content is MessagePhoto photoMessage)
                {
                    var small = photoMessage.Photo.GetSmall();

                    photo.SetSource(ViewModel.ClientService, small.Photo, blurRadius: photoMessage.HasSpoiler ? 15 : 0);
                    overlay.Visibility = Visibility.Collapsed;

                    particles.Source = photoMessage.HasSpoiler
                        ? new ParticlesImageSource()
                        : null;
                }
                else if (message.Content is MessageVideo videoMessage)
                {
                    var thumbnail = videoMessage.Cover?.GetThumbnail();
                    thumbnail ??= videoMessage.Video.Thumbnail;

                    photo.SetSource(ViewModel.ClientService, thumbnail?.File, blurRadius: videoMessage.HasSpoiler ? 15 : 0);
                    overlay.Visibility = Visibility.Visible;

                    duration.Text = videoMessage.Video.GetDuration();

                    particles.Source = videoMessage.HasSpoiler
                        ? new ParticlesImageSource()
                        : null;
                }

                args.Handled = true;
            }
        }

        private async void Photo_Click(object sender, RoutedEventArgs e)
        {
            var element = sender as FrameworkElement;
            var message = element.Tag as MessageWithOwner;

            var response = await ViewModel.ClientService.SendAsync(new GetMessageProperties(message.ChatId, message.Id));
            if (response is not MessageProperties properties)
            {
                return;
            }

            var viewModel = new ChatGalleryViewModel(ViewModel.ClientService, ViewModel.StorageService, ViewModel.Aggregator, message.ChatId, ViewModel.Topic, message, properties, true);
            ViewModel.NavigationService.ShowGallery(viewModel, element);
        }
    }
}
