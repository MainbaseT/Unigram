using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Telegram.Views.Tabbed;
using Windows.Foundation;

namespace Telegram.Entities
{
    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(VideoGeneration))]
    [JsonSerializable(typeof(ImageGeneration))]
    [JsonSerializable(typeof(NavigateToHistoryEntryParameters))]
    [JsonSerializable(typeof(NavigationHistory))]
    [JsonSerializable(typeof(Rect))]
    internal partial class SourceGenerationContext : JsonSerializerContext
    {
    }
}
