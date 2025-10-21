//
// Copyright (c) Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Buffers;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace Telegram.Common
{
    public interface IAnimation
    {
        double FrameRate { get; }

        void RenderNextFrame();
    }

    public class AnimationScheduler : IDisposable
    {
        private class AnimationBatch
        {
            public double FrameRate { get; init; }
            public double Interval { get; init; }
            public double MaxExecution { get; init; }
            public int MaxLength { get; init; }

            public List<IAnimation> Animations { get; init; } = new List<IAnimation>();
            public Timer Timer { get; set; }
            public Stopwatch ExecutionTimer { get; } = new Stopwatch();
            public long LastTickTime { get; set; }

            public AnimationBatch(double frameRate)
            {
                FrameRate = frameRate;
                Interval = 1000.0 / frameRate;
                MaxExecution = 1000.0 / frameRate * 0.8; // 0.75?
                MaxLength = GetBatchSize(frameRate);
            }

            private static int GetBatchSize(double size)
            {
                // Maybe needs some more tuning.
                const double a = 400.0;
                const double b = 0.8;
                int batch = (int)Math.Round(a * Math.Pow(size, -b));
                return Math.Clamp(batch, 1, 50);
            }

            public IEnumerator<IAnimation> GetEnumerator()
            {
                return new AnimationEnumerator(Animations);
            }

            struct AnimationEnumerator : IEnumerator<IAnimation>
            {
                private readonly ICollection<IAnimation> _source;
                private readonly IAnimation[] _buffer;
                private readonly int _count;
                private int _index;
                private bool _disposed;

                public AnimationEnumerator(ICollection<IAnimation> source)
                {
                    _source = source;
                    _count = source.Count;
                    _buffer = ArrayPool<IAnimation>.Shared.Rent(_count);
                    source.CopyTo(_buffer, 0);
                    _index = -1;
                    _disposed = false;
                }

                public IAnimation Current
                {
                    get
                    {
                        if (_index < 0 || _index >= _count)
                        {
                            throw new InvalidOperationException();
                        }

                        return _buffer[_index];
                    }
                }

                object IEnumerator.Current => Current;

                public bool MoveNext()
                {
                    if (_index < _count - 1)
                    {
                        _index++;
                        return true;
                    }
                    return false;
                }

                public void Reset()
                {
                    _index = -1;
                }

                public void Dispose()
                {
                    if (!_disposed && _buffer != null)
                    {
                        ArrayPool<IAnimation>.Shared.Return(_buffer);
                        _disposed = true;
                    }
                }
            }
        }

        private readonly ConcurrentDictionary<IAnimation, byte> _allAnimations = new();
        private readonly ConcurrentDictionary<double, List<AnimationBatch>> _batchesByFps = new();
        private readonly object _batchLock = new();
        private bool _disposed;

        public AnimationScheduler()
        {
        }

        public void Subscribe(IAnimation animation)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(AnimationScheduler));
            if (animation == null) throw new ArgumentNullException(nameof(animation));

            if (_allAnimations.TryAdd(animation, 0))
            {
                lock (_batchLock)
                {
                    AssignAnimationToBatch(animation);
                }
            }
        }

        public void Unsubscribe(IAnimation animation)
        {
            if (_allAnimations.TryRemove(animation, out _))
            {
                lock (_batchLock)
                {
                    RemoveAnimationFromBatches(animation);
                }
            }
        }

        public int ActiveAnimationCount => _allAnimations.Count;

        public string GetBatchingStats()
        {
            lock (_batchLock)
            {
                var stats = new List<string>();
                foreach (var kvp in _batchesByFps.OrderByDescending(x => x.Key))
                {
                    var fps = kvp.Key;
                    var batches = kvp.Value;
                    var totalAnims = batches.Sum(b => b.Animations.Count);
                    stats.Add($"{fps}fps: {totalAnims} animations in {batches.Count} batch(es)");
                }
                return string.Join("\n", stats);
            }
        }

        private void AssignAnimationToBatch(IAnimation animation)
        {
            var fps = animation.FrameRate;
            var batches = _batchesByFps.GetOrAdd(fps, _ => new List<AnimationBatch>());

            // Try to find a batch with room
            // TODO: too small batches?
            var targetBatch = batches.FirstOrDefault(b => b.Animations.Count < b.MaxLength);

            if (targetBatch == null)
            {
                targetBatch = new AnimationBatch(fps);
                batches.Add(targetBatch);

                var interval = (int)Math.Max(1, targetBatch.Interval);
                targetBatch.Timer = new Timer(
                    _ => ExecuteBatch(targetBatch),
                    null,
                    interval,
                    interval
                );
                targetBatch.LastTickTime = Stopwatch.GetTimestamp();
            }

            targetBatch.Animations.Add(animation);
        }

        private void RemoveAnimationFromBatches(IAnimation animation)
        {
            var fps = animation.FrameRate;
            if (_batchesByFps.TryGetValue(fps, out var batches))
            {
                for (int i = 0; i < batches.Count; i++)
                {
                    AnimationBatch batch = batches[i];
                    batch.Animations.Remove(animation);

                    if (batch.Animations.Count == 0)
                    {
                        batch.Timer?.Dispose();
                        batches.RemoveAt(i);
                        i--;
                    }
                }

                if (batches.Count == 0)
                {
                    _batchesByFps.TryRemove(fps, out _);
                }
            }
        }

        private void ExecuteBatch(AnimationBatch batch)
        {
            if (_disposed) return;

            batch.ExecutionTimer.Restart();

            foreach (var animation in batch)
            {
                try
                {
                    animation.RenderNextFrame();
                }
                catch (Exception ex)
                {
                    // Log exception but don't let one animation crash the batch
                    Debug.WriteLine($"Animation threw exception: {ex.Message}");
                }

                // Check if we're exceeding our time budget
                if (batch.ExecutionTimer.ElapsedMilliseconds > batch.MaxExecution)
                {
                    // Mark for rebalancing
                    ThreadPool.QueueUserWorkItem(_ => RebalanceBatch(batch));
                    break;
                }
            }

            batch.ExecutionTimer.Stop();
        }

        private void RebalanceBatch(AnimationBatch overloadedBatch)
        {
            lock (_batchLock)
            {
                // If batch is now small enough, no need to rebalance
                if (overloadedBatch.Animations.Count <= 2) return;

                var fps = overloadedBatch.FrameRate;
                var animations = overloadedBatch.Animations.ToList();

                // Split roughly in half
                var splitPoint = animations.Count / 2;
                var animationsToMove = animations.Skip(splitPoint).ToList();

                // Remove from current batch
                foreach (var anim in animationsToMove)
                {
                    overloadedBatch.Animations.Remove(anim);
                }

                // Create new batch
                var newBatch = new AnimationBatch(fps);
                newBatch.Animations.AddRange(animationsToMove);

                var batches = _batchesByFps[fps];
                batches.Add(newBatch);

                // Start timer for new batch
                var intervalMs = (int)Math.Max(1, newBatch.Interval);
                newBatch.Timer = new Timer(
                    _ => ExecuteBatch(newBatch),
                    null,
                    intervalMs,
                    intervalMs
                );
                newBatch.LastTickTime = Stopwatch.GetTimestamp();

                Debug.WriteLine($"Rebalanced {fps}fps batch: split into {overloadedBatch.Animations.Count} and {newBatch.Animations.Count} animations");
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            lock (_batchLock)
            {
                foreach (var batchList in _batchesByFps.Values)
                {
                    foreach (var batch in batchList)
                    {
                        batch.Timer?.Dispose();
                    }
                }
                _batchesByFps.Clear();
            }

            _allAnimations.Clear();
        }
    }
}
