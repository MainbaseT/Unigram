//
// Copyright Fela Ameghino & Contributors 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using Telegram.Common;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;

namespace Telegram.Controls.Messages.Content
{
    public sealed partial class ChecklistContent : ControlEx, IContent
    {
        private MessageViewModel _message;
        public MessageViewModel Message => _message;

        public ChecklistContent(MessageViewModel message)
        {
            _message = message;

            DefaultStyleKey = typeof(ChecklistContent);
        }

        #region InitializeComponent

        private RichTextBlock TitleText;
        private Paragraph Title;
        private TextBlock Type;
        private StackPanel Tasks;
        private TextBlock Completed;
        private bool _templateApplied;

        protected override void OnApplyTemplate()
        {
            TitleText = GetTemplateChild(nameof(TitleText)) as RichTextBlock;
            Title = GetTemplateChild(nameof(Title)) as Paragraph;
            Type = GetTemplateChild(nameof(Type)) as TextBlock;
            Tasks = GetTemplateChild(nameof(Tasks)) as StackPanel;
            Completed = GetTemplateChild(nameof(Completed)) as TextBlock;

            _templateApplied = true;

            if (_message != null)
            {
                UpdateMessage(_message);
            }
        }

        #endregion

        public void UpdateMessage(MessageViewModel message)
        {
            var recycled = _message?.Id == message.Id;

            _message = message;

            var checklist = message.Content as MessageChecklist;
            if (checklist == null | !_templateApplied)
            {
                return;
            }

            if (message.Chat.Type is ChatTypePrivate || !checklist.List.CanMarkTasksAsDone)
            {
                Type.Text = Strings.MessageTodoList;
            }
            else
            {
                Type.Text = Strings.MessageGroupTodoList;
            }

            CustomEmojiIcon.Add(TitleText, Title.Inlines, message.ClientService, checklist.List.Title);

            var completed = 0;

            // TODO: Diff?

            for (int i = 0; i < Math.Max(checklist.List.Tasks.Count, Tasks.Children.Count); i++)
            {
                if (i < Tasks.Children.Count)
                {
                    var button = Tasks.Children[i] as ChecklistTaskContent;
                    button.Click -= Task_Click;

                    if (i < checklist.List.Tasks.Count)
                    {
                        if (checklist.List.Tasks[i].CompletionDate != 0)
                        {
                            completed++;
                        }

                        button.UpdateChecklistTask(message, checklist.List, checklist.List.Tasks[i]);
                        button.Click += Task_Click;
                    }
                    else
                    {
                        Tasks.Children.Remove(button);
                    }
                }
                else
                {
                    if (checklist.List.Tasks[i].CompletionDate != 0)
                    {
                        completed++;
                    }

                    var button = new ChecklistTaskContent();
                    button.UpdateChecklistTask(message, checklist.List, checklist.List.Tasks[i]);
                    button.Click += Task_Click;

                    Tasks.Children.Add(button);
                }
            }

            Completed.Text = Locale.Declension(Strings.R.TodoCompleted, checklist.List.Tasks.Count, completed);
        }

        public Rect Highlight(MessageBubbleHighlightOptions options)
        {
            foreach (var child in Tasks.Children)
            {
                if (child is ChecklistTaskContent button
                    && button.Task.Id == options.ChecklistTaskId)
                {
                    var transform = child.TransformToVisual(this);
                    var point = transform.TransformPoint(new Point());

                    return new Rect(point.X, point.Y, button.ActualWidth, button.ActualHeight);
                }
            }

            return Rect.Empty;
        }

        public void Recycle()
        {
            _message = null;
        }

        public bool IsValid(MessageContent content, bool primary)
        {
            return content is MessageChecklist;
        }

        private async void Task_Click(object sender, RoutedEventArgs e)
        {
            if (_message?.SchedulingState != null)
            {
                await MessagePopup.ShowAsync(XamlRoot, Strings.MessageScheduledVote, Strings.AppName, Strings.OK);
                return;
            }

            var button = sender as ChecklistTaskContent;
            if (button.IsChecked == null)
            {
                return;
            }

            var task = button.Task as ChecklistTask;
            if (task == null)
            {
                return;
            }

            var checklist = _message?.Content as MessageChecklist;
            if (checklist == null)
            {
                return;
            }

            if (!_message.ClientService.IsPremium)
            {
                ToastPopup.ShowFeaturePromo(_message.Delegate.NavigationService, new PremiumFeatureChecklists());
                return;
            }
            else if (!checklist.List.CanMarkTasksAsDone)
            {
                if (_message.ForwardInfo != null)
                {
                    ToastPopup.Show(XamlRoot, Strings.TodoCompleteForbiddenForward, ToastPopupIcon.Error);
                }
                else
                {
                    ToastPopup.Show(XamlRoot, string.Format(Strings.TodoCompleteForbidden, _message.ClientService.GetTitle(_message.SenderId)), ToastPopupIcon.Error);
                }

                return;
            }

            var markedAsDone = new List<int>();
            var markedAsNotDone = new List<int>();

            foreach (var item in checklist.List.Tasks)
            {
                if (item.Id == task.Id)
                {
                    if (item.CompletionDate != 0)
                    {
                        markedAsNotDone.Add(item.Id);
                    }
                    else
                    {
                        markedAsDone.Add(item.Id);
                    }
                }
                else if (item.CompletionDate != 0)
                {
                    markedAsDone.Add(item.Id);
                }
                else
                {
                    markedAsNotDone.Add(item.Id);
                }
            }

            _message.ClientService.Send(new MarkChecklistTasksAsDone(_message.ChatId, _message.Id, markedAsDone, markedAsNotDone));
        }
    }
}
