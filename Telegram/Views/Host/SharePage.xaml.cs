using System;
using System.Linq;
using Telegram.Controls;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views.Popups;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.DataTransfer;
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

                void handler(object sender, ContentDialogClosedEventArgs args)
                {
                    if (sender is ContentPopup popup)
                    {
                        popup.Closed -= handler;
                    }

                    if (args.Result != ContentDialogResult.Primary)
                    {
                        App.ShareOperation?.ReportCompleted();
                    }
                }

                var popup = new ChooseChatsPopup();
                popup.IsSmokeEnabled = false;
                popup.Closed += handler;

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
    }
}
