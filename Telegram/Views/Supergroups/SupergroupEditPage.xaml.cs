//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Linq;
using Telegram.Controls.Media;
using Telegram.Td.Api;
using Telegram.ViewModels.Delegates;
using Telegram.ViewModels.Supergroups;
using Windows.UI.Xaml;

namespace Telegram.Views.Supergroups
{
    public sealed partial class SupergroupEditPage : HostedPage, ISupergroupEditDelegate
    {
        public SupergroupEditViewModel ViewModel => DataContext as SupergroupEditViewModel;

        public SupergroupEditPage()
        {
            InitializeComponent();
            Title = Strings.ChannelEdit;
        }

        #region Binding

        private string ConvertHistory(int available)
        {
            if (ViewModel.Chat?.Type is ChatTypeSupergroup { IsChannel: true })
            {
                return string.Empty;
            }

            return ViewModel.AllHistoryAvailableOptions[available].Value
                ? Strings.ChatHistoryVisibleInfo
                : Strings.ChatHistoryHiddenInfo;
        }

        #endregion

        #region Delegate

        public void UpdateChat(Chat chat)
        {
            //UpdateChatTitle(chat);
            UpdateChatPhoto(chat);

            if (chat.AvailableReactions is ChatAvailableReactionsAll)
            {
                Reactions.Badge = Strings.AllReactions;
            }
            else if (chat.AvailableReactions is ChatAvailableReactionsSome some)
            {
                if (some.Reactions.Count > 0)
                {
                    Reactions.Badge = some.Reactions.Count.ToString("N0");
                }
                else
                {
                    Reactions.Badge = Strings.ReactionsOff;
                }
            }
        }

        public void UpdateChatTitle(Chat chat)
        {
            if (chat.Id == ViewModel.FeedbackChatId)
            {
                ChannelFeedbackGroupCell.UpdateChatTitle(chat);
            }
            else
            {
                TitleLabel.Text = ViewModel.ClientService.GetTitle(chat);
            }
        }

        public void UpdateChatPhoto(Chat chat)
        {
            if (chat.Id == ViewModel.FeedbackChatId)
            {
                ChannelFeedbackGroupCell.UpdateChatPhoto(chat);
            }
            else
            {
                Photo.SetChat(ViewModel.ClientService, chat, 96);
            }
        }

