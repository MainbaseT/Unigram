using Telegram.Common;
using Telegram.Converters;
using Telegram.ViewModels.Supergroups;

namespace Telegram.Views.Supergroups
{
    public sealed partial class SupergroupFeedbackGroupPage : HostedPage
    {
        public SupergroupFeedbackGroupViewModel ViewModel => DataContext as SupergroupFeedbackGroupViewModel;

        public SupergroupFeedbackGroupPage()
        {
            InitializeComponent();
            Title = Strings.PostSuggestions;

            SliderHelper.InitializeTicks(Price, PriceTicks, 2, ConvertPriceTicks);
        }

        private string ConvertPriceValue(int value)
        {
            return Locale.Declension(Strings.R.StarsCount, value);
        }

        private string ConvertPriceTicks(int value)
        {
            return (value == 0 ? 0 : 10000).ToString("N0");
        }

        private string ConvertPriceFee(int value)
        {
            var xtr = value / 1000d;
            var usd = xtr * ViewModel.ClientService.Options.ThousandStarToUsdRate;

            var format = Formatter.FormatAmount((long)usd, "USD");

            return string.Format(Strings.PostSuggestionsPriceInfo, 85, format);
        }
    }
}
