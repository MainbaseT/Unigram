//
// Copyright Fela Ameghino & Contributors 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Td.Api;
using Windows.UI.Xaml.Controls;

namespace Telegram.ViewModels
{
    public partial class DialogPinnedMessagesViewModel : List<PinnedMessageViewModel>
    {
        private readonly DialogViewModel _viewModel;
        private readonly Dictionary<long, PinnedMessageViewModel> _messages = new();

        private long _lockedId;
        private long _visibleId;

        private bool _hasLoadedLastPinnedMessage = false;

        private bool _hasLoadedOldestSlice = false;
        private bool _hasLoadedNewestSlice = false;

        protected readonly DisposableMutex _loadMoreLock = new();

        public DialogPinnedMessagesViewModel(DialogViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        public int TotalCount { get; private set; }

        public async void LoadSlice(long maxId, PanelScrollingDirection direction = PanelScrollingDirection.None)
        {
            using var disposable = await _loadMoreLock.WaitAsync();

            var chat = _viewModel.Chat;
            if (chat == null || (_viewModel.Type is not DialogType.History and not DialogType.Thread))
            {
                _viewModel.Delegate?.UpdatePinnedMessage(chat, false);
                return;
            }

            if (_viewModel.Type == DialogType.Thread && _viewModel.Thread != null)
            {
                _viewModel.Delegate?.UpdatePinnedMessage(chat, false);
                return;
            }

            var hidden = _viewModel.Settings.GetChatPinnedMessage(chat.Id);
            if (hidden != 0)
            {
                _viewModel.Delegate?.UpdatePinnedMessage(chat, false);
                return;
            }

            var hasHole = false;

            if (direction == PanelScrollingDirection.Backward && Count > 0)
            {
                var item = this[0];
                if (item.Id < maxId || (item.Index == 0 && _hasLoadedOldestSlice))
                {
                    return;
                }
                else if (item.Index == 0)
                {
                    maxId = 0;
                    hasHole = true;
                }
                else
                {
                    maxId = item.Id;
                }
            }
            else if (direction == PanelScrollingDirection.Forward && Count > 0)
            {
                var item = this[^1];
                if (item.Id > maxId || (_hasLoadedNewestSlice && item.Index == TotalCount - 1))
                {
                    return;
                }
                else if (item.Index == TotalCount - 1)
                {
                    maxId = 1;
                    hasHole = true;
                }
                else
                {
                    maxId = item.Id;
                }
            }

            var filter = new SearchMessagesFilterPinned();
            var messageTopic = _viewModel.Topic;

            if (direction == PanelScrollingDirection.None && !_hasLoadedLastPinnedMessage && _viewModel.Topic == null)
            {
                _hasLoadedLastPinnedMessage = true;

                //var last = await _viewModel.ClientService.SendAsync(new GetChatPinnedMessage(chat.Id)) as Message;
                var last = await _viewModel.ClientService.SendAsync(new GetChatMessageCount(chat.Id, messageTopic, filter, true)) as Count;
                if (last is Count { CountValue: > 0 })
                {
                    _viewModel.Delegate?.UpdatePinnedMessage(chat, true);
                }
                else
                {
                    _viewModel.Delegate?.UpdatePinnedMessage(chat, false);
                }
            }

            var offset = direction == PanelScrollingDirection.Backward ? 0 : direction == PanelScrollingDirection.Forward ? -49 : -25;
            var limit = 50;

            var func = new SearchChatMessages(chat.Id, messageTopic, string.Empty, null, maxId, offset, limit, filter);

            var tsc = new TaskCompletionSource<List<PinnedMessageViewModel>>();
            async void handler(BaseObject result)
            {
                if (result is FoundChatMessages foundChatMessages && foundChatMessages.Messages.Count > 0)
                {
                    TotalCount = foundChatMessages.TotalCount;

                    var results = new List<PinnedMessageViewModel>();

                    var response = await _viewModel.ClientService.SendAsync(new GetChatMessagePosition(chat.Id, messageTopic, filter, foundChatMessages.Messages[0].Id));
                    if (response is Count count)
                    {
                        for (int i = 0; i < foundChatMessages.Messages.Count; i++)
                        {
                            results.Add(_viewModel.CreatePinnedMessage(foundChatMessages.Messages[i], foundChatMessages.TotalCount - count.CountValue - i));
                        }
                    }

                    tsc.SetResult(results);
                }
                else
                {
                    tsc.SetResult(null);
                }
            }

            _viewModel.ClientService.Send(func, handler);

            var response = await tsc.Task;
            if (response is List<PinnedMessageViewModel> messages && messages.Count > 0)
            {
                if (direction == PanelScrollingDirection.None)
                {
                    _messages.Clear();
                    Clear();
                }

                var insert = direction == PanelScrollingDirection.Backward && !hasHole;
                insert |= direction == PanelScrollingDirection.Forward && hasHole;

                if (insert)
                {
                    if (hasHole)
                    {
                        Insert(0, null);
                    }

                    for (int i = 0; i < messages.Count; i++)
                    {
                        var message = messages[i];

                        if (_messages.ContainsKey(message.Id))
                        {
                            continue;
                        }

                        _messages.Add(message.Id, message);
                        Insert(0, message);
                    }
                }
                else
                {
                    if (hasHole)
                    {
                        Add(null);
                    }

                    for (int i = messages.Count - 1; i >= 0; i--)
                    {
                        var message = messages[i];

                        if (_messages.ContainsKey(message.Id))
                        {
                            continue;
                        }

                        _messages.Add(message.Id, message);
                        Add(message);
                    }
                }

                if (Count > 0)
                {
                    _hasLoadedNewestSlice = this[0].Index == 0;
                    _hasLoadedOldestSlice = this[^1].Index == TotalCount - 1;
                }

                _viewModel.Delegate?.ViewVisibleMessages();
            }
            else if (this.Empty())
            {
                _viewModel.Delegate?.UpdatePinnedMessage(chat, false);
            }
        }

        public void SetLocked(long messageId)
        {
            _lockedId = messageId;

            if (Count > 0 && this[0].Id == messageId)
            {
                LoadSlice(messageId, PanelScrollingDirection.Backward);
            }
        }

        public PinnedMessageViewModel GetVisible(long lastVisibleId, bool hasBeenScrolled)
        {
            if (_lockedId != 0 && !hasBeenScrolled)
            {
                // We subtract 1 because we don't want to match the same
                lastVisibleId = _lockedId - 1;
            }
            else
            {
                _lockedId = 0;
            }

            for (int i = Count - 1; i >= 0; i--)
            {
                var message = this[i];
                if (message?.Id <= lastVisibleId)
                {
                    _visibleId = message.Id;
                    return message;
                }
            }

            if (hasBeenScrolled)
            {
                _visibleId = this[0].Id;
                return this[0];
            }

            _visibleId = this[^1].Id;
            return this[^1];
        }

        public void UpdateMessageContent(long messageId, MessageContent newContent)
        {
            if (_messages.TryGetValue(messageId, out var message))
            {
                message.Content = newContent;

                if (_visibleId == messageId)
                {
                    _viewModel.BeginOnUIThread(UpdateMessageContent);
                }
            }
        }

        private void UpdateMessageContent()
        {
            // This invalidates the current visible message, otherwise it won't update
            _viewModel.Delegate?.UpdatePinnedMessage(_viewModel.Chat, true);
            _viewModel.Delegate?.ViewVisibleMessages();
        }
    }
}
