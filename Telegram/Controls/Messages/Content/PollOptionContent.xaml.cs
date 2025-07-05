//
// Copyright Fela Ameghino & Contributors 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Linq;
using Telegram.Common;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Shapes;

namespace Telegram.Controls.Messages.Content
{
    public sealed partial class PollOptionControl : ToggleButton
    {
        private bool _allowToggle;

        private MessageViewModel _message;
        private Poll _poll;
        private PollOption _option;

        public PollOptionControl()
        {
            DefaultStyleKey = typeof(PollOptionControl);
        }

        #region InitializeComponent

        private Ellipse Ellipse;
        private global::Microsoft.UI.Xaml.Controls.ProgressRing Loading;
        private TextBlock Percentage;
        private RichTextBlock TextText;
        private Grid Tick;
        private Ellipse Zero;
        private ProgressBar Votes;
        private Border VotesLine;
        private global::Windows.UI.Xaml.Documents.Paragraph Text;

        private bool _templateApplied;

        protected override void OnApplyTemplate()
        {
            Ellipse = GetTemplateChild(nameof(Ellipse)) as Ellipse;
            Loading = GetTemplateChild(nameof(Loading)) as Microsoft.UI.Xaml.Controls.ProgressRing;
            Percentage = GetTemplateChild(nameof(Percentage)) as TextBlock;
            TextText = GetTemplateChild(nameof(TextText)) as RichTextBlock;
            Tick = GetTemplateChild(nameof(Tick)) as Grid;
            Zero = GetTemplateChild(nameof(Zero)) as Ellipse;
            Votes = GetTemplateChild(nameof(Votes)) as ProgressBar;
            Text = GetTemplateChild(nameof(Text)) as Paragraph;
            VotesLine = GetTemplateChild(nameof(VotesLine)) as Border;

            _templateApplied = true;

            if (_message != null && _poll != null && _option != null)
            {
                UpdatePollOption(_message, _poll, _option);
            }
        }

        #endregion

        public void UpdatePollOption(MessageViewModel message, Poll poll, PollOption option)
        {
            _message = message;
            _poll = poll;
            _option = option;

            if (!_templateApplied)
            {
                return;
            }

            var results = poll.IsClosed || poll.Options.Any(x => x.IsChosen);
            var correct = poll.Type is PollTypeQuiz quiz && quiz.CorrectOptionId == poll.Options.IndexOf(option);

            var votes = Locale.Declension(poll.Type is PollTypeQuiz ? Strings.R.Answer : Strings.R.Vote, option.VoterCount);

            IsThreeState = results;
            IsChecked = results ? null : new bool?(false);
            Tag = option;

            _allowToggle = poll.Type is PollTypeRegular regular && regular.AllowMultipleAnswers && !results;

            Ellipse.Opacity = results || option.IsBeingChosen ? 0 : 1;

            Percentage.Visibility = results ? Visibility.Visible : Visibility.Collapsed;
            Percentage.Text = $"{option.VotePercentage}%";

            Extensions.SetToolTip(Percentage, results ? votes : null);

            CustomEmojiIcon.Add(TextText, Text.Inlines, message.ClientService, option.Text);

            Zero.Visibility = results ? Visibility.Visible : Visibility.Collapsed;

            Votes.Maximum = results ? Math.Max(poll.Options.Max(x => x.VoterCount), 1) : 1;
            Votes.Value = results ? option.VoterCount : 0;
            VotesLine.Opacity = results ? 0 : 0.3;

            Loading.IsActive = option.IsBeingChosen;

            Tick.Visibility = (results && correct) || option.IsChosen ? Visibility.Visible : Visibility.Collapsed;

            if (option.IsChosen && poll.Type is PollTypeQuiz)
            {
                VisualStateManager.GoToState(this, correct ? "Correct" : "Wrong", false);
            }
            else
            {
                VisualStateManager.GoToState(this, "Unknown", false);
            }

            if (results)
            {
                AutomationProperties.SetName(this, $"{option.Text.Text}, {votes}, {option.VotePercentage}%");
            }
            else
            {
                AutomationProperties.SetName(this, option.Text.Text);
            }
        }

        protected override void OnToggle()
        {
            if (_allowToggle)
            {
                base.OnToggle();
            }
        }
    }
}
