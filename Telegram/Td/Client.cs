//
// Copyright (c) Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Runtime.InteropServices;
using System.Security;
using System.Threading;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Td.Api;

namespace Telegram.Td
{
    public delegate void LogMessageCallback(int verbosityLevel, string message);

    public interface ClientResultHandler
    {
        void OnResult(Object result);
    }

    public class Client
    {
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void TdLogMessageCallback(int verbosity_level, IntPtr message);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("tdjson.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern void td_set_log_message_callback(int max_verbosity_level, TdLogMessageCallback callback);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("tdjson.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int td_create_client_id();

        [SuppressUnmanagedCodeSecurity]
        [DllImport("tdjson.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern unsafe void td_send(int client_id, byte* request);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("tdjson.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern unsafe byte* td_execute(byte* request);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("tdjson.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern unsafe byte* td_receive(double timeout);

        private static long _currentRequestId = 0;
        private static readonly ReaderWriterDictionary<long, ClientResultHandler> _handlers = new();
        private static readonly ReaderWriterDictionary<long, ClientResultHandler> _updateHandlers = new();

        private readonly int _clientId;

        private Client(ClientResultHandler updateHandler)
        {
            _clientId = td_create_client_id();

            if (updateHandler != null)
            {
                _updateHandlers[_clientId] = updateHandler;
            }

            Send(new GetOption("version"));
        }

        public static Client Create(ClientResultHandler updateHandler) => new Client(updateHandler);

        public unsafe void Send(Function function, ClientResultHandler handler = null)
        {
            var requestId = Interlocked.Increment(ref _currentRequestId);
            if (handler != null)
            {
                _handlers[requestId] = handler;
            }

            using var buffer = new ArrayPoolBufferWriter();

            var request = ClientJson.ToJson(buffer, function, requestId);
            fixed (byte* bytes = request)
            {
                td_send(_clientId, bytes);
            }
        }

        /// <summary>
        /// Synchronously executes a TDLib request. Only a few marked accordingly requests can be executed synchronously.
        /// </summary>
        /// <param name="function">Object representing a query to the TDLib.</param>
        /// <returns>Returns request result.</returns>
        /// <exception cref="NullReferenceException">Thrown when query is null.</exception>
        public static unsafe Object Execute(Function function)
        {
            using var buffer = new ArrayPoolBufferWriter();

            var request = ClientJson.ToJson(buffer, function, 0);
            fixed (byte* source = request)
            {
                var ptr = td_execute(source);

                byte* end = ptr;
                while (*end != 0)
                {
                    end++;
                }

                int length = (int)(end - ptr);
                if (length == 0)
                {
                    return null;
                }

                buffer.Resize(length);

                fixed (byte* dest = buffer.Bytes)
                {
                    Buffer.MemoryCopy(ptr, dest, buffer.Bytes.Length, length);
                }

                var span = new ReadOnlySpan<byte>(buffer.Bytes, 0, buffer.Bytes.Length);
                return ClientJson.FromJson(span, out _, out _);
            }
        }

        /// <summary>
        /// Launches a cycle which will fetch all results of queries to TDLib and incoming updates from TDLib.
        /// Must be called once on a separate dedicated thread on which all updates and query results from all Clients will be handled.
        /// Never returns.
        /// </summary>
        public static void Run()
        {
            while (true)
            {
                var response = Receive(300.0, out int client_id, out long request_id);
                if (response != null)
                {
                    bool isClosed = response is UpdateAuthorizationState { AuthorizationState: AuthorizationStateClosed } && request_id == 0;

                    if (request_id == 0
                        ? _updateHandlers.TryGetValue(client_id, out ClientResultHandler handler)
                        : _handlers.TryRemove(request_id, out handler))
                    {
                        // TODO try/catch
                        handler.OnResult(response);
                    }

                    if (isClosed)
                    {
                        _updateHandlers.TryRemove(client_id, out _);
                    }
                }
            }
        }

        private static byte[] _buffer = new byte[1 << 18];

        public static unsafe Object Receive(double timeout, out int clientId, out long requestId)
        {
            clientId = 0;
            requestId = 0;

            var ptr = td_receive(timeout);
            if (ptr == null)
            {
                return null;
            }

            byte* end = ptr;
            while (*end != 0)
            {
                end++;
            }

            int length = (int)(end - ptr);
            if (length == 0)
            {
                return null;
            }

            if (_buffer.Length < length)
            {
                Array.Resize(ref _buffer, length);
            }

            fixed (byte* dest = _buffer)
            {
                Buffer.MemoryCopy(ptr, dest, _buffer.Length, length);
            }

            var span = new ReadOnlySpan<byte>(_buffer, 0, _buffer.Length);
            return ClientJson.FromJson(span, out clientId, out requestId);
        }

        private static readonly object _logMutex = new();
        private static LogMessageCallback _logMessageCallback;

        public static void SetLogMessageCallback(int max_verbosity_level, LogMessageCallback callback)
        {
            lock (_logMutex)
            {
                if (callback == null)
                {
                    td_set_log_message_callback(max_verbosity_level, null);
                    _logMessageCallback = null;
                }
                else
                {
                    _logMessageCallback = callback;
                    td_set_log_message_callback(max_verbosity_level, LogMessageCallbackWrapper);
                }
            }
        }

        private static void LogMessageCallbackWrapper(int verbosity_level, IntPtr message)
        {
            var callback = _logMessageCallback;
            if (callback != null)
            {
                callback(verbosity_level, Marshal.PtrToStringUTF8(message));
            }
        }
    }
}
