using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Td.Api;

namespace Telegram.Services
{
    public partial interface ICacheService
    {
        Task<Topics> GetFeedbackChatTopicsAsync(long chatId, int offset, int limit);

        bool TryGetFeedbackChatTopic(long chatId, long id, out FeedbackChatTopic topic);
        bool TryGetFeedbackChatTopic(long chatId, MessageTopic messageTopic, out FeedbackChatTopic topic);

        IEnumerable<FeedbackChatTopic> GetFeedbackChatTopics(long chatId, IEnumerable<long> ids);
        FeedbackChatTopic GetFeedbackChatTopic(long chatId, long id);
    }

    public partial class ClientService
    {
        private readonly ConcurrentDictionary<long, FeedbackChatTopicService> _feedbackChats = new();

        public Task<Topics> GetFeedbackChatTopicsAsync(long chatId, int offset, int limit)
        {
            _feedbackChats.TryGetValue(chatId, out FeedbackChatTopicService manager);

            if (manager == null)
            {
                manager = new FeedbackChatTopicService(this, _aggregator, chatId);
                _feedbackChats[chatId] = manager;
            }

            return manager.GetFeedbackChatTopicsAsync(offset, limit);
        }

        public FeedbackChatTopic GetFeedbackChatTopic(long chatId, long id)
        {
            if (_feedbackChats.TryGetValue(chatId, out FeedbackChatTopicService manager))
            {
                return manager.GetTopic(id);
            }

            return null;
        }

        public bool TryGetFeedbackChatTopic(long chatId, long id, out FeedbackChatTopic topic)
        {
            if (_feedbackChats.TryGetValue(chatId, out FeedbackChatTopicService manager))
            {
                topic = manager.GetTopic(id);
                return topic != null;
            }

            topic = null;
            return false;
        }

        public bool TryGetFeedbackChatTopic(long chatId, MessageTopic messageTopic, out FeedbackChatTopic topic)
        {
            if (messageTopic is MessageTopicFeedbackChat topicFeedbackChat)
            {
                return TryGetFeedbackChatTopic(chatId, topicFeedbackChat.FeedbackChatTopicId, out topic);
            }

            topic = null;
            return false;
        }

        public IEnumerable<FeedbackChatTopic> GetFeedbackChatTopics(long chatId, IEnumerable<long> ids)
        {
            if (_feedbackChats.TryGetValue(chatId, out FeedbackChatTopicService manager))
            {
                return manager.GetTopics(ids);
            }

            return Array.Empty<FeedbackChatTopic>();
        }

        private void UpdateFeedbackChatTopic(long chatId, Action<FeedbackChatTopicService> update)
        {
            if (_feedbackChats.TryGetValue(chatId, out FeedbackChatTopicService manager))
            {
                update(manager);
            }
            else
            {
                manager = new FeedbackChatTopicService(this, _aggregator, chatId);
                _feedbackChats[chatId] = manager;

                update(manager);
            }
        }
    }
}
