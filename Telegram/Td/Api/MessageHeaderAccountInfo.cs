using System;

namespace Telegram.Td.Api
{
    public class MessageHeaderAccountInfo : MessageContent
    {
        public override string ToString()
        {
            return nameof(MessageHeaderAccountInfo);
        }

        public NativeObject ToUnmanaged()
        {
            return null;
        }
    }
}
