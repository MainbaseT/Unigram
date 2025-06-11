//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using Telegram.Common;
using Telegram.Services;
using Telegram.ViewModels;

namespace Telegram.Td.Api
{
    public partial class MessageAlbumLastMessage : MessageContent
    {
        private readonly IClientService _clientService;
        private readonly IEventAggregator _aggregator;

        private readonly Chat _chat;
        private readonly UniqueList<long, Message> _messages = new(x => x.Id, Comparer<long>.Create((x, y) => y.CompareTo(x)));

        private Queue<long> _queue = new();
        private bool _loading = true;

        private bool _hasOldestMessage;
        private bool _hasNewestMessage;

        public long MediaAlbumId { get; }

        public IList<Message> Media => _messages;

        public FormattedText Caption { get; private set; }

        public MessageAlbumLastMessage(IClientService clientService, IEventAggregator aggregator, Chat chat, Message fromMessage)
        {
            _clientService = clientService;
            _aggregator = aggregator;

            _chat = chat;

            MediaAlbumId = fromMessage.MediaAlbumId;

            Initialize(fromMessage);
        }

        private async void Initialize(Message fromMessage)
        {
            var response = await _clientService.SendAsync(new GetChatHistory(_chat.Id, fromMessage.Id, 0, 10, false));
            if (response is Messages album && album.MessagesValue.Count > 0)
            {
                UpdateLastMessage(album, fromMessage, true);
            }

            _loading = false;
            Dequeue();
        }

        public async void LoadMore(long fromMessageId)
        {
            if (_hasOldestMessage && _hasNewestMessage)
            {
                return;
            }

            if (_loading)
            {
                if (!_queue.Contains(fromMessageId) && !_messages.ContainsKey(fromMessageId))
                {
                    _queue.Enqueue(fromMessageId);
                }

                return;
            }

            _loading = true;

            var count = 10 - Media.Count;
            var fromMessage = Media[0];

            var response = await _clientService.SendAsync(new GetChatHistory(_chat.Id, fromMessage.Id, -count, count, false));
            if (response is Messages album && album.MessagesValue.Count > 0)
            {
                UpdateLastMessage(album, fromMessage, false);
            }

            _loading = false;
            Dequeue();
        }

        private void UpdateLastMessage(Messages album, Message fromMessage, bool needFromMessage)
        {
            var hasOldestMessage = false;
            var hasNewestMessage = false;
            var found = false;

            for (int i = 0; i < album.MessagesValue.Count; i++)
            {
                var message = album.MessagesValue[i];
                if (message.MediaAlbumId == MediaAlbumId)
                {
                    if (message.Id == fromMessage.Id && !needFromMessage)
                    {
                        continue;
                    }

                    _messages.Add(message);
                    found = true;
                }
                else if (found)
                {
                    hasOldestMessage = true;
                    break;
                }
                else
                {
                    hasNewestMessage = true;
                }
            }

            if (needFromMessage)
            {
                _messages.Add(fromMessage);
            }

            Caption = _messages[^1].GetCaption();

            _hasOldestMessage = hasOldestMessage || Media.Count == 10;
            _hasNewestMessage = hasNewestMessage || Media.Count == 10;

            if (Media.Count > 0 && _chat.LastMessage?.MediaAlbumId == MediaAlbumId)
            {
                _aggregator.Publish(new UpdateChatLastMessage(_chat.Id, _chat.LastMessage, _chat.Positions));
            }
        }

        private void Dequeue()
        {
            if (_hasOldestMessage && _hasNewestMessage)
            {
                _queue.Clear();
            }

            while (_queue.TryDequeue(out long fromMessageId))
            {
                if (_messages.ContainsKey(fromMessageId))
                {
                    continue;
                }

                LoadMore(fromMessageId);
                break;
            }
        }

        public void DeleteMessages(IList<long> messageIds)
        {
            var found = false;

            foreach (var messageId in messageIds)
            {
                if (_messages.Remove(messageId))
                {
                    found = true;
                }
            }

            if (found && Media.Count > 0 && _chat.LastMessage?.MediaAlbumId == MediaAlbumId)
            {
                _aggregator.Publish(new UpdateChatLastMessage(_chat.Id, _chat.LastMessage, _chat.Positions));
            }
        }

        public NativeObject ToUnmanaged()
        {
            throw new NotImplementedException();
        }

        public override string ToString()
        {
            return nameof(MessageAlbumLastMessage);
        }
    }
}
