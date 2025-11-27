//
// Copyright (c) Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Services.Updates;
using Telegram.ViewModels;
using Telegram.ViewModels.Authorization;
using Telegram.ViewModels.Business;
using Telegram.ViewModels.Chats;
using Telegram.ViewModels.Create;
using Telegram.ViewModels.Delegates;
using Telegram.ViewModels.Folders;
using Telegram.ViewModels.Payments;
using Telegram.ViewModels.Premium;
using Telegram.ViewModels.Settings;
using Telegram.ViewModels.Settings.Privacy;
using Telegram.ViewModels.Stars;
using Telegram.ViewModels.Supergroups;
using Telegram.ViewModels.Users;
using Telegram.Views;
using Telegram.Views.Authorization;
using Telegram.Views.Business;
using Telegram.Views.Chats;
using Telegram.Views.Create;
using Telegram.Views.Folders;
using Telegram.Views.Folders.Popups;
using Telegram.Views.Host;
using Telegram.Views.Payments;
using Telegram.Views.Popups;
using Telegram.Views.Premium.Popups;
using Telegram.Views.Settings;
using Telegram.Views.Settings.Popups;
using Telegram.Views.Settings.Privacy;
using Telegram.Views.Stars;
using Telegram.Views.Stars.Popups;
using Telegram.Views.Stories.Popups;
using Telegram.Views.Supergroups;
using Telegram.Views.Supergroups.Popups;
using Telegram.Views.Users;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.AppService;
using Windows.ApplicationModel.ExtendedExecution;
using Windows.UI.Notifications;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram
{
    sealed partial class App : BootStrapper
    {
        private static ExtendedExecutionSession _extendedSession;

        /// <summary>
        /// Initializes a new instance of the <see cref="App"/> class.
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            TypeCrosserGenerator.Generate();

            if (SettingsService.Current.Diagnostics.LastUpdateVersion < Constants.BuildNumber)
            {
                SettingsService.Current.Diagnostics.LastUpdateVersion = Constants.BuildNumber;
                SettingsService.Current.Diagnostics.UpdateCount++;
            }

            WatchDog.Initialize();
            TypeResolver.Current.Configure();

            RequestedTheme = SettingsService.Current.Appearance.GetCalculatedApplicationTheme();
            InitializeComponent();
        }

        protected override void OnWindowActivated(Window window, bool active)
        {
            SettingsService.Current.Appearance.UpdateTimer();

            var navigation = WindowContext.GetNavigationService(window);
            if (navigation != null)
            {
                var aggregator = navigation.Session.Resolve<IEventAggregator>();
                aggregator?.Publish(new UpdateWindowActivated(active));

                var clientService = navigation.Session.Resolve<IClientService>();
                clientService?.Options.Online = active;
            }
        }

        protected override async void OnBackgroundActivated(BackgroundActivatedEventArgs args)
        {
            base.OnBackgroundActivated(args);

            if (args.TaskInstance.TriggerDetails is AppServiceTriggerDetails appService && string.Equals(appService.CallerPackageFamilyName, Package.Current.Id.FamilyName))
            {
                NotifyIcon.Connect(appService.AppServiceConnection, args.TaskInstance);
            }
            else
            {
                var deferral = args.TaskInstance.GetDeferral();

                if (args.TaskInstance.TriggerDetails is ToastNotificationActionTriggerDetail triggerDetail)
                {
                    var data = Toast.GetData(triggerDetail);
                    if (data == null)
                    {
                        deferral.Complete();
                        return;
                    }

                    var session = TypeResolver.Current.Lifetime.ActiveItem.Id;
                    if (data.TryGetValue("session", out string value) && int.TryParse(value, out int result))
                    {
                        session = result;
                    }

                    if (TypeResolver.Current.TryResolve(session, out INotificationsService service))
                    {
                        await service.ProcessAsync(data);
                    }
                }

                deferral.Complete();
            }
        }

        public override void OnInitialize(IActivatedEventArgs args)
        {
            //Locator.Configure();
            //UnigramContainer.Current.ResolveType<IGenerationService>();

            if (TypeResolver.Current.Passcode.IsEnabled)
            {
                TypeResolver.Current.Passcode.Lock(true);
                InactivityHelper.Initialize(TypeResolver.Current.Passcode.AutolockTimeout);
            }
        }

        public override async void OnStart(StartKind startKind, IActivatedEventArgs args)
        {
#if DEBUG
            DebugSettings.EnableFrameRateCounter = false;
#endif

            if (startKind == StartKind.Activate)
            {
                var lifetime = TypeResolver.Current.Lifetime;
                var sessionId = lifetime.ActiveItem.Id;

                var id = Toast.GetSession(args);
                if (id != null)
                {
                    lifetime.ActiveItem = lifetime.Items.FirstOrDefault(x => x.Id == id.Value) ?? lifetime.ActiveItem;
                }

                if (sessionId != TypeResolver.Current.Lifetime.ActiveItem.Id)
                {
                    var root = WindowContext.Current.Content as RootPage;
                    root?.Switch(lifetime.ActiveItem);
                }
            }

            var navigation = WindowContext.Current.NavigationServices.GetByFrameId($"{TypeResolver.Current.Lifetime.ActiveItem.Id}");

            var update = TypeResolver.Current.Resolve<ICloudUpdateService>();
            var service = TypeResolver.Current.Resolve<IClientService>();

            var state = await service.GetAuthorizationStateAsync();

            if (args is not ShareTargetActivatedEventArgs share)
            {
                WindowContext.Current.Activate(args, navigation, state);

                _ = Task.Run(() => OnStartSync(startKind, navigation, update));

                if (startKind != StartKind.Launch && WindowContext.Current.IsInMainView)
                {
                    var view = ApplicationView.GetForCurrentView();
                    await ApplicationViewSwitcher.TryShowAsStandaloneAsync(view.Id);
                    //view.TryResizeView(WindowContext.Current.Bounds.ToSize());
                }
            }
            else if (WindowContext.Current.Content is SharePage sharePage)
            {
                sharePage.Activate(share, navigation, state);
            }
        }

        public override UIElement CreateRootElement(IActivatedEventArgs e, WindowContext window)
        {
            var id = Toast.GetSession(e);
            if (id != null)
            {
                TypeResolver.Current.Lifetime.ActiveItem = TypeResolver.Current.Lifetime.Items.FirstOrDefault(x => x.Id == id.Value) ?? TypeResolver.Current.Lifetime.ActiveItem;
            }

            var session = TypeResolver.Current.Lifetime.ActiveItem;
            var navigationService = NavigationServiceFactory(window, BackButton.Ignore, session, $"{session.Id}", true) as NavigationService;

            if (e is ShareTargetActivatedEventArgs)
            {
                return new SharePage(window, session)
                {
                    FlowDirection = LocaleService.Current.FlowDirection
                };
            }

            return new RootPage(window, navigationService)
            {
                FlowDirection = LocaleService.Current.FlowDirection
            };
        }

        public override UIElement CreateRootElement(INavigationService navigationService)
        {
            return new StandalonePage(navigationService)
            {
                FlowDirection = LocaleService.Current.FlowDirection
            };
        }

        protected override INavigationService CreateNavigationService(WindowContext window, Frame frame, ISessionService session, string id, bool root)
        {
            if (root)
            {
                return new TLRootNavigationService(session, window, frame, session, id);
            }

            return new TLNavigationService(session.Resolve<IClientService>(), session.Resolve<IViewService>(), window, frame, id);
        }

        private async void OnStartSync(StartKind startKind, INavigationService navigation, ICloudUpdateService updateService = null)
        {
            await RequestExtendedExecutionSessionAsync();
            await Toast.RegisterBackgroundTasks();

            try
            {
                TileUpdateManager.CreateTileUpdaterForApplication("App").Clear();
            }
            catch { }

            try
            {
                ToastNotificationManager.History.Clear("App");
            }
            catch { }

            if (Constants.RELEASE && startKind == StartKind.Launch)
            {
                if (await CloudUpdateService.LaunchAsync(navigation, true))
                {
                    return;
                }
            }

            if (SettingsService.Current.IsTrayVisible)
            {
                await NotifyIcon.LaunchAsync();
            }
            else if (Constants.RELEASE && startKind == StartKind.Launch)
            {
                await NotifyIcon.AddLoopbackExemptionAsync();
            }

            Windows.ApplicationModel.Core.CoreApplication.EnablePrelaunch(true);

            if (updateService != null)
            {
                await updateService.UpdateAsync(false);
            }
        }

        private async Task RequestExtendedExecutionSessionAsync()
        {
            if (_extendedSession == null && ApiInfo.IsDesktop)
            {
                var session = new ExtendedExecutionSession();
                session.Reason = ExtendedExecutionReason.Unspecified;
                session.Revoked += ExtendedExecutionSession_Revoked;

                var result = await session.RequestExtensionAsync();
                if (result == ExtendedExecutionResult.Allowed)
                {
                    _extendedSession = session;

                    Logger.Info("ExtendedExecutionResult.Allowed");
                }
                else
                {
                    session.Revoked -= ExtendedExecutionSession_Revoked;
                    session.Dispose();

                    Logger.Warning("ExtendedExecutionResult.Denied");
                }
            }
        }

        private void ExtendedExecutionSession_Revoked(object sender, ExtendedExecutionRevokedEventArgs args)
        {
            Logger.Warning(args.Reason);
            _extendedSession?.Dispose();
            _extendedSession = null;
        }

        public override void OnResuming(object s, object e, AppExecutionState previousExecutionState)
        {
            Logger.Info("OnResuming");

            // #1225: Will this work? No one knows.
            foreach (var network in TypeResolver.Current.ResolveAll<INetworkService>())
            {
                network.Reconnect();
            }

            //foreach (var client in TLContainer.Current.ResolveAll<IClientService>())
            //{
            //    client.TryInitialize();
            //}

            // #2034: Will this work? No one knows.
            SettingsService.Current.Appearance.UpdateNightMode(null);

            OnStartSync(StartKind.Activate, WindowContext.Current.GetNavigationService());
        }

        public override Task OnSuspendingAsync(object s, SuspendingEventArgs e)
        {
            Logger.Info("OnSuspendingAsync");

            TypeResolver.Current.Passcode.CloseTime = DateTime.UtcNow;

            //return Task.WhenAll(TypeResolver.Current.ResolveAll<IVoipService>().Select(x => x.DiscardAsync()));
            //await Task.WhenAll(TLContainer.Current.ResolveAll<IClientService>().Select(x => x.CloseAsync()));
            return Task.CompletedTask;
        }

        public override ViewModelBase ViewModelForPage(UIElement page, ISessionService session)
        {
            var sessionId = session.Id;
            return page switch
            {
                DiagnosticsPage => session.Resolve<DiagnosticsViewModel>(),
                LogOutPopup => session.Resolve<LogOutViewModel>(),
                ProfilePage profile => session.Resolve<ProfileViewModel, IProfileDelegate>(profile),
                InstantPage => session.Resolve<InstantViewModel>(),
                //
                SettingsPage settings => session.Resolve<SettingsViewModel, ISettingsDelegate>(settings),
                NewContactPopup => session.Resolve<NewContactViewModel>(),
                NewChannelPopup => session.Resolve<NewChannelViewModel>(),
                NewGroupPopup => session.Resolve<NewGroupViewModel>(),
                UserEditPage userEdit => session.Resolve<UserEditViewModel, IUserDelegate>(userEdit),
                UserAffiliatePage => session.Resolve<UserAffiliateViewModel>(),
                //
                SupergroupChooseMemberPopup => session.Resolve<SupergroupChooseMemberViewModel>(),
                SupergroupAdministratorsPage supergroupAdministrators => session.Resolve<SupergroupAdministratorsViewModel, ISupergroupDelegate>(supergroupAdministrators),
                SupergroupBannedPage supergroupBanned => session.Resolve<SupergroupBannedViewModel, ISupergroupDelegate>(supergroupBanned),
                SupergroupEditAdministratorPopup supergroupEditAdministrator => session.Resolve<SupergroupEditAdministratorViewModel, IMemberPopupDelegate>(supergroupEditAdministrator),
                SupergroupEditLinkedChatPage supergroupEditLinkedChat => session.Resolve<SupergroupEditLinkedChatViewModel, ISupergroupDelegate>(supergroupEditLinkedChat),
                SupergroupEditRestrictedPopup supergroupEditRestricted => session.Resolve<SupergroupEditRestrictedViewModel, IMemberPopupDelegate>(supergroupEditRestricted),
                SupergroupEditStickerSetPopup => session.Resolve<SupergroupEditStickerSetViewModel>(),
                SupergroupEditTypePage supergroupEditType => session.Resolve<SupergroupEditTypeViewModel, ISupergroupEditDelegate>(supergroupEditType),
                SupergroupEditPage supergroupEdit => session.Resolve<SupergroupEditViewModel, ISupergroupEditDelegate>(supergroupEdit),
                SupergroupMembersPage supergroupMembers => session.Resolve<SupergroupMembersViewModel, ISupergroupDelegate>(supergroupMembers),
                SupergroupPermissionsPage supergroupPermissions => session.Resolve<SupergroupPermissionsViewModel, ISupergroupDelegate>(supergroupPermissions),
                SupergroupTopicsPage => session.Resolve<SupergroupTopicsViewModel>(),
                SupergroupDirectMessagesPage => session.Resolve<SupergroupDirectMessagesViewModel>(),
                SupergroupReactionsPopup => session.Resolve<SupergroupReactionsViewModel>(),
                SupergroupProfileColorPage => session.Resolve<SupergroupProfileColorViewModel>(),
                ChatBoostsPage => session.Resolve<ChatBoostsViewModel>(),
                ChatAffiliatePage => session.Resolve<ChatAffiliateViewModel>(),
                //
                AuthorizationRecoveryPage => session.Resolve<AuthorizationRecoveryViewModel>(),
                AuthorizationRegistrationPage => session.Resolve<AuthorizationRegistrationViewModel>(),
                AuthorizationPasswordPage => session.Resolve<AuthorizationPasswordViewModel>(),
                AuthorizationCodePage => session.Resolve<AuthorizationCodeViewModel>(),
                AuthorizationEmailAddressPage => session.Resolve<AuthorizationEmailAddressViewModel>(),
                AuthorizationEmailCodePage => session.Resolve<AuthorizationEmailCodeViewModel>(),
                AuthorizationPage signIn => session.Resolve<AuthorizationViewModel, ISignInDelegate>(signIn),
                //
                FoldersPage => session.Resolve<FoldersViewModel>(),
                FolderPage => session.Resolve<FolderViewModel>(),
                ShareFolderPopup => session.Resolve<ShareFolderViewModel>(),
                AddFolderPopup => session.Resolve<AddFolderViewModel>(),
                RemoveFolderPopup => session.Resolve<RemoveFolderViewModel>(),
                //
                SettingsBlockedChatsPage => session.Resolve<SettingsBlockedChatsViewModel>(),
                SettingsStickersPage => session.Resolve<SettingsStickersViewModel>(),
                //
                SettingsThemePage => session.Resolve<SettingsThemeViewModel>(),
                //
                SettingsAdvancedPage => session.Resolve<SettingsAdvancedViewModel>(),
                SettingsAppearancePage => session.Resolve<SettingsAppearanceViewModel>(),
                SettingsAutoDeletePage => session.Resolve<SettingsAutoDeleteViewModel>(),
                SettingsBackgroundsPage => session.Resolve<SettingsBackgroundsViewModel>(),
                SettingsDataAndStoragePage => session.Resolve<SettingsDataAndStorageViewModel>(),
                SettingsLanguagePage => session.Resolve<SettingsLanguageViewModel>(),
                SettingsNetworkPage => session.Resolve<SettingsNetworkViewModel>(),
                SettingsNightModePage => session.Resolve<SettingsNightModeViewModel>(),
                SettingsNotificationsExceptionsPage => session.Resolve<SettingsNotificationsExceptionsViewModel>(),
                SettingsPasscodePage => session.Resolve<SettingsPasscodeViewModel>(),
                SettingsPasswordPage => session.Resolve<SettingsPasswordViewModel>(),
                SettingsPrivacyAndSecurityPage => session.Resolve<SettingsPrivacyAndSecurityViewModel>(),
                SettingsProxyPage => session.Resolve<SettingsProxyViewModel>(),
                SettingsProxyPopup => session.Resolve<SettingsProxyViewModel>(),
                SettingsShortcutsPage => session.Resolve<SettingsShortcutsViewModel>(),
                SettingsThemesPage => session.Resolve<SettingsThemesViewModel>(),
                SettingsWebSessionsPage => session.Resolve<SettingsWebSessionsViewModel>(),
                SettingsNotificationsPage => session.Resolve<SettingsNotificationsViewModel>(),
                SettingsSessionsPage => session.Resolve<SettingsSessionsViewModel>(),
                SettingsStoragePage => session.Resolve<SettingsStorageViewModel>(),
                SettingsProfilePage settingsProfilePage => session.Resolve<SettingsProfileViewModel, IUserDelegate>(settingsProfilePage),
                SettingsProfileColorPage => session.Resolve<SettingsProfileColorViewModel>(),
                SettingsPowerSavingPage => session.Resolve<SettingsPowerSavingViewModel>(),
                SettingsPrivacyAllowCallsPage => session.Resolve<SettingsPrivacyAllowCallsViewModel>(),
                SettingsPrivacyAllowChatInvitesPage => session.Resolve<SettingsPrivacyAllowChatInvitesViewModel>(),
                SettingsPrivacyAllowP2PCallsPage => session.Resolve<SettingsPrivacyAllowP2PCallsViewModel>(),
                SettingsPrivacyAllowPrivateVoiceAndVideoNoteMessagesPage => session.Resolve<SettingsPrivacyAllowPrivateVoiceAndVideoNoteMessagesViewModel>(),
                SettingsPrivacyShowForwardedPage => session.Resolve<SettingsPrivacyShowForwardedViewModel>(),
                SettingsPrivacyPhonePage => session.Resolve<SettingsPrivacyPhoneViewModel>(),
                SettingsPrivacyShowPhotoPage privacyShowPhotoPage => session.Resolve<SettingsPrivacyShowPhotoViewModel, IUserDelegate>(privacyShowPhotoPage),
                SettingsPrivacyShowProfileAudioPage privacyShowProfileAudioPage => session.Resolve<SettingsPrivacyShowProfileAudioViewModel>(),
                SettingsPrivacyShowStatusPage => session.Resolve<SettingsPrivacyShowStatusViewModel>(),
                SettingsPrivacyShowBioPage => session.Resolve<SettingsPrivacyShowBioViewModel>(),
                SettingsPrivacyShowBirthdatePage => session.Resolve<SettingsPrivacyShowBirthdateViewModel>(),
                SettingsPrivacyNewChatPage => session.Resolve<SettingsPrivacyNewChatViewModel>(),
                SettingsPrivacyAutosaveGiftsPage => session.Resolve<SettingsPrivacyAutosaveGiftsViewModel>(),

                BusinessPage => session.Resolve<BusinessViewModel>(),
                BusinessLocationPage => session.Resolve<BusinessLocationViewModel>(),
                BusinessHoursPage => session.Resolve<BusinessHoursViewModel>(),
                BusinessRepliesPage businessRepliesPage => session.Resolve<BusinessRepliesViewModel, IBusinessRepliesDelegate>(businessRepliesPage),
                BusinessGreetPage => session.Resolve<BusinessGreetViewModel>(),
                BusinessAwayPage => session.Resolve<BusinessAwayViewModel>(),
                BusinessBotsPage => session.Resolve<BusinessBotsViewModel>(),
                BusinessIntroPage => session.Resolve<BusinessIntroViewModel>(),
                BusinessChatLinksPage businessChatLinksPage => session.Resolve<BusinessChatLinksViewModel, IBusinessChatLinksDelegate>(businessChatLinksPage),

                RevenuePage => session.Resolve<RevenueViewModel>(),

                PaymentFormPage => session.Resolve<PaymentFormViewModel>(),
                MessageStatisticsPage => session.Resolve<MessageStatisticsViewModel>(),
                ChatInviteLinksPage => session.Resolve<ChatInviteLinksViewModel>(),
                ChatStatisticsPage => session.Resolve<ChatStatisticsViewModel>(),
                ChatRevenuePage => session.Resolve<ChatRevenueViewModel>(),
                ChatStarsPage => session.Resolve<ChatStarsViewModel>(),
                ChatStoriesPage => session.Resolve<ChatStoriesViewModel>(),

                // Popups
                ContactsPopup => session.Resolve<ContactsViewModel>(),
                CallsPopup => session.Resolve<CallsViewModel>(),
                DownloadsPopup => session.Resolve<DownloadsViewModel>(),
                SettingsUsernamePopup => session.Resolve<SettingsUsernameViewModel>(),
                SettingsDataAutoPopup => session.Resolve<SettingsDataAutoViewModel>(),
                ChooseChatsPopup => session.Resolve<ChooseChatsViewModel>(),
                ChooseSoundPopup => session.Resolve<ChooseSoundViewModel>(),
                ChatNotificationsPopup => session.Resolve<ChatNotificationsViewModel>(),
                CreateChatPhotoPopup => session.Resolve<CreateChatPhotoViewModel>(),
                PromoPopup => session.Resolve<PromoViewModel>(),
                StarsPage => session.Resolve<StarsViewModel>(),
                BuyPopup => session.Resolve<BuyViewModel>(),
                PayPopup => session.Resolve<PayViewModel>(),
                StoryInteractionsPopup => session.Resolve<StoryInteractionsViewModel>(),
                BackgroundsPopup => session.Resolve<SettingsBackgroundsViewModel>(),
                BackgroundPopup backgroundPopup => session.Resolve<BackgroundViewModel, IBackgroundDelegate>(backgroundPopup),
                _ => null
            };
        }
    }
}
