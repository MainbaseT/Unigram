using System.Threading.Tasks;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Supergroups
{
    public class SupergroupTopicsViewModel : SupergroupViewModelBase
    {
        public SupergroupTopicsViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
        }

        private Chat _chat;
        public Chat Chat
        {
            get => _chat;
            set => Set(ref _chat, value);
        }

        private bool _isEnabled = true;
        public bool IsEnabled
        {
            get => _isEnabled;
            set => SetIsEnabled(value);
        }

        private async void SetIsEnabled(bool value)
        {
            Set(ref _isEnabled, value, nameof(IsEnabled));

            if (Chat.Type is ChatTypeBasicGroup)
            {
                Chat = await UpgradeAsync(Chat);
            }

            if (Chat.Type is ChatTypeSupergroup supergroup)
            {
                ClientService.Send(new ToggleSupergroupIsForum(supergroup.SupergroupId, value, UseTabsLayout));
            }
        }

        private bool _useTabsLayout = true;
        public bool UseTabsLayout
        {
            get => _useTabsLayout;
            set => SetUseTabsLayout(value);
        }

        private async void SetUseTabsLayout(bool value)
        {
            Set(ref _useTabsLayout, value, nameof(UseTabsLayout));

            if (Chat.Type is ChatTypeBasicGroup)
            {
                Chat = await UpgradeAsync(Chat);
            }

            if (Chat.Type is ChatTypeSupergroup supergroup)
            {
                ClientService.Send(new ToggleSupergroupIsForum(supergroup.SupergroupId, value, UseTabsLayout));
            }
        }

        private bool _useListLayout;
        public bool UseListLayout
        {
            get => _useListLayout;
            set => SetUseListLayout(value);
        }

        private async void SetUseListLayout(bool value)
        {
            Set(ref _useListLayout, value, nameof(UseListLayout));

            if (Chat.Type is ChatTypeBasicGroup)
            {
                Chat = await UpgradeAsync(Chat);
            }

            if (Chat.Type is ChatTypeSupergroup supergroup)
            {
                ClientService.Send(new ToggleSupergroupIsForum(supergroup.SupergroupId, value, UseTabsLayout));
            }
        }

        protected override Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            var chatId = (long)parameter;

            Chat = ClientService.GetChat(chatId);

            var chat = _chat;
            if (chat == null)
            {
                return Task.CompletedTask;
            }

            if (ClientService.TryGetSupergroup(chat, out Supergroup supergroup))
            {
                Set(ref _isEnabled, supergroup.IsForum, nameof(IsEnabled));
                UseTabsLayout = true;
                UseListLayout = false;
            }
            else
            {
                IsEnabled = false;
                UseTabsLayout = false;
                UseListLayout = false;
            }

            return Task.CompletedTask;
        }
    }
}
