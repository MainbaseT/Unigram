//
// Copyright (c) Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Collections.Generic;
using Telegram.Td.Api;

namespace Telegram.Services.Calls
{
    public record VoipGroupCallMessagesChangedEventArgs(GroupCallMessage Message, bool Deleted);

    public record VoipGroupCallReactionsChangedEventArgs(MessageSender SenderId, long StarCount);

    public record VoipGroupCallTopDonorsChangedEventArgs(IList<PaidReactor> Donors);

    public record VoipGroupCallTotalStarCountChangedEventArgs(long TotalStarCount);
}
