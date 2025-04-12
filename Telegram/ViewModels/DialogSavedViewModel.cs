//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Services;

namespace Telegram.ViewModels
{
    public partial class DialogSavedViewModel : DialogViewModel
    {
        public DialogSavedViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator, ILocationService locationService, INotificationsService pushService, IPlaybackService playbackService, IVoipService voipService, INetworkService networkService, IStorageService storageService, ITranslateService translateService)
            : base(clientService, settingsService, aggregator, locationService, pushService, playbackService, voipService, networkService, storageService, translateService)
        {
        }

        public override DialogType Type => DialogType.SavedMessagesTopic;
    }
}
