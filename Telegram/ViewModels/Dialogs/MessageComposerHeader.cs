//
// Copyright Fela Ameghino & Contributors 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Services;
using Telegram.Td.Api;

namespace Telegram.ViewModels
{
    public partial class MessageComposerHeader
    {
        public IClientService ClientService { get; }

        public MessageComposerHeader(IClientService clientService)
        {
            ClientService = clientService;
        }

        public MessageViewModel ReplyToMessage { get; set; }
        public InputTextQuote ReplyToQuote { get; set; }
        public int ReplyToTaskId { get; set; }

        public MessageViewModel EditingMessage { get; set; }
        public InputMessageContent EditingMessageMedia { get; set; }

        public InputSuggestedPostInfo SuggestedPostInfo { get; set; }

        public LinkPreview LinkPreview { get; set; }
        public string LinkPreviewUrl { get; set; }

        public bool LinkPreviewDisabled
        {
            get => LinkPreviewOptions?.IsDisabled ?? false;
            set
            {
                if (LinkPreviewOptions == null && !value)
                {
                    return;
                }

                LinkPreviewOptions ??= new();
                LinkPreviewOptions.IsDisabled = value;
            }
        }

        private LinkPreviewOptions _linkPreviewOptions = new();
        public LinkPreviewOptions LinkPreviewOptions
        {
            get => _linkPreviewOptions;
            set
            {
                if (value != null)
                {
                    _linkPreviewOptions = value;
                }
            }
        }

        public bool IsEmpty
        {
            get
            {
                return ReplyToMessage == null && EditingMessage == null;
            }
        }

        public bool Matches(long messageId)
        {
            if (ReplyToMessage != null && ReplyToMessage.Id == messageId)
            {
                return true;
            }
            else if (EditingMessage != null && EditingMessage.Id == messageId)
            {
                return true;
            }

            return false;
        }
    }
}
