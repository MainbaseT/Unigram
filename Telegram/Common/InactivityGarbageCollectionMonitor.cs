//
// Copyright (c) Fela Ameghino 2015-2025
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
using Windows.UI.Core;

namespace Telegram.Common
{
    public sealed class InactivityGarbageCollectionMonitor
    {
        private sealed class WindowState
        {
            public CoreWindow Window { get; }

            public TypedEventHandler<CoreWindow, PointerEventArgs> PointerMovedHandler;
            public TypedEventHandler<CoreWindow, PointerEventArgs> PointerPressedHandler;
            public TypedEventHandler<CoreWindow, PointerEventArgs> PointerReleasedHandler;
            public TypedEventHandler<CoreWindow, PointerEventArgs> PointerWheelChangedHandler;
            public TypedEventHandler<CoreWindow, KeyEventArgs> KeyDownHandler;
            public TypedEventHandler<CoreWindow, KeyEventArgs> KeyUpHandler;
            public TypedEventHandler<CoreWindow, CoreWindowEventArgs> ClosedHandler;

            public WindowState(CoreWindow window)
            {
                Window = window;
            }
        }

        private readonly object _syncLock = new object();
        private readonly Dictionary<CoreWindow, WindowState> _windowStates = new Dictionary<CoreWindow, WindowState>();
        private readonly Timer _checkTimer;

        private long _lastActivityTicks;
        private long _lastRecordedActivityTicks;
        private volatile bool _xamlCollectionRequested;
        private volatile bool _isDisposed;
        private long _lastCollectionTicks;

        private static volatile int _requested;
        private static volatile int _count;
        public static string Debug => $"{_count}/{_requested}";

        public TimeSpan InactivityTimeout { get; set; } = TimeSpan.FromSeconds(1);
        public TimeSpan DebounceDelay { get; set; } = TimeSpan.FromMilliseconds(500);
        public TimeSpan CheckInterval { get; }
        public TimeSpan ActivityThrottle { get; set; } = TimeSpan.FromMilliseconds(100);

        public InactivityGarbageCollectionMonitor(TimeSpan? checkInterval = null)
        {
            CheckInterval = checkInterval ?? TimeSpan.FromMilliseconds(250);

            long now = (long)Logger.TickCount;
            _lastActivityTicks = now;
            _lastRecordedActivityTicks = now;

            // Start the timer
            _checkTimer = new Timer(
                OnTimerCallback,
                state: null,
                dueTime: CheckInterval,
                period: CheckInterval);

            NativeUtils.SetCollectCallback(RhCollect, SettingsService.Current.Diagnostics.DisableXamlGcCollect);
        }

        public void StartMonitoring(CoreWindow window)
        {
            if (_isDisposed)
                return;

            WindowState state;
            bool isNewWindow;

            lock (_syncLock)
            {
                if (_windowStates.TryGetValue(window, out state))
                {
                    return;
                }

                state = new WindowState(window);
                _windowStates.Add(window, state);
                isNewWindow = true;
            }

            if (isNewWindow)
            {
                state.PointerMovedHandler = (sender, args) => RecordActivityThrottled();
                state.PointerPressedHandler = (sender, args) => RecordActivityImmediate();
                state.PointerReleasedHandler = (sender, args) => RecordActivityImmediate();
                state.PointerWheelChangedHandler = (sender, args) => RecordActivityThrottled();
                state.KeyDownHandler = (sender, args) => RecordActivityImmediate();
                state.KeyUpHandler = (sender, args) => RecordActivityThrottled();

                state.ClosedHandler = (sender, args) =>
                {
                    StopMonitoring(window);
                };

                window.PointerMoved += state.PointerMovedHandler;
                window.PointerPressed += state.PointerPressedHandler;
                window.PointerReleased += state.PointerReleasedHandler;
                window.PointerWheelChanged += state.PointerWheelChangedHandler;
                window.KeyDown += state.KeyDownHandler;
                window.KeyUp += state.KeyUpHandler;
                window.Closed += state.ClosedHandler;
            }
        }

        public void StopMonitoring(CoreWindow window)
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

            if (state.PointerMovedHandler != null)
                window.PointerMoved -= state.PointerMovedHandler;

            if (state.PointerPressedHandler != null)
                window.PointerPressed -= state.PointerPressedHandler;

            if (state.PointerReleasedHandler != null)
                window.PointerReleased -= state.PointerReleasedHandler;

            if (state.PointerWheelChangedHandler != null)
                window.PointerWheelChanged -= state.PointerWheelChangedHandler;

            if (state.KeyDownHandler != null)
                window.KeyDown -= state.KeyDownHandler;

            if (state.KeyUpHandler != null)
                window.KeyUp -= state.KeyUpHandler;

            if (state.ClosedHandler != null)
                window.Closed -= state.ClosedHandler;

            state.PointerMovedHandler = null;
            state.PointerPressedHandler = null;
            state.PointerReleasedHandler = null;
            state.PointerWheelChangedHandler = null;
            state.KeyDownHandler = null;
            state.KeyUpHandler = null;
            state.ClosedHandler = null;
        }

        private void RhCollect(int generation, int mode)
        {
            Logger.Info(string.Format("generation: {0}, mode: {1}", generation, mode));
            
            _requested++;
            DisconnectUnusedReferenceSources();
        }

        public void DisconnectUnusedReferenceSources()
        {
            if (_isDisposed)
                return;

            _xamlCollectionRequested = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RecordActivityImmediate()
        {
            long now = (long)Logger.TickCount;
            Interlocked.Exchange(ref _lastActivityTicks, now);
            Interlocked.Exchange(ref _lastRecordedActivityTicks, now);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RecordActivityThrottled()
        {
            long now = (long)Logger.TickCount;

            long lastRecorded = Interlocked.Read(ref _lastRecordedActivityTicks);
            long timeSinceLastRecord = now - lastRecorded;

            if (timeSinceLastRecord >= (long)ActivityThrottle.TotalMilliseconds)
            {
                Interlocked.Exchange(ref _lastActivityTicks, now);
                Interlocked.Exchange(ref _lastRecordedActivityTicks, now);
            }
        }

        private void OnTimerCallback(object state)
        {
            if (_isDisposed)
                return;

            TryTriggerCollection();
        }

        private void TryTriggerCollection()
        {
            if (_isDisposed)
                return;

            if (!_xamlCollectionRequested)
                return;

            long currentTicks = (long)Logger.TickCount;

            long lastCollectionTicks = Interlocked.Read(ref _lastCollectionTicks);
            long timeSinceLastCollection = currentTicks - lastCollectionTicks;

            if (timeSinceLastCollection < (long)DebounceDelay.TotalMilliseconds)
                return;

            long lastActivity = Interlocked.Read(ref _lastActivityTicks);
            long timeSinceActivity = currentTicks - lastActivity;

            bool shouldCollect = timeSinceActivity >= (long)InactivityTimeout.TotalMilliseconds;

            if (shouldCollect)
            {
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
}
