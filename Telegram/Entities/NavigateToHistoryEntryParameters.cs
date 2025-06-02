using Newtonsoft.Json;

namespace Telegram.Entities
{
    public record NavigateToHistoryEntryParameters
    {
        public NavigateToHistoryEntryParameters(int entryId)
        {
            EntryId = entryId;
        }

        [JsonProperty("entryId")]
        public int EntryId { get; init; }
    }
}
