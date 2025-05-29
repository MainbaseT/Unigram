using Telegram.Converters;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Cells
{
    public sealed partial class ForumTopicShareCell : GridEx
    {
        public ForumTopicShareCell()
        {
            InitializeComponent();
        }

        public void UpdateCell(IClientService clientService, ForumTopic topic)
        {
            if (topic.Info.Icon.CustomEmojiId != 0)
            {
                Animated.Source = new CustomEmojiFileSource(clientService, topic.Info.Icon.CustomEmojiId);
                IconRoot.Visibility = Visibility.Collapsed;
                General.Visibility = Visibility.Collapsed;
            }
            else if (topic.Info.IsGeneral)
            {
                Animated.Source = null;
                IconRoot.Visibility = Visibility.Collapsed;
                General.Visibility = Visibility.Visible;
            }
            else
            {
                Animated.Source = null;
                IconRoot.Visibility = Visibility.Visible;
                General.Visibility = Visibility.Collapsed;

                var brush = ForumTopicCell.GetIconGradient(topic.Info.Icon);

                IconPath.Fill = brush;
                IconPath.Stroke = new SolidColorBrush(brush.GradientStops[1].Color);
                IconText.Text = InitialNameStringConverter.Convert(topic.Info.Name);
            }

            TitleLabel.Text = topic.Info.Name;
        }

        public void UpdateCell(IClientService clientService, FeedbackChatTopic topic)
        {
            Animated.Source = null;
            IconRoot.Visibility = Visibility.Collapsed;
            General.Visibility = Visibility.Collapsed;

            TitleLabel.Text = clientService.GetTitle(topic.SenderId);
            Photo.SetMessageSender(clientService, topic.SenderId, 36);
        }
    }
}
