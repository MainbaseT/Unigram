//
// Copyright (c) Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Text.Json;

namespace Telegram.Td
{
    // TODO: should be possible to use Action directly without intermediate class
    partial class TdHandler : ClientResultHandler
    {
        private readonly Action<Object> _closure;
        private readonly Action<Object> _callback;

        public TdHandler(Action<Object> closure, Action<Object> callback)
        {
            _closure = closure;
            _callback = callback;
        }

        // TODO: refactoring
        public Api.File OnFile(ref Utf8JsonReader reader, bool updateFile)
        {
            throw new NotImplementedException();
        }

        public void OnResult(Object result)
        {
            try
            {
                //_closure(result);
                _callback?.Invoke(result);
            }
            catch
            {
                // We need to explicitly catch here because
                // an exception on the handler thread will cause
                // the app to no longer receive any update from TDLib.
            }
        }
    }
}
