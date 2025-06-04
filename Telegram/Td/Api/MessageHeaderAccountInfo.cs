using System;

namespace Telegram.Td.Api
{
    public class MessageHeaderAccountInfo : MessageContent
    {
        public NativeObject ToUnmanaged()
        {
            throw new NotImplementedException();
        }

        public override string ToString()
        {
            return nameof(MessageHeaderAccountInfo);
        }
    }
}
