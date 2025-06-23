//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Threading.Tasks;

namespace Telegram.Td
{
    partial class TdCompletionSource : TaskCompletionSource<Object>, ClientResultHandler
    {
        private readonly Action<Object> _closure;

        public TdCompletionSource(Action<Object> closure)
        {
            _closure = closure;
        }

#if TD_CX
        public void OnResult(BaseObject result)
#else
        public void OnResult(Object result)
#endif
        {
            _closure(result as Object);
            SetResult(result as Object);
        }
    }
}
