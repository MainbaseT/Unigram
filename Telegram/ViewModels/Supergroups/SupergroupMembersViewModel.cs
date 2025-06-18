//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels.Delegates;
using Telegram.Views.Popups;
using Telegram.Views.Supergroups.Popups;
using Windows.UI.Xaml.Controls;

namespace Telegram.ViewModels.Supergroups
{
    public partial class SupergroupMembersViewModel : SupergroupMembersViewModelBase, IDelegable<ISupergroupDelegate>, IHandle
    {
        public SupergroupMembersViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator, new SupergroupMembersFilterRecent(), query => new SupergroupMembersFilterSearch(query))
        {
        }

        public override void Subscribe()
        {
            Aggregator.Subscribe<UpdateChatMember>(this, Handle);
        }

        private void Handle(UpdateChatMember update)
        {
            if (update.ChatId == _chat.Id)
            {
                var item = Members.FirstOrDefault(x => x.MemberId.AreTheSame(update.NewChatMember.MemberId));
                if (item != null)
                {
                    if (update.NewChatMember.Status is ChatMemberStatusMember or ChatMemberStatusAdministrator or ChatMemberStatusCreator)
                    {
                        item.Status = update.NewChatMember.Status;
                    }
                    else
                    {
                        Members.Remove(item);
                    }
                }
                else if (update.NewChatMember.Status is ChatMemberStatusMember or ChatMemberStatusAdministrator or ChatMemberStatusCreator)
                {
                    Members.Insert(0, update.NewChatMember);
                }
            }
        }

        public bool IsEmbedded { get; set; }

        private bool _hasHiddenMembers;
        public bool HasHiddenMembers
        {
            get => _hasHiddenMembers;
            set => SetHiddenMembers(value);
        }

        public void UpdateHiddenMembers(bool value)
        {
            Set(ref _hasHiddenMembers, value, nameof(HasHiddenMembers));
        }

        private void SetHiddenMembers(bool value)
        {
            if (Chat.Type is ChatTypeSupergroup supergroupType && ClientService.TryGetSupergroupFull(Chat, out SupergroupFullInfo supergroup))
            {
                if (supergroup.CanHideMembers)
                {
                    Set(ref _hasHiddenMembers, value, nameof(HasHiddenMembers));
                    ClientService.Send(new ToggleSupergroupHasHiddenMembers(supergroupType.SupergroupId, value));
                }
                else
                {
                    Set(ref _hasHiddenMembers, false, nameof(HasHiddenMembers));
                }
            }
        }

        public async void Add()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (chat.Type is ChatTypeSupergroup or ChatTypeBasicGroup)
            {
                var header = chat.Type is ChatTypeSupergroup { IsChannel: true }
                    ? Strings.AddSubscriber
                    : Strings.AddMember;

                var selectionMode = chat.Type is ChatTypeBasicGroup
                    ? ListViewSelectionMode.Single
                    : ListViewSelectionMode.Multiple;

                var selected = await ChooseChatsPopup.PickUsersAsync(ClientService, NavigationService, header, selectionMode);
                if (selected == null || selected.Count == 0)
                {
                    return;
                }

                if (selected[0].Type is UserTypeBot && chat.Type is ChatTypeSupergroup { IsChannel: true })
                {
                    var admin = await ShowPopupAsync(Strings.AddBotAsAdmin, Strings.AddBotAdminAlert, Strings.AddAsAdmin, Strings.Cancel);
                    if (admin == ContentDialogResult.Primary)
                    {
                        _ = NavigationService.ShowPopupAsync(new SupergroupEditAdministratorPopup(), new SupergroupEditMemberArgs(chat.Id, new MessageSenderUser(selected[0].Id)));
                    }

                    return;
                }

                string title = Locale.Declension(Strings.R.AddManyMembersAlertTitle, selected.Count);
                string message;

                if (selected.Count <= 5)
                {
                    var names = string.Join(", ", selected.Select(x => x.FullName()));
                    message = string.Format(Strings.AddMembersAlertNamesText, names, chat.Title);
                }
                else
                {
                    message = Locale.Declension(Strings.R.AddManyMembersAlertNamesText, selected.Count, chat.Title);
                }

                var confirm = await ShowPopupAsync(message, title, Strings.Add, Strings.Cancel);
                if (confirm != ContentDialogResult.Primary)
                {
                    return;
                }

                var response = await AddChatMembers(chat, selected.Select(x => x.Id));
                if (response is FailedToAddMembers failed)
                {
                    if (failed.FailedToAddMembersValue.Count > 0)
                    {
                        ShowPopup(new ChatInviteFallbackPopup(ClientService, chat.Id, failed.FailedToAddMembersValue));
                    }

                    var failedUserIds = failed.FailedToAddMembersValue
                        .Select(x => x.UserId)
                        .ToHashSet();

                    foreach (var user in selected)
                    {
                        if (failedUserIds.Contains(user.Id))
                        {
                            continue;
                        }

                        Aggregator.Publish(new UpdateChatMember(chat.Id, 0, 0, null, false, false, null, new ChatMember(new MessageSenderUser(user.Id), ClientService.Options.MyId, DateTime.Now.ToTimestamp(), new ChatMemberStatusMember())));
                    }
                }
                else if (response is Error error)
                {
                    ShowPopup(error.Message, Strings.AppName);
                }
            }
        }

        private async Task<Object> AddChatMembers(Chat chat, IEnumerable<long> users)
        {
            if (chat.Type is ChatTypeSupergroup)
            {
                return await ClientService.SendAsync(new AddChatMembers(chat.Id, users.ToArray()));
            }

            IList<FailedToAddMember> members = null;

            foreach (var userId in users)
            {
                var response = await ClientService.SendAsync(new AddChatMember(chat.Id, userId, 100));
                if (response is FailedToAddMembers failed)
                {
                    members ??= new List<FailedToAddMember>();
                    members.AddRange(failed.FailedToAddMembersValue);
                }
                else if (response is Error)
                {
                    // TODO: this is not ideal as the app will not try to add subsequent users
                    return response;
                }
            }

            return new FailedToAddMembers(members ?? Array.Empty<FailedToAddMember>());
        }

        #region Context menu

        public void PromoteMember(ChatMember member)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            NavigationService.ShowPopupAsync(new SupergroupEditAdministratorPopup(), new SupergroupEditMemberArgs(chat.Id, member.MemberId));
        }

        public void RestrictMember(ChatMember member)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            NavigationService.ShowPopupAsync(new SupergroupEditRestrictedPopup(), new SupergroupEditMemberArgs(chat.Id, member.MemberId));
        }

        public async void RemoveMember(ChatMember member)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var index = Members.IndexOf(member);

            Members.Remove(member);

            var response = await ClientService.SendAsync(new SetChatMemberStatus(chat.Id, member.MemberId, new ChatMemberStatusBanned()));
            if (response is Error)
            {
                Members.Insert(index, member);
            }
        }

        #endregion
    }
}
