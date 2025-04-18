using System;
using System.Linq;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views.Popups;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Telegram.Views.Host
{
    public sealed partial class SharePage : UserControl
    {
        private readonly WindowContext _window;

        public SharePage(WindowContext window, int sessionId)
        {
            InitializeComponent();

            _window = window;

            Background.Update(TypeResolver.Current.Resolve<IClientService>(sessionId));

#if DEBUG && !MOCKUP
            StateLabel.Text = Strings.AppName;
#else
            StateLabel.Text = "Unigram";
#endif
        }

        public async void Activate(ShareTargetActivatedEventArgs args, INavigationService navigationService, AuthorizationState state)
        {
            WatchDog.TrackEvent("ShareTarget");
            App.ShareOperation = args.ShareOperation;

            if (state is AuthorizationStateReady)
            {
                var package = new DataPackage();

                try
                {
                    var operation = args.ShareOperation.Data;
                    if (operation.AvailableFormats.Contains(StandardDataFormats.ApplicationLink))
                    {
                        package.SetApplicationLink(await operation.GetApplicationLinkAsync());
                    }
                    if (operation.AvailableFormats.Contains(StandardDataFormats.Bitmap))
                    {
                        package.SetBitmap(await operation.GetBitmapAsync());
                    }
                    //if (operation.Contains(StandardDataFormats.Html))
                    //{
                    //    package.SetHtmlFormat(await operation.GetHtmlFormatAsync());
                    //}
                    //if (operation.Contains(StandardDataFormats.Rtf))
                    //{
                    //    package.SetRtf(await operation.GetRtfAsync());
                    //}
                    if (operation.AvailableFormats.Contains(StandardDataFormats.StorageItems))
                    {
                        package.SetStorageItems(await operation.GetStorageItemsAsync());
                    }
                    if (operation.AvailableFormats.Contains(StandardDataFormats.Text))
                    {
                        package.SetText(await operation.GetTextAsync());
                    }
                    //if (operation.Contains(StandardDataFormats.Uri))
                    //{
                    //    package.SetUri(await operation.GetUriAsync());
                    //}
                    if (operation.AvailableFormats.Contains(StandardDataFormats.WebLink))
                    {
                        package.SetWebLink(await operation.GetWebLinkAsync());
                    }
                }
                catch { }

                var popup = new ChooseChatsPopup();
                popup.IsSmokeEnabled = false;
                popup.Closed += OnClosed;
                popup.AccountClick += OnAccountClick;

                navigationService.ShowPopup(popup, new ChooseChatsConfigurationDataPackage(package.GetView()));
            }
            else
            {
                try
                {
                    var options = new Windows.System.LauncherOptions();
                    options.TargetApplicationPackageFamilyName = Package.Current.Id.FamilyName;

                    await Windows.System.Launcher.LaunchUriAsync(new Uri("tg://"), options);
                }
                catch
                {
                    // It's too early?
                }
            }
        }

        private void ShowPopup(ISessionService session, DataPackageView package)
        {
            var popup = new ChooseChatsPopup();
            popup.IsSmokeEnabled = false;
            popup.Closed += OnClosed;
            popup.AccountClick += OnAccountClick;

            var clientService = session.ClientService;
            var service = new TLNavigationService(clientService, null, _window, null, "Share");

            service.ShowPopup(popup, new ChooseChatsConfigurationDataPackage(package));
        }

        private void OnAccountClick(object sender, EventArgs e)
        {
            foreach (var popup in VisualTreeHelper.GetOpenPopupsForXamlRoot(XamlRoot))
            {
                if (popup.Child is ChooseChatsPopup chooseChats && chooseChats.ViewModel.Configuration is ChooseChatsConfigurationDataPackage package)
                {
                    chooseChats.Closed -= OnClosed;
                    chooseChats.Hide();

                    ShowPopup(sender as ISessionService, package.Package);
                }
            }
        }

        private void OnClosed(ContentDialog sender, ContentDialogClosedEventArgs args)
        {
            sender.Closed -= OnClosed;

            if (args.Result != ContentDialogResult.Primary)
            {
                App.ShareOperation?.TryReportCompleted();
                App.ShareOperation = null;
            }
        }
    }
}
