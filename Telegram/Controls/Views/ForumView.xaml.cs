//
// Copyright Fela Ameghino & Contributors 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Collections.Generic;
using System.Linq;
using Telegram.Common;
using Telegram.Controls.Cells;
using Telegram.Controls.Media;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.Views;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using VirtualKey = Windows.System.VirtualKey;

namespace Telegram.Controls.Views
{
    public sealed partial class ForumView : UserControl
    {
        public TopicListViewModel ViewModel => DataContext as TopicListViewModel;

        public ForumView()
        {
            InitializeComponent();
        }

        public object SelectedItem
        {
            get => ScrollingHost.SelectedItem;
            set => ScrollingHost.SelectedItem = value;
        }

        public IList<object> SelectedItems => ScrollingHost.SelectedItems;

        public void UpdateChat(Chat chat)
        {
            UpdateChatTitle(chat);
            UpdateChatPhoto(chat);
            UpdateChatEmojiStatus(chat);

            if (ViewModel.ClientService.TryGetSupergroupFull(chat, out SupergroupFullInfo fullInfo))
            {
                Subtitle.Text = Locale.Declension(Strings.R.Members, fullInfo.MemberCount);
            }
            else if (ViewModel.ClientService.TryGetSupergroup(chat, out Supergroup supergroup))
            {
                Subtitle.Text = Locale.Declension(Strings.R.Members, supergroup.MemberCount);
            }
        }

        public void UpdateChatTitle(Chat chat)
        {
            if (chat != null)
            {
                Title.Text = ViewModel.ClientService.GetTitle(chat);
            }
        }

        public void UpdateChatPhoto(Chat chat)
        {
            if (chat != null)
            {
                Photo.SetChat(ViewModel.ClientService, chat, 36);
            }
        }

        public void UpdateChatEmojiStatus(Chat chat)
        {
            if (chat != null)
            {
                Identity.SetStatus(ViewModel.ClientService, chat, BotVerified);
            }
        }

        private void Segments_Click(object sender, RoutedEventArgs e)
        {

        }

        private void Profile_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.NavigationService.Navigate(typeof(ProfilePage), ViewModel.Chat.Id);
        }

