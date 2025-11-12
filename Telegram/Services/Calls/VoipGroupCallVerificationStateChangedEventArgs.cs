//
// Copyright (c) Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;

namespace Telegram.Services.Calls
{
    public partial class VoipGroupCallVerificationStateChangedEventArgs : EventArgs
    {
        public VoipGroupCallVerificationStateChangedEventArgs(int generation, IList<string> emojis)
        {
            Generation = generation;
            Emojis = emojis;
        }

        public int Generation { get; }

        public IList<string> Emojis { get; }
    }
}
