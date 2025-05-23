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
            if (Chat.Type is ChatTypeBasicGroup)
            {
                Chat = await UpgradeAsync(Chat);
            }

            if (Chat.Type is ChatTypeSupergroup supergroup)
            {
                Set(ref _isEnabled, value, nameof(IsEnabled));
                ClientService.Send(new ToggleSupergroupIsForum(supergroup.SupergroupId, value));
            }
        }

        private bool _useTabsLayout = true;
        public bool UseTabsLayout
        {
            get => _useTabsLayout;
            set => Set(ref _useTabsLayout, value);
        }

        private bool _useListLayout;
        public bool UseListLayout
        {
            get => _useListLayout;
            set => Set(ref _useListLayout, value);
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
