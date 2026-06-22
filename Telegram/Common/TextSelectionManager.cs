//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Text;
using Telegram.Controls;
using Telegram.Td.Api;
using Windows.Devices.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace Telegram.Common
{
    /// <summary>
    /// Drives a single continuous selection across the <see cref="ISelectableControl"/>
    /// descendants of a working tree (a parent <see cref="Panel"/>) — the read view's
    /// equivalent of selecting across an editor's blocks. It owns the gesture and
    /// renders selection purely through each control's highlight (see
    /// <see cref="ISelectableControl"/>), so it works regardless of focus and across
    /// any selectable kind (text today, formulae/etc. later).
    ///
    /// Gesture: press is only a CANDIDATE (taps and links keep working); once the
    /// pointer moves past a small threshold the manager captures the pointer and
    /// starts selecting, so moves/release continue to fire even outside the tree.
    /// </summary>
    public sealed class TextSelectionManager
    {
        private const double DragThreshold = 4.0;

        private readonly Control _root;

        // Selectable controls in document (visual pre-order) order, rebuilt per gesture.
        private readonly List<ISelectableControl> _items = new();

        private bool _candidate;       // pointer is down on a selectable, not yet a drag
        private bool _selecting;       // drag confirmed, selecting
        private bool _captured;        // the root captured the pointer
        private Pointer _pointer;
        private Point _pressPoint;     // in _root coordinates

        private ISelectableControl _anchor;
        private int _anchorPosition;

        private bool _hasSelection; // a finalized selection is currently shown
        private bool _watchingFocus; // subscribed to the (static) FocusManager.LostFocus

        // The current per-control selected ranges, in document order (for copy).
        private readonly List<(ISelectableControl Item, int Start, int End)> _selectedRanges = new();

        public event EventHandler SelectionChanged;

        public bool HasSelection => _hasSelection;

        public TextSelectionManager(Control root)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
            _root.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(OnPointerPressed), true);
            _root.AddHandler(UIElement.PointerMovedEvent, new PointerEventHandler(OnPointerMoved), true);
            _root.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler(OnPointerReleased), true);
            _root.AddHandler(UIElement.PointerCaptureLostEvent, new PointerEventHandler(OnPointerCaptureLost), true);
            // FocusManager.LostFocus is a STATIC event; if we unload mid-selection we'd
            // leak the subscription (and this whole control), so always unhook on unload.
            _root.Unloaded += OnUnloaded;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            StopWatchingFocus();
        }

        public void Detach()
        {
            StopWatchingFocus();
            _root.RemoveHandler(UIElement.PointerPressedEvent, new PointerEventHandler(OnPointerPressed));
            _root.RemoveHandler(UIElement.PointerMovedEvent, new PointerEventHandler(OnPointerMoved));
            _root.RemoveHandler(UIElement.PointerReleasedEvent, new PointerEventHandler(OnPointerReleased));
            _root.RemoveHandler(UIElement.PointerCaptureLostEvent, new PointerEventHandler(OnPointerCaptureLost));
            _root.Unloaded -= OnUnloaded;
        }

        /// <summary>Clears the highlight on every selectable control and resets state.</summary>
        public void ClearSelection()
        {
            foreach (var item in _items)
            {
                item.ClearSelection();
            }

            var had = _hasSelection;
            Reset();
            _hasSelection = false;
            StopWatchingFocus();
            if (had)
            {
                SelectionChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void Reset()
        {
            _candidate = false;
            _selecting = false;
            _captured = false;
            _pointer = null;
            _anchor = null;
            _anchorPosition = 0;
            _selectedRanges.Clear();
        }

        // --- gesture ------------------------------------------------------------

        private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            // Mouse: left button only. Touch/pen: any contact.
            var point = e.GetCurrentPoint(_root);
            if (e.Pointer.PointerDeviceType == PointerDeviceType.Mouse && !point.Properties.IsLeftButtonPressed)
            {
                return;
            }

            // A new gesture clears the previous selection's highlight.
            foreach (var item in _items)
            {
                item.ClearSelection();
            }

            Reset();
            _hasSelection = false;
            StopWatchingFocus();
            RebuildItems();

            // Anchor at the press point. If the press isn't on a selectable (media,
            // gap, ...), do nothing — let the press through.
            if (!ResolvePosition(e, point.Position, out _anchor, out _anchorPosition))
            {
                _anchor = null;
                return;
            }

            _pointer = e.Pointer;
            _pressPoint = point.Position;
            _candidate = true;

            // Capture on the ROOT so the whole gesture (moves/release, even outside the
            // tree) is delivered here and no child runs its own selection.
            _captured = _root.CapturePointer(e.Pointer);
        }

        private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if ((!_candidate && !_selecting) || e.Pointer.PointerId != _pointer?.PointerId)
            {
                return;
            }

            var rootPoint = e.GetCurrentPoint(_root).Position;

            if (_candidate)
            {
                // Promote to a real selection only once the user actually drags.
                if (Math.Abs(rootPoint.X - _pressPoint.X) < DragThreshold &&
                    Math.Abs(rootPoint.Y - _pressPoint.Y) < DragThreshold)
                {
                    return;
                }

                _candidate = false;
                _selecting = true;
                _root.Focus(FocusState.Pointer);
            }

            UpdateSelection(e, rootPoint);
            e.Handled = true;
        }

        private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerId != _pointer?.PointerId)
            {
                return;
            }

            var selecting = _selecting;

            if (_captured)
            {
                _root.ReleasePointerCapture(e.Pointer);
            }

            _candidate = false;
            _selecting = false;
            _captured = false;
            _pointer = null;

            if (selecting)
            {
                // Keep the highlight + _anchor so the selection persists and can be
                // copied. Now that it's finalized, watch for focus moving elsewhere
                // (clicked a control outside, window deactivated, ...) to clear it.
                _hasSelection = true;
                BeginWatchingFocus();
                SelectionChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void OnPointerCaptureLost(object sender, PointerRoutedEventArgs e)
        {
            _candidate = false;
            _selecting = false;
            _captured = false;
            _pointer = null;
        }

        // --- "lost focus" clearing ---------------------------------------------
        // A finalized selection doesn't take focus, so we start listening only after
        // it's made: the first focus loss means focus went somewhere else (a control
        // outside the read view, or the window deactivated), which is our cue to clear.

        private void BeginWatchingFocus()
        {
            if (_watchingFocus)
            {
                return;
            }

            _watchingFocus = true;
            FocusManager.LostFocus += OnFocusLost;
        }

        private void StopWatchingFocus()
        {
            if (!_watchingFocus)
            {
                return;
            }

            _watchingFocus = false;
            FocusManager.LostFocus -= OnFocusLost;
        }

        private void OnFocusLost(object sender, FocusManagerLostFocusEventArgs e)
        {
            ClearSelection();
        }

        // --- selection ----------------------------------------------------------

        private void UpdateSelection(PointerRoutedEventArgs e, Point rootPoint)
        {
            if (!ResolvePosition(e, rootPoint, out var current, out var currentPosition) || _anchor == null)
            {
                return;
            }

            var anchorIndex = _items.IndexOf(_anchor);
            var currentIndex = _items.IndexOf(current);
            if (anchorIndex < 0 || currentIndex < 0)
            {
                return;
            }

            // Order the two ends in document order.
            int startPos, endPos, startIndex, endIndex;
            if (currentIndex > anchorIndex || (currentIndex == anchorIndex && currentPosition >= _anchorPosition))
            {
                startPos = _anchorPosition; startIndex = anchorIndex;
                endPos = currentPosition; endIndex = currentIndex;
            }
            else
            {
                startPos = currentPosition; startIndex = currentIndex;
                endPos = _anchorPosition; endIndex = anchorIndex;
            }

            _selectedRanges.Clear();
            for (int i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                if (i < startIndex || i > endIndex)
                {
                    item.ClearSelection();
                    continue;
                }

                int from, to;
                if (startIndex == endIndex)
                {
                    from = startPos; to = endPos;
                }
                else if (i == startIndex)
                {
                    from = startPos; to = item.ContentLength;
                }
                else if (i == endIndex)
                {
                    from = 0; to = endPos;
                }
                else
                {
                    from = 0; to = item.ContentLength;
                }

                item.Select(from, to);
                _selectedRanges.Add((item, from, to));
            }
        }

        /// <summary>
        /// The whole selection as a single <see cref="FormattedText"/> — each selected
        /// control's slice in document order, joined by newlines. Null when empty.
        /// </summary>
        public FormattedText GetSelectedText()
        {
            if (_selectedRanges.Count == 0)
            {
                return null;
            }

            var builder = new StringBuilder();
            var entities = new List<TextEntity>();

            foreach (var range in _selectedRanges)
            {
                var part = range.Item.GetSelectedText(range.Start, range.End);
                if (part == null || string.IsNullOrEmpty(part.Text))
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append('\n');
                }

                var baseOffset = builder.Length;
                builder.Append(part.Text);

                if (part.Entities != null)
                {
                    foreach (var entity in part.Entities)
                    {
                        entities.Add(new TextEntity(baseOffset + entity.Offset, entity.Length, entity.Type));
                    }
                }
            }

            return builder.Length > 0 ? new FormattedText(builder.ToString(), entities) : null;
        }

        // Resolve a pointer to (control, position). Directly over a selectable -> use
        // it. Otherwise clamp sensibly: within a row (table cells share a vertical
        // band) clamp HORIZONTALLY to the nearest cell; in a vertical gap clamp to the
        // control above; above everything -> start of the first control.
        private bool ResolvePosition(PointerRoutedEventArgs e, Point rootPoint, out ISelectableControl control, out int position)
        {
            control = null;
            position = 0;
            if (_items.Count == 0)
            {
                return false;
            }

            ISelectableControl firstValid = null;  // first laid-out control (above-all clamp)
            ISelectableControl clampBefore = null;  // last control fully above the pointer
            ISelectableControl rowFirst = null;     // first control in the pointer's row (Y band)
            ISelectableControl rowLeftOf = null;    // last row control entirely left of the pointer

            foreach (var item in _items)
            {
                var element = (FrameworkElement)item;
                if (element.ActualHeight <= 0 || element.ActualWidth <= 0)
                {
                    continue; // not laid out / effectively hidden — never resolve to it
                }

                firstValid ??= item;
                var local = e.GetCurrentPoint(element).Position;

                if (local.Y >= 0 && local.Y <= element.ActualHeight)
                {
                    // in this control's vertical band (its row)
                    rowFirst ??= item;

                    if (local.X >= 0 && local.X <= element.ActualWidth)
                    {
                        control = item; // directly over it
                        position = item.GetPositionFromPoint(local);
                        return true;
                    }

                    if (local.X > element.ActualWidth)
                    {
                        rowLeftOf = item; // pointer is to the right of this cell
                    }
                }
                else if (local.Y > element.ActualHeight)
                {
                    clampBefore = item; // pointer is below this control
                }
            }

            // within a row but not over a cell: clamp horizontally
            if (rowLeftOf != null)
            {
                control = rowLeftOf;
                position = rowLeftOf.ContentLength; // to the right of this cell -> its end
                return true;
            }
            if (rowFirst != null)
            {
                control = rowFirst;
                position = 0; // left of the first cell in the row -> its start
                return true;
            }

            // vertical clamp
            if (clampBefore != null)
            {
                control = clampBefore;
                position = clampBefore.ContentLength;
                return true;
            }
            if (firstValid != null)
            {
                control = firstValid; // above everything -> start of the first control
                position = 0;
                return true;
            }

            return false;
        }

        // --- working tree -------------------------------------------------------

        private void RebuildItems()
        {
            _items.Clear();
            Collect(_root);
        }

        private void Collect(DependencyObject node)
        {
            var count = VisualTreeHelper.GetChildrenCount(node);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(node, i);

                // Skip collapsed subtrees: a hidden selectable must not participate, and
                // its stale (zero) geometry would corrupt hit-testing. Skipping at the
                // collapsed ancestor also covers selectables nested below it.
                if (child is FrameworkElement fe && fe.Visibility == Visibility.Collapsed)
                {
                    continue;
                }

                if (child is ISelectableControl selectable)
                {
                    // Only controls that opt into manager-driven (Extended) selection;
                    // a selectable control owns its own content, so never descend into it.
                    if (selectable.IsSelectionEnabled)
                    {
                        _items.Add(selectable);
                    }

                    continue;
                }

                Collect(child);
            }
        }
    }
}
