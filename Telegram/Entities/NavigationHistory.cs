using Newtonsoft.Json;
using System.Collections.Generic;

namespace Telegram.Entities
{
    public record NavigationHistory
    {
        [JsonProperty("currentIndex")]
        public int CurrentIndex { get; init; }

        [JsonProperty("entries")]
        public IReadOnlyList<HistoryEntry> Entries { get; init; }
    }

    public record HistoryEntry
    {
        [JsonProperty("id")]
        public int Id { get; init; }

        [JsonProperty("title")]
        public string Title { get; init; }

        [JsonProperty("url")]
        public string Url { get; init; }

        [JsonIgnore]
        public string DocumentTitle
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(Title))
                {
                    return Title;
                }

                return Url;
            }
        }

        [JsonIgnore]
        public string FaviconUri { get; init; }

        [JsonIgnore]
        public int Index { get; set; }
    }
}
