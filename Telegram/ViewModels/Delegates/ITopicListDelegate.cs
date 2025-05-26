//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using Telegram.Controls.Cells;
using Telegram.Td.Api;

namespace Telegram.ViewModels.Delegates
{
    public interface ITopicListDelegate : IViewModelDelegate
    {
        void SetSelectedItem(ForumTopic topic);
        void SetSelectedItems(IList<ForumTopic> topics);

        void UpdateForumTopicLastMessage(ForumTopic topic);

        void Handle(long messageThreadId, Action<IForumTopicDelegate, ForumTopic> action);
        void Handle(ForumTopic topic, Action<IForumTopicDelegate, ForumTopic> action);
    }
}
