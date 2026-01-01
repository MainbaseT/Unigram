//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Telegram.Native;
using Telegram.Services;
using Windows.Foundation;
using Windows.System;
using Windows.UI.Core;

namespace Telegram.Common
{
    public static class InactivityGarbageCollectionMonitor
    {
        private sealed class WindowState
        {
            public CoreWindow Window { get; }
            public volatile bool IsWindowActive;

            public TypedEventHandler<CoreWindow, WindowActivatedEventArgs> ActivatedHandler;
            public TypedEventHandler<CoreWindow, CoreWindowEventArgs> ClosedHandler;

            public WindowState(CoreWindow window)
            {
                Window = window ?? throw new ArgumentNullException(nameof(window));
                IsWindowActive = false;
            }
        }

        private static readonly object _syncLock = new object();
        private static readonly Dictionary<CoreWindow, WindowState> _windowStates = new Dictionary<CoreWindow, WindowState>();
        private static readonly Thread _monitorThread;

        private static volatile bool _xamlCollectionRequested;
        private static long _lastCollectionTicks;

        private static volatile int _requested;
        private static volatile int _count;
        public static string Debug => $" {_count}/{_requested}{(_xamlCollectionRequested ? "*" : "")}";

        public static TimeSpan InactivityTimeout { get; set; } = TimeSpan.FromSeconds(2);
        public static TimeSpan DebounceDelay { get; set; } = TimeSpan.FromMilliseconds(500);
        public static TimeSpan CheckIntervalActive { get; set; } = TimeSpan.FromMilliseconds(250);
        public static TimeSpan CheckIntervalInactive { get; set; } = TimeSpan.FromSeconds(1);

        static InactivityGarbageCollectionMonitor()
        {
            _monitorThread = new Thread(MonitorThreadProc)
            {
                Name = "InactivityGCMonitor",
                IsBackground = true
            };
            _monitorThread.Start();

            NativeUtils.SetCollectCallback(RhCollect,
                SettingsService.Current.Diagnostics.DisableXamlGcCollect,
                SettingsService.Current.Diagnostics.DisableMemoryPressure);
        }

        private static void MonitorThreadProc()
        {
            while (true)
            {
                // Determine check interval based on window activity
                bool anyWindowActive = IsAnyWindowActive();
                TimeSpan checkInterval = anyWindowActive ? CheckIntervalActive : CheckIntervalInactive;

                Thread.Sleep(checkInterval);

                TryTriggerCollection();
            }
        }

        /// <summary>
        /// Starts monitoring the specified window.
        /// Must be called on the window's thread.
        /// </summary>
        /// <param name="window">The window to monitor.</param>
        /// <exception cref="ArgumentNullException">Thrown when window is null.</exception>
        public static void StartMonitoring(CoreWindow window)
        {
            if (window == null)
                throw new ArgumentNullException(nameof(window));

            WindowState state;
            bool isNewWindow;

            lock (_syncLock)
            {
                if (_windowStates.TryGetValue(window, out state))
                {
                    // Already monitoring this window
                    return;
                }

                state = new WindowState(window);
                _windowStates.Add(window, state);
                isNewWindow = true;
            }

            if (isNewWindow)
            {
                state.ActivatedHandler = (sender, args) =>
                {
                    state.IsWindowActive = args.WindowActivationState != CoreWindowActivationState.Deactivated;
                };

                state.ClosedHandler = (sender, args) =>
                {
                    StopMonitoring(window);
                };

                window.Activated += state.ActivatedHandler;
                window.Closed += state.ClosedHandler;

                state.IsWindowActive = true;
            }
        }

        public static void StopMonitoring(CoreWindow window)
        {
            if (window == null)
                throw new ArgumentNullException(nameof(window));

            WindowState state;
            lock (_syncLock)
            {
                if (!_windowStates.TryGetValue(window, out state))
                {
                    return;
                }

                _windowStates.Remove(window);
            }

            if (state.ActivatedHandler != null)
                window.Activated -= state.ActivatedHandler;

            if (state.ClosedHandler != null)
                window.Closed -= state.ClosedHandler;

            state.ActivatedHandler = null;
            state.ClosedHandler = null;
        }

        private static void RhCollect(int generation, int mode)
        {
            Logger.Info(string.Format("generation: {0}, mode: {1}", generation, mode));
            DisconnectUnusedReferenceSources();
        }

        public static void DisconnectUnusedReferenceSources()
        {
            _requested++;

            var usageLevel = MemoryManager.AppMemoryUsageLevel;
            if (usageLevel is AppMemoryUsageLevel.High or AppMemoryUsageLevel.OverLimit)
            {
                long currentTicks = (long)Logger.TickCount;
                Interlocked.Exchange(ref _lastCollectionTicks, currentTicks);

                _count++;
                Logger.Info();

                NativeUtils.Collect = true;
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
                NativeUtils.Collect = false;
            }
            else
            {
                _xamlCollectionRequested = true;
            }

            Logger.Info(usageLevel);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsAnyWindowActive()
        {
            lock (_syncLock)
            {
                foreach (var state in _windowStates.Values)
                {
                    if (state.IsWindowActive)
                        return true;
                }
                return false;
            }
        }

        private static void TryTriggerCollection()
        {
            if (!_xamlCollectionRequested)
                return;

            long currentTicks = (long)Logger.TickCount;

            long lastCollectionTicks = Interlocked.Read(ref _lastCollectionTicks);
            long timeSinceLastCollection = currentTicks - lastCollectionTicks;

            if (timeSinceLastCollection < (long)DebounceDelay.TotalMilliseconds)
                return;

            bool anyWindowActive = IsAnyWindowActive();
            if (anyWindowActive)
            {
                uint lastInputTime = NativeUtils.GetLastInputTime();
                uint timeSinceInput = currentTicks >= lastInputTime
                    ? (uint)(currentTicks - lastInputTime)
                    : 0; // Handle wraparound conservatively

                if (timeSinceInput < (uint)InactivityTimeout.TotalMilliseconds)
                    return; // User is still active
            }
            else
            {
                if (timeSinceLastCollection < (long)InactivityTimeout.TotalMilliseconds)
                    return;
            }

            Interlocked.Exchange(ref _lastCollectionTicks, currentTicks);
            _xamlCollectionRequested = false;

            _count++;
            Logger.Info();

            NativeUtils.Collect = true;
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: false, compacting: true);
            NativeUtils.Collect = false;
        }
    }
}
