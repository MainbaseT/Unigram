//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Media;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Xaml;

namespace Telegram.Views.Popups
{
    public sealed partial class JoinGroupCallPopup : ContentPopup
    {
        private readonly IClientService _clientService;

        public JoinGroupCallPopup(IClientService clientService, GroupCallParticipants participants)
        {
            InitializeComponent();

            _clientService = clientService;

            //Photo.SetChat(clientService, info, 96);
            Photo.Source = PlaceholderImage.GetGlyph(Icons.CallFilled24);

            //Identity.SetStatus(info);

            Title.Text = Strings.GroupCallLinkTitle;
            Subtitle.Text = Strings.GroupCallLinkText;

            PrimaryButtonText = Strings.GroupCallLinkJoin;
            SecondaryButtonText = Strings.Cancel;

            Participants.ItemSize = 32;
            Participants.ItemOverlap = 10;

            if (participants.ParticipantIds.Count == 1)
            {
                var participant1 = clientService.GetTitle(participants.ParticipantIds[0]);

                Participants.Items.ReplaceDiff(participants.ParticipantIds);
                TextBlockHelper.SetMarkdown(JoinRequestInfo, string.Format(Strings.GroupCallLinkText2One, participant1));
            }
            else if (participants.ParticipantIds.Count == 2)
            {
                var participant1 = clientService.GetTitle(participants.ParticipantIds[0]);
                var participant2 = clientService.GetTitle(participants.ParticipantIds[1]);

                Participants.Items.ReplaceDiff(participants.ParticipantIds);
                TextBlockHelper.SetMarkdown(JoinRequestInfo, string.Format(Strings.GroupCallLinkText2Two, participant1, participant2));
            }
            else if (participants.ParticipantIds.Count >= 3)
            {
                var participant1 = clientService.GetTitle(participants.ParticipantIds[0]);
                var participant2 = clientService.GetTitle(participants.ParticipantIds[1]);

                Participants.Items.ReplaceDiff(participants.ParticipantIds);
                TextBlockHelper.SetMarkdown(JoinRequestInfo, Locale.Declension(Strings.R.GroupCallLinkText2Many, participants.TotalCount - 2, participant1, participant2));
            }
            else
            {
                Participants.Visibility = Visibility.Collapsed;
                JoinRequestInfo.Visibility = Visibility.Collapsed;
            }
        }

        private void Participants_RecentUserHeadChanged(ProfilePicture sender, MessageSender messageSender)
        {
            sender.SetMessageSender(_clientService, messageSender, 32);
        }
    }
}