        private void Options_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            Profile.Padding = new Thickness(60, 0, e.NewSize.Width, 0);
        }

        private void Menu_ContextRequested(object sender, RoutedEventArgs e)
        {
            var flyout = new MenuFlyout();
            flyout.CreateFlyoutItem(Search, Strings.Search, Icons.Search, VirtualKey.E);
            flyout.CreateFlyoutSeparator();

            flyout.CreateFlyoutItem(ViewModel.ViewAsMessages, Strings.TopicViewAsMessages, Icons.ChatEmpty);

            if (ViewModel.Chat.CanCreateTopics(ViewModel.ClientService))
            {
                flyout.CreateFlyoutItem(ViewModel.CreateTopic, Strings.CreateTopic, Icons.Compose);
            }

            flyout.ShowAt(sender as UIElement, FlyoutPlacementMode.BottomEdgeAlignedRight);
        }

        private void Search()
        {
            // TODO: Use event instead
            this.GetParent<MainPage>()?.Search();
        }

        private void Search_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Use event instead
            this.GetParent<MainPage>()?.Search();
        }



        #region Recycle

        private readonly Dictionary<long, SelectorItem> _itemToSelector = new();

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new ChatListListViewItem(null) { MinHeight = 0 };
                args.ItemContainer.ContentTemplate = sender.ItemTemplate;
                args.ItemContainer.ContextRequested += Topic_ContextRequested;
            }

            args.IsContainerPrepared = true;
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.Item is not ForumTopic topic)
            {
                return;
            }

            if (args.InRecycleQueue)
            {
                _itemToSelector.Remove(topic.Info.MessageThreadId);
                return;
            }

            _itemToSelector[topic.Info.MessageThreadId] = args.ItemContainer;

            var cell = args.ItemContainer.ContentTemplateRoot as ForumTopicCell;

            cell.UpdateForumTopic(ViewModel.ClientService, topic);
            args.Handled = true;
        }

        public bool TryGetContainer(long messageThreadId, out SelectorItem container)
        {
            return _itemToSelector.TryGetValue(messageThreadId, out container);
        }

        public bool TryGetChatAndCell(long messageThreadId, out ForumTopic topic, out ForumTopicCell cell)
        {
            if (_itemToSelector.TryGetValue(messageThreadId, out SelectorItem container))
            {
                topic = ScrollingHost.ItemFromContainer(container) as ForumTopic;
                cell = container.ContentTemplateRoot as ForumTopicCell;
                return topic != null && cell != null;
            }

            topic = null;
            cell = null;
            return false;
        }

        public bool TryGetCell(ForumTopic topic, out ForumTopicCell cell)
        {
            if (_itemToSelector.TryGetValue(topic.Info.MessageThreadId, out SelectorItem container))
            {
                cell = container.ContentTemplateRoot as ForumTopicCell;
                return cell != null;
            }

            cell = null;
            return false;
        }

        #endregion

        private void OnDragItemsStarting(object sender, DragItemsStartingEventArgs e)
        {
            try
            {
                if (e.Items[0] is ForumTopic topic)
                {
                    if (!topic.IsPinned || e.Items.Count > 1 || ScrollingHost.SelectionMode == ListViewSelectionMode.Multiple)
                    {
                        ScrollingHost.CanReorderItems = false;
                        e.Cancel = true;
                    }
                    else
                    {
                        ScrollingHost.CanReorderItems = true;
                    }
                }
            }
            catch
            {
                ScrollingHost.CanReorderItems = false;
                e.Cancel = true;
            }
        }

        private void OnDragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
        {
            ScrollingHost.CanReorderItems = false;

            if (args.DropResult == DataPackageOperation.Move && args.Items.Count == 1 && args.Items[0] is ForumTopic topic)
            {
                var items = ViewModel.Items;
                if (items.Count == 1)
                {
                    return;
                }

                var index = items.IndexOf(topic);
                var compare = items[index > 0 ? index - 1 : index + 1];

                if (compare.IsPinned)
                {
                    var pinned = items.Where(x => x.IsPinned).Select(x => x.Info.MessageThreadId).ToArray();

                    ViewModel.ClientService.Send(new SetPinnedForumTopics(ViewModel.Chat.Id, pinned));
                }
                else
                {
                    items.Handle(topic.Info.MessageThreadId, topic.Order);
                }
            }
        }

        #region Context menu

        private void Topic_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var viewModel = ViewModel;
            var chat = viewModel?.Chat;

            if (viewModel == null || !viewModel.ClientService.TryGetSupergroup(chat, out Supergroup supergroup))
            {
                return;
            }

            var flyout = new MenuFlyout();
            var topic = ScrollingHost.ItemFromContainer(sender) as ForumTopic;

            var canManage = supergroup.CanManageTopics();
            if (canManage)
            {
                //Telegram.Td.Api.ToggleForumTopicIsPinned // CanManageTopics
                flyout.CreateFlyoutItem(viewModel.PinTopic, topic, topic.IsPinned ? Strings.UnpinFromTop : Strings.PinToTop, topic.IsPinned ? Icons.PinOff : Icons.Pin);
            }

            var muted = ViewModel.ClientService.Notifications.IsMuted(chat, topic);
            flyout.CreateFlyoutItem(viewModel.NotifyTopic, topic, muted ? Strings.Unmute : Strings.Mute, topic.IsPinned ? Icons.Alert : Icons.AlertOff);

            if (canManage)
            {
                //Telegram.Td.Api.ToggleGeneralForumTopicIsHidden // CanManageTopics
                //Telegram.Td.Api.ToggleForumTopicIsClosed // CanManageTopics
                flyout.CreateFlyoutItem(viewModel.CloseTopic, topic, topic.Info.IsClosed ? Strings.RestartTopic : Strings.CloseTopic, topic.Info.IsClosed ? Icons.PlayCircle : Icons.HandRight);
            }

            if (topic.UnreadCount > 0)
            {
                flyout.CreateFlyoutItem(viewModel.MarkTopicAsRead, topic, Strings.MarkAsRead, Icons.MarkAsRead);
            }

            if (canManage)
            {
                //     Deletes all messages in a forum topic; requires CanDeleteMessages administrator
                //     right in the supergroup unless the user is creator of the topic, the topic has
                //     no messages from other users and has at most 11 messages.
                flyout.CreateFlyoutItem(viewModel.DeleteTopic, topic, Strings.Delete, Icons.Delete, destructive: true);
            }

            if (viewModel.SelectionMode != ListViewSelectionMode.Multiple)
            {
                flyout.CreateFlyoutSeparator();
                flyout.CreateFlyoutItem(viewModel.OpenTopic, topic, Strings.OpenInNewWindow, Icons.WindowNew);
                flyout.CreateFlyoutSeparator();
                flyout.CreateFlyoutItem(viewModel.SelectTopic, topic, Strings.Select, Icons.CheckmarkCircle);
            }

            flyout.ShowAt(sender, args);
        }

        private bool CanCreateTopics(Chat chat, Supergroup supergroup, ForumTopic topic)
        {
            if (supergroup.Status is ChatMemberStatusCreator || (supergroup.Status is ChatMemberStatusAdministrator admin && (admin.Rights.CanPinMessages || supergroup.IsChannel && admin.Rights.CanEditMessages)))
            {
                return true;
            }
            else if (supergroup.Status is ChatMemberStatusRestricted restricted)
            {
                return restricted.Permissions.CanCreateTopics;
            }

            return chat.Permissions.CanCreateTopics;
        }

        #endregion

        public event ItemClickEventHandler ItemClick
        {
            add
            {
                ScrollingHost.ItemClick += value;
            }
            remove
            {
                ScrollingHost.ItemClick -= value;
            }
        }
    }
}
