using Telegram.Streams;
using Telegram.ViewModels.Supergroups;
using Windows.UI.Xaml;

namespace Telegram.Views.Supergroups
{
    public sealed partial class SupergroupTopicsPage : HostedPage
    {
        public SupergroupTopicsViewModel ViewModel => DataContext as SupergroupTopicsViewModel;

        public SupergroupTopicsPage()
        {
            InitializeComponent();
            Title = Strings.TopicsTitle;

            TabsAnimated.Source = new LocalFileSource("ms-appx:///Assets/Animations/SupergroupTopicsTabs.tgs")
            {
                NeedsRepainting = true
            };

            ListAnimated.Source = new LocalFileSource("ms-appx:///Assets/Animations/SupergroupTopicsList.tgs")
            {
                NeedsRepainting = true
            };
        }

        private void Tabs_Checked(object sender, RoutedEventArgs e)
        {
            TabsAnimated.Play();
        }

        private void List_Checked(object sender, RoutedEventArgs e)
        {
            ListAnimated.Play();
        }
    }
}