        public void UpdateSupergroup(Chat chat, Supergroup group)
        {
            TitleLabel.PlaceholderText = group.IsChannel ? Strings.EnterChannelName : Strings.GroupName;

            Delete.Content = group.IsChannel ? Strings.ChannelDelete : Strings.DeleteMega;
            DeletePanel.Footer = group.IsChannel ? Strings.ChannelDeleteInfo : Strings.MegaDeleteInfo;

            Members.Content = group.IsChannel ? Strings.ChannelSubscribers : Strings.ChannelMembers;
            //Members.Visibility = group.IsChannel ? Visibility.Visible : Visibility.Collapsed;

            EventLog.Visibility = Visibility.Visible;

            ViewModel.Title = chat.Title;
            ViewModel.HasAutomaticTranslation = group.HasAutomaticTranslation;

            var canChangeInfo = group.CanChangeInfo(chat);
            var canInviteUsers = group.CanInviteUsers();
            var canRestrictMembers = group.CanRestrictMembers();
            var canPostMessages = group.CanPostMessages();
            var hasActiveUsername = group.HasActiveUsername();

            TitleLabel.IsReadOnly = !canChangeInfo;
            About.IsReadOnly = !canChangeInfo;
            SetNewPhoto.Visibility = canChangeInfo
                ? Visibility.Visible
                : Visibility.Collapsed;

            ChatType.Content = group.IsChannel ? Strings.ChannelType : Strings.GroupType;
            ChatType.Glyph = group.IsChannel ? Icons.Megaphone : Icons.People;
            ChatType.Badge = hasActiveUsername
                ? group.IsChannel
                    ? Strings.TypePublic
                    : Strings.TypePublicGroup
                : group.IsChannel
                    ? chat.HasProtectedContent
                        ? Strings.TypePrivateRestrictedForwards
                        : Strings.TypePrivate
                    : chat.HasProtectedContent
                        ? Strings.TypePrivateGroupRestrictedForwards
                        : Strings.TypePrivateGroup;

            ChatHistory.Visibility = canChangeInfo && !hasActiveUsername && !group.IsChannel && !group.HasLinkedChat
                ? Visibility.Visible
                : Visibility.Collapsed;

            InviteLinks.Visibility = canInviteUsers && !hasActiveUsername
                ? Visibility.Visible
                : Visibility.Collapsed;

            GroupTopics.Visibility = group.Status is ChatMemberStatusCreator && !group.IsChannel && !group.HasLinkedChat
                ? Visibility.Visible
                : Visibility.Collapsed;

            GroupTopics.Badge = group.IsForum
                ? Strings.TopicsEnabled
                : Strings.TopicsDisabled;

            ChannelFeedbackGroup.Visibility = group.Status is ChatMemberStatusCreator && group.IsChannel
                ? Visibility.Visible
                : Visibility.Collapsed;

            if (canChangeInfo)
            {
                if (ViewModel.IsPremiumAvailable)
                {
                    ChannelColor.Visibility = Visibility.Visible;
                    ProfileColor.SetChat(ViewModel.ClientService, chat);
                }
                else
                {
                    ChannelColor.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                ChannelColor.Visibility = Visibility.Collapsed;
            }

            ChatLinked.Visibility = group.IsChannel ? Visibility.Visible : group.HasLinkedChat ? Visibility.Visible : Visibility.Collapsed;
            ChatLinked.Content = group.IsChannel ? Strings.Discussion : Strings.LinkedChannel;
            ChatLinked.Glyph = group.IsChannel ? Icons.ChatEmpty : Icons.Megaphone;
            ChatLinked.Badge = group.HasLinkedChat ? string.Empty : Strings.DiscussionInfoShort;

            Permissions.Badge = string.Format("{0}/{1}", chat.Permissions.Count(), chat.Permissions.Total());
            Permissions.Visibility = group.IsChannel || !canRestrictMembers ? Visibility.Collapsed : Visibility.Visible;

            DeletePanel.Visibility = group.Status is ChatMemberStatusCreator ? Visibility.Visible : Visibility.Collapsed;

            ChatBasicPanel.Visibility = ChatType.Visibility == Visibility.Visible
                || ChatHistory.Visibility == Visibility.Visible
                || ChatLinked.Visibility == Visibility.Visible
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            AffiliatePrograms.Visibility = group.IsChannel && group.CanPostMessages() ? Visibility.Visible : Visibility.Collapsed;
            ChannelAutoTranslate.Visibility = group.IsChannel && canChangeInfo ? Visibility.Visible : Visibility.Collapsed;
        }

        public void UpdateSupergroupFullInfo(Chat chat, Supergroup group, SupergroupFullInfo fullInfo)
        {
            if (fullInfo != null)
            {
                ViewModel.About = fullInfo.Description;
                ViewModel.IsAllHistoryAvailable = fullInfo.IsAllHistoryAvailable ? 0 : 1;

                var linkedChat = ViewModel.ClientService.GetChat(fullInfo.LinkedChatId);
                if (linkedChat != null && ViewModel.ClientService.TryGetSupergroup(linkedChat, out Supergroup linkedSupergroup))
                {
                    if (linkedSupergroup.HasActiveUsername(out string username))
                    {
                        ChatLinked.Badge = $"@{username}";
                    }
                    else
                    {
                        ChatLinked.Badge = linkedChat.Title;
                    }
                }
                else
                {
                    ChatLinked.Badge = Strings.DiscussionInfoShort;
                }

                Admins.Badge = fullInfo.AdministratorCount;
                Members.Badge = fullInfo.MemberCount;
                Blacklist.Badge = fullInfo.BannedCount;

                ChatBasicPanel.Visibility = ChatType.Visibility == Visibility.Visible
                    || ChatHistory.Visibility == Visibility.Visible
                    || ChatLinked.Visibility == Visibility.Visible
                        ? Visibility.Visible
                        : Visibility.Collapsed;

                Statistics.Visibility = fullInfo.CanGetStatistics
                    ? Visibility.Visible
                    : Visibility.Collapsed;

                if (ViewModel.ClientService.TryGetChat(fullInfo.FeedbackChatId, out Chat feedbackChat))
                {
                    var price = ViewModel.ClientService.PaidMessageStarCount(feedbackChat);
                    if (price > 0)
                    {
                        ChannelFeedbackGroupStars.Visibility = Visibility.Visible;
                        ChannelFeedbackGroupStarCount.Text = price.ToString("N0");
                    }
                    else
                    {
                        ChannelFeedbackGroupStars.Visibility = Visibility.Collapsed;
                        ChannelFeedbackGroupStarCount.Text = Strings.PostSuggestionsFree;
                    }

                    ChannelFeedbackGroupRoot.Visibility = Visibility.Visible;
                    ChannelFeedbackGroupCell.UpdateChat(ViewModel.ClientService, feedbackChat, new ChatListFolder(int.MaxValue));
                }
                else
                {
                    ChannelFeedbackGroupStars.Visibility = Visibility.Collapsed;
                    ChannelFeedbackGroupStarCount.Text = Strings.PostSuggestionsOff;
                }
            }
            else
            {
                ChannelFeedbackGroupRoot.Visibility = Visibility.Collapsed;
                ChannelFeedbackGroupStars.Visibility = Visibility.Collapsed;
                ChannelFeedbackGroupStarCount.Text = string.Empty;
            }

            UpdateSupergroup(chat, group);
        }



        public void UpdateBasicGroup(Chat chat, BasicGroup group)
        {
            TitleLabel.PlaceholderText = Strings.GroupName;

            Delete.Content = Strings.DeleteMega;
            DeletePanel.Footer = Strings.MegaDeleteInfo;

            Members.Content = Strings.ChannelMembers;

            EventLog.Visibility = Visibility.Collapsed;

            ViewModel.Title = chat.Title;
            ViewModel.IsAllHistoryAvailable = 1;

            var canChangeInfo = group.CanChangeInfo(chat);
            var canInviteUsers = group.CanInviteUsers();

            TitleLabel.IsReadOnly = !canChangeInfo;
            About.IsReadOnly = !canChangeInfo;
            SetNewPhoto.Visibility = canChangeInfo
                ? Visibility.Visible
                : Visibility.Collapsed;

            ChatType.Glyph = Icons.People;
            ChatType.Content = Strings.GroupType;
            ChatType.Badge = Strings.TypePrivateGroup;
            ChatType.Visibility = group.Status is ChatMemberStatusCreator ? Visibility.Visible : Visibility.Collapsed;

            ChatHistory.Visibility = group.Status is ChatMemberStatusCreator ? Visibility.Visible : Visibility.Collapsed;

            InviteLinks.Visibility = canInviteUsers
                ? Visibility.Visible
                : Visibility.Collapsed;
            ChatLinked.Visibility = Visibility.Collapsed;
            ChannelColor.Visibility = Visibility.Collapsed;

            GroupTopics.Badge = Strings.TopicsDisabled;

            Permissions.Badge = string.Format("{0}/{1}", chat.Permissions.Count(), chat.Permissions.Total());
            Permissions.Visibility = group.Status is ChatMemberStatusCreator ? Visibility.Visible : Visibility.Collapsed;
            Blacklist.Visibility = Visibility.Collapsed;

            DeletePanel.Visibility = group.Status is ChatMemberStatusCreator ? Visibility.Visible : Visibility.Collapsed;

            ChatBasicPanel.Visibility = ChatType.Visibility == Visibility.Visible
                || ChatHistory.Visibility == Visibility.Visible
                || ChatLinked.Visibility == Visibility.Visible
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            AffiliatePrograms.Visibility = Visibility.Collapsed;
        }

        public void UpdateBasicGroupFullInfo(Chat chat, BasicGroup group, BasicGroupFullInfo fullInfo)
        {
            if (fullInfo != null)
            {
                Admins.Badge = fullInfo.Members.Count(x => x.Status is ChatMemberStatusCreator or ChatMemberStatusAdministrator);
                Members.Badge = fullInfo.Members.Count;
                Blacklist.Badge = 0;

                ChatBasicPanel.Visibility = ChatType.Visibility == Visibility.Visible
                    || ChatHistory.Visibility == Visibility.Visible
                    || ChatLinked.Visibility == Visibility.Visible
                        ? Visibility.Visible
                        : Visibility.Collapsed;
            }

            UpdateBasicGroup(chat, group);
        }

        #endregion
    }
}
