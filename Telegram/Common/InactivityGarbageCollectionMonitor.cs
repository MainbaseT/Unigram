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
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Common
{
    public static class InactivityGarbageCollectionMonitor
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

        private static readonly object _syncLock = new object();
        private static readonly Dictionary<CoreWindow, WindowState> _windowStates = new Dictionary<CoreWindow, WindowState>();
        private static readonly Timer _checkTimer;

        private static long _lastActivityTicks;
        private static long _lastRecordedActivityTicks;
        private static volatile bool _directManipulationStarted;
        private static volatile bool _xamlCollectionRequested;
        private static long _lastCollectionTicks;

        private static volatile int _requested;
        private static volatile int _count;
        public static string Debug => $" {_count}/{_requested}{(_xamlCollectionRequested ? "*" : "")}";

        public static TimeSpan InactivityTimeout { get; set; } = TimeSpan.FromSeconds(2);
        public static TimeSpan DebounceDelay { get; set; } = TimeSpan.FromMilliseconds(500);
        public static TimeSpan CheckInterval { get; } = TimeSpan.FromMilliseconds(250);
        public static TimeSpan ActivityThrottle { get; set; } = TimeSpan.FromMilliseconds(100);

        static InactivityGarbageCollectionMonitor()
        {
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

        public static void StartMonitoring(CoreWindow window)
        {
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

        private static void RhCollect(int generation, int mode)
        {
            Logger.Info(string.Format("generation: {0}, mode: {1}", generation, mode));

            _requested++;
            DisconnectUnusedReferenceSources();
        }

        public static void DisconnectUnusedReferenceSources()
        {
            _xamlCollectionRequested = true;
        }

        public static void DirectManipulationStarted()
        {
            _directManipulationStarted = true;
        }

        public static void DirectManipulationCompleted()
        {
            _directManipulationStarted = false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void RecordActivityImmediate()
        {
            long now = (long)Logger.TickCount;
            Interlocked.Exchange(ref _lastActivityTicks, now);
            Interlocked.Exchange(ref _lastRecordedActivityTicks, now);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void RecordActivityThrottled()
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

        private static void OnTimerCallback(object state)
        {
            TryTriggerCollection();
        }

        private static void TryTriggerCollection()
        {
            if (_directManipulationStarted || !_xamlCollectionRequested)
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

    public static class XamlReferenceTracker
    {
        public static bool GetIsAttached(DependencyObject obj)
        {
            return (bool)obj.GetValue(IsAttachedProperty);
        }

        public static void SetIsAttached(DependencyObject obj, bool value)
        {
            obj.SetValue(IsAttachedProperty, value);
        }

        public static readonly DependencyProperty IsAttachedProperty =
            DependencyProperty.RegisterAttached("IsAttached", typeof(bool), typeof(XamlReferenceTracker), new PropertyMetadata(false, OnIsAttachedChanged));

        private static void OnIsAttachedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not ScrollViewer sender || e.NewValue is not bool attached)
            {
                return;
            }

            if (attached)
            {
                sender.DirectManipulationStarted += OnDirectManipulationStarted;
                sender.DirectManipulationCompleted += OnDirectManipulationCompleted;
            }
            else
            {
                sender.DirectManipulationStarted -= OnDirectManipulationStarted;
                sender.DirectManipulationCompleted -= OnDirectManipulationCompleted;
            }
        }

        private static void OnDirectManipulationStarted(object sender, object e)
        {
            InactivityGarbageCollectionMonitor.DirectManipulationStarted();

            if (sender is ScrollViewer scrollViewer)
            {
                scrollViewer.Unloaded += OnDirectManipulationCompleted;
            }
        }

        private static void OnDirectManipulationCompleted(object sender, object e)
        {
            InactivityGarbageCollectionMonitor.DirectManipulationCompleted();

            if (sender is ScrollViewer scrollViewer)
            {
                scrollViewer.Unloaded -= OnDirectManipulationCompleted;
            }
        }
    }
}
