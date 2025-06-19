namespace Telegram.Td.Api
{
    public class InputMessageReplyToTopicMessage : InputMessageReplyTo
    {
        public InputMessageReplyToTopicMessage(long messageId, MessageTopic topicId, InputTextQuote quote)
        {
            MessageId = messageId;
            TopicId = topicId;
            Quote = quote;
        }

        /// <summary>
        /// The identifier of the message to be replied in the same chat and forum topic.
        /// A message can be replied in the same chat and forum topic only if messageProperties.CanBeReplied.
        /// </summary>
        public long MessageId { get; set; }

        public MessageTopic TopicId { get; set; }

        /// <summary>
        /// Quote from the message to be replied; pass null if none. Must always be null
        /// for replies in secret chats.
        /// </summary>
        public InputTextQuote Quote { get; set; }

        public override string ToString()
        {
            return nameof(MessageBigEmoji);
        }

        public NativeObject ToUnmanaged()
        {
            return null;
        }
    }
}
