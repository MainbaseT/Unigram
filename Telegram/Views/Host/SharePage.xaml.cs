using Telegram.Navigation.Services;
using Telegram.Services;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Host
{
    public sealed partial class SharePage : UserControl
    {
        public SharePage(NavigationService navigationService)
        {
            InitializeComponent();

            var clientService = TypeResolver.Current.Resolve<IClientService>(navigationService.SessionId);

            Background.Update(clientService, null);
            RootGrid.Children.Add(navigationService.Frame);

#if DEBUG && !MOCKUP
            StateLabel.Text = Strings.AppName;
#else
            StateLabel.Text = "Unigram";
#endif
        }
    }
}
