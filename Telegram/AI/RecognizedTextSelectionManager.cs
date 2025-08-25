using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using Telegram.Native.AI;

namespace Telegram.AI
{
    public record RecognizedTextSelectionChangedEventArgs(RecognizedTextSelection NewSelection);

    public class RecognizedTextSelectionManager
    {
        private readonly IList<RecognizedLine> _lines;
        private readonly IList<RecognizedTextBlock> _blocks;

        public IList<RecognizedLine> Lines => _lines;
        public IList<RecognizedTextBlock> Blocks => _blocks;

        public RecognizedTextSelectionManager(RecognizedText result)
        {
            //var test = result.Lines.ToList();
            //test.Shuffle();
            _blocks = ClusterIntoBlocks(result.Lines);
            _lines = _blocks.SelectMany(x => x.Lines).ToList();
        }

        private static IList<RecognizedTextBlock> ClusterIntoBlocks(IList<RecognizedLine> lines)
        {
            if (lines.Count == 0) return Array.Empty<RecognizedTextBlock>();

            var blocks = new List<List<RecognizedLine>>();
            var visited = new HashSet<int>();

            for (int i = 0; i < lines.Count; i++)
            {
                if (visited.Contains(i)) continue;

                var block = new List<RecognizedLine>();
                var queue = new Queue<int>();
                queue.Enqueue(i);

                float currentAngle = GetLineAngle(lines[i]);

                while (queue.Count > 0)
                {
                    int idx = queue.Dequeue();
                    if (visited.Contains(idx)) continue;

                    var prevLine = lines[idx];
                    block.Add(prevLine);
                    visited.Add(idx);

                    for (int j = 0; j < lines.Count; j++)
                    {
                        if (visited.Contains(j)) continue;
                        var currLine = lines[j];

                        float horizontalScore = HorizontalScore(prevLine.BoundingBox, currLine.BoundingBox, out ColumnAlignment alignment);
                        float verticalScore = VerticalScore(prevLine.BoundingBox, currLine.BoundingBox, alignment);

                        float angleDiff = Math.Abs(GetLineAngle(currLine) - currentAngle);
                        float angleTolerance = (float)(Math.PI / 18); // 10 degrees
                        float angleScore = 1 - Math.Min(angleDiff / angleTolerance, 1);

                        // --- STEP 4: merge score ---
                        float mergeScore = verticalScore * 0.5f + horizontalScore * 0.3f + angleScore * 0.2f;

                        // Tunable threshold
                        if (mergeScore >= 0.7)
                        {
                            queue.Enqueue(j);
                            currentAngle = (currentAngle + GetLineAngle(currLine)) / 2;
                        }
                    }
                }

                blocks.Add(block);
            }

            return SortReadingOrder(blocks.Select(block => new RecognizedTextBlock(SortReadingOrder(block))));
        }

        private static List<RecognizedLine> SortReadingOrder(List<RecognizedLine> boxes)
        {
            if (boxes.Count == 0) return new List<RecognizedLine>();
            if (boxes.Count == 1) return new List<RecognizedLine>(boxes);

            // Estimate the rotation angle from the first box
            float GetRotationAngle(RecognizedLine box)
            {
                float dx = box.BoundingBox.TopRight.X - box.BoundingBox.TopLeft.X;
                float dy = box.BoundingBox.TopRight.Y - box.BoundingBox.TopLeft.Y;
                return (float)Math.Atan2(dy, dx);
            }

            float rotationAngle = GetRotationAngle(boxes.First());

            // Get the rotated center Y coordinate for sorting
            float GetRotatedCenterY(RecognizedTextBoundingBox box)
            {
                var centroid = box.Center();
                var rotated = RecognizedTextBoundingBoxHelper.RotatePoint(centroid, rotationAngle);
                return rotated.Y;
            }

            // Simply sort by the rotated Y coordinate
            return boxes.OrderBy(box => GetRotatedCenterY(box.BoundingBox)).ToList();
        }

        public enum ColumnAlignment
        {
            Left,
            Center,
            Right
        }

        private static float HorizontalScore(RecognizedTextBoundingBox a, RecognizedTextBoundingBox b, out ColumnAlignment alignment)
        {
            // Get the rotation angle (assuming both boxes have similar rotation)
            float GetRotationAngle(RecognizedTextBoundingBox box)
            {
                float dx = box.TopRight.X - box.TopLeft.X;
                float dy = box.TopRight.Y - box.TopLeft.Y;
                return (float)Math.Atan2(dy, dx);
            }

            float rotationAngle = (GetRotationAngle(a) + GetRotationAngle(b)) / 2.0f;

            // Get rotated coordinates for alignment comparison
            var aLeftRotated = RecognizedTextBoundingBoxHelper.RotatePoint(a.TopLeft, rotationAngle);
            var aRightRotated = RecognizedTextBoundingBoxHelper.RotatePoint(a.TopRight, rotationAngle);
            var aCenterRotated = ((aLeftRotated.X + aRightRotated.X) / 2.0f, (aLeftRotated.Y + aRightRotated.Y) / 2.0f);

            var bLeftRotated = RecognizedTextBoundingBoxHelper.RotatePoint(b.TopLeft, rotationAngle);
            var bRightRotated = RecognizedTextBoundingBoxHelper.RotatePoint(b.TopRight, rotationAngle);
            var bCenterRotated = ((bLeftRotated.X + bRightRotated.X) / 2.0f, (bLeftRotated.Y + bRightRotated.Y) / 2.0f);

            // Compare only X coordinates after rotation (horizontal alignment)
            float leftDiff = Math.Abs(aLeftRotated.X - bLeftRotated.X);
            float rightDiff = Math.Abs(aRightRotated.X - bRightRotated.X);
            float centerDiff = Math.Abs(aCenterRotated.Item1 - bCenterRotated.Item1);

            float minDiff = Math.Min(leftDiff, Math.Min(rightDiff, centerDiff));

            // Determine alignment based on which difference was smallest
            if (minDiff == leftDiff)
                alignment = ColumnAlignment.Left;
            else if (minDiff == rightDiff)
                alignment = ColumnAlignment.Right;
            else
                alignment = ColumnAlignment.Center;

            // Calculate tolerance based on rotated widths
            float aWidth = Math.Abs(aRightRotated.X - aLeftRotated.X);
            float bWidth = Math.Abs(bRightRotated.X - bLeftRotated.X);
            float tolerance = Math.Max(aWidth, bWidth) * 0.6f;

            return Math.Max(0, 1 - Math.Min(minDiff / tolerance, 1));
        }

        private static float VerticalScore(RecognizedTextBoundingBox a, RecognizedTextBoundingBox b, ColumnAlignment alignment)
        {
            float currPrev = VerticalDistance(a, b, alignment);
            float prevCurr = VerticalDistance(b, a, alignment);

            float verticalGap = Math.Min(currPrev, prevCurr);
            float avgHeight = (a.Height() + b.Height()) / 2;

            float tolerance = avgHeight * 0.8f; // per-line tolerance
            return 1 - Math.Min(verticalGap / tolerance, 1);
        }

        private static float VerticalDistance(RecognizedTextBoundingBox a, RecognizedTextBoundingBox b, ColumnAlignment alignment)
        {
            var (ap, bp) = alignment switch
            {
                ColumnAlignment.Left => (
                    a.BottomLeft,  // Bottom-left of a
                    b.TopLeft   // Top-left of b
                ),
                ColumnAlignment.Right => (
                    a.BottomRight,  // Bottom-right of a
                    b.TopRight   // Top-right of b
                ),
                ColumnAlignment.Center => (
                    (a.BottomRight + a.BottomLeft) / 2,  // Bottom-center of a
                    (b.TopLeft + b.TopRight) / 2   // Top-center of b
                ),
                _ => throw new ArgumentException($"Unknown alignment: {alignment}")
            };

            return RecognizedTextBoundingBoxHelper.Distance(ap, bp);
        }

        public static float GetLineAngle(RecognizedLine line)
        {
            var bb = line.BoundingBox;
            // vector from bottom-left to bottom-right (baseline)
            float dx = bb.BottomRight.X - bb.BottomLeft.X;
            float dy = bb.BottomRight.Y - bb.BottomLeft.Y;
            return (float)Math.Atan2(dy, dx);
        }

        private static List<RecognizedTextBlock> SortReadingOrder(IEnumerable<RecognizedTextBlock> items)
        {
            // There's large room for improvement here

            var boxInfos = items.Select(obj =>
            {
                var b = RecognizedTextBoundingBoxHelper.Compute(obj.Lines.Select(x => x.BoundingBox));
                var xs = new[] { b.TopLeft.X, b.TopRight.X, b.BottomRight.X, b.BottomLeft.X };
                var ys = new[] { b.TopLeft.Y, b.TopRight.Y, b.BottomRight.Y, b.BottomLeft.Y };

                return new BoxInfo(
                    obj,
                    xs.Min(),
                    ys.Min(),
                    Math.Abs(b.TopRight.X - b.TopLeft.X) + Math.Abs(b.BottomRight.X - b.BottomLeft.X) / 2f // rough width
                );
            }).ToList();

            float avgWidth = boxInfos.Average(b => b.Width);
            float colThreshold = avgWidth * 0.5f;

            // Group into columns
            var columns = new List<List<BoxInfo>>();
            foreach (var info in boxInfos.OrderBy(b => b.MinX))
            {
                var col = columns.FirstOrDefault(c => Math.Abs(c[0].MinX - info.MinX) < colThreshold);
                if (col == null)
                {
                    col = new List<BoxInfo>();
                    columns.Add(col);
                }
                col.Add(info);
            }

            // Sort columns left-to-right, then top-to-bottom
            var ordered = columns
                .OrderBy(c => c.Min(b => b.MinX))
                .SelectMany(c => c.OrderBy(b => b.MinY))
                .Select(b => b.Item)
                .ToList();

            return ordered;
        }

        private readonly struct BoxInfo
        {
            public readonly RecognizedTextBlock Item;
            public readonly float MinX;
            public readonly float MinY;
            public readonly float Width;

            public BoxInfo(RecognizedTextBlock item, float minX, float minY, float width)
            {
                Item = item;
                MinX = minX;
                MinY = minY;
                Width = width;
            }
        }

        public bool IsPointWithinText(Point point)
        {
            var start = point.ToVector2();
            start *= _inverseScale;

            return _blocks.Any(x => x.Polygons.Any(x => x.ContainsPoint(start)));
        }

        private Vector2 _scale = Vector2.One;
        private Vector2 _inverseScale = Vector2.One;

        public Vector2 Scale
        {
            get => _scale;
            set
            {
                _scale = value;
                _inverseScale = Vector2.One / value;
            }
        }

        public event EventHandler<RecognizedTextSelectionChangedEventArgs> SelectionChanged;

        private SelectionPointer _selectionStart = SelectionPointer.Empty;
        private SelectionPointer _selectionEnd = SelectionPointer.Empty;

        public RecognizedTextSelection Selection { get; private set; }

        public void SelectTextBetweenPoints(Point startPoint, Point endPoint)
        {
            var start = startPoint.ToVector2();
            var end = endPoint.ToVector2();

            start *= _inverseScale;
            end *= _inverseScale;

            var firstBlock = RecognizedTextBoundingBoxHelper.FindNearestIndex(_blocks, start);
            var lastBlock = RecognizedTextBoundingBoxHelper.FindNearestIndex(_blocks, end);

            if (firstBlock == -1 || lastBlock == -1)
            {
                ClearSelection();
                return;
            }

            var backwardBlocks = firstBlock > lastBlock;
            var minBlock = Math.Min(firstBlock, lastBlock);
            var maxBlock = Math.Max(firstBlock, lastBlock);

            var lines = new List<IList<RecognizedTextBoundingBox>>();
            var content = new StringBuilder();

            SelectionPointer selectionStart = default;
            SelectionPointer selectionEnd = default;

            Debug.WriteLine("first block: " + firstBlock + ", last block: " + lastBlock + ", going " + (backwardBlocks ? "backward" : "forward"));

            for (int i = minBlock; i <= maxBlock; i++)
            {
                var block = _blocks[i];

                int firstLine = 0;
                int lastLine = block.Lines.Count - 1;

                if (i == minBlock)
                {
                    firstLine = RecognizedTextBoundingBoxHelper.FindNearestIndex(block.Lines, backwardBlocks ? end : start);
                    Debug.WriteLine("  found first line " + firstLine + " for block " + i + " at the " + (backwardBlocks ? "end" : "start"));
                }

                if (i == maxBlock)
                {
                    lastLine = RecognizedTextBoundingBoxHelper.FindNearestIndex(block.Lines, backwardBlocks ? start : end);
                    Debug.WriteLine("  found last line " + lastLine + " for block " + i + " at the " + (backwardBlocks ? "start" : "end"));
                }

                var backwardLines = backwardBlocks || firstLine > lastLine;
                var minLine = Math.Min(firstLine, lastLine);
                var maxLine = Math.Max(firstLine, lastLine);

                Debug.WriteLine("    first line: " + firstLine + ", last line: " + lastLine + ", going " + (backwardLines ? "backward" : "forward"));

                for (int j = minLine; j <= maxLine; j++)
                {
                    var line = block.Lines[j];

                    var firstWord = 0;
                    var lastWord = line.Words.Count - 1;

                    if (i == minBlock && j == minLine)
                    {
                        firstWord = RecognizedTextBoundingBoxHelper.FindNearestIndex(line.Words, backwardLines ? end : start);
                        selectionStart = new SelectionPointer(i, j, firstWord);
                        Debug.WriteLine("    found first word " + firstWord + " for line " + j + " at the " + (backwardLines ? "end" : "start"));
                    }

                    if (i == maxBlock && j == maxLine)
                    {
                        lastWord = RecognizedTextBoundingBoxHelper.FindNearestIndex(line.Words, backwardLines ? start : end);
                        selectionEnd = new SelectionPointer(i, j, lastWord);
                        Debug.WriteLine("    found last word " + lastWord + " for line " + j + " at the " + (backwardLines ? "start" : "end"));
                    }

                    var minWord = Math.Min(firstWord, lastWord);
                    var maxWord = Math.Max(firstWord, lastWord);

                    if (minWord != 0 || maxWord != line.Words.Count - 1)
                    {
                        var words = new List<RecognizedWord>(maxWord - minWord + 1);

                        for (int k = minWord; k <= maxWord; k++)
                        {
                            words.Add(line.Words[k]);
                        }

                        lines.Add(words.Select(x => x.BoundingBox).ToList());
                        content.AppendLine(string.Join(' ', words.Select(x => x.Text)));
                    }
                    else
                    {
                        lines.Add(new[] { line.BoundingBox });
                        content.AppendLine(line.Text);
                    }
                }
            }

            Debug.WriteLine("");

            UpdateSelection(selectionStart, selectionEnd, content.ToString(), lines.Select(y => RecognizedTextBoundingBoxHelper.Compute(y)));
        }

        public void ExpandSelection(Point point, bool singleLine)
        {
            var start = point.ToVector2();
            start *= _inverseScale;

            var blockIndex = RecognizedTextBoundingBoxHelper.FindNearestIndex(_blocks, start);
            if (blockIndex != -1)
            {
                var block = _blocks[blockIndex];

                if (singleLine)
                {
                    var lineIndex = RecognizedTextBoundingBoxHelper.FindNearestIndex(block.Lines, start);
                    if (lineIndex != -1)
                    {
                        var line = block.Lines[lineIndex];
                        var selectionStart = new SelectionPointer(blockIndex, lineIndex, 0);
                        var selectionEnd = new SelectionPointer(blockIndex, lineIndex, line.Words.Count - 1);

                        UpdateSelection(selectionStart, selectionEnd, line.Text, new[] { line.BoundingBox });
                        return;
                    }
                }
                else
                {
                    var selectionStart = new SelectionPointer(blockIndex, 0, 0);
                    var selectionEnd = new SelectionPointer(blockIndex, block.Lines.Count - 1, block.Lines[^1].Words.Count - 1);

                    var content = string.Join('\n', block.Lines.Select(x => x.Text));
                    var boundingBoxes = block.Lines.Select(x => x.BoundingBox);

                    UpdateSelection(selectionStart, selectionEnd, content, boundingBoxes);
                    return;
                }
            }

            ClearSelection();
        }

        public void SelectAll()
        {
            var selectionStart = new SelectionPointer(0, 0, 0);
            var selectionEnd = new SelectionPointer(_blocks.Count - 1, _blocks[^1].Lines.Count - 1, _blocks[^1].Lines[^1].Words.Count - 1);

            var content = string.Join('\n', _blocks.Select(x => string.Join('\n', x.Lines.Select(x => x.Text))));
            var boundingBoxes = _blocks.SelectMany(x => x.Lines.Select(x => x.BoundingBox));

            UpdateSelection(selectionStart, selectionEnd, content, boundingBoxes);
        }

        public void ClearSelection()
        {
            if (_selectionStart != SelectionPointer.Empty && _selectionEnd != SelectionPointer.Empty)
            {
                _selectionStart = SelectionPointer.Empty;
                _selectionEnd = SelectionPointer.Empty;

                Selection = null;
                SelectionChanged?.Invoke(this, new RecognizedTextSelectionChangedEventArgs(null));
            }
        }

        private void UpdateSelection(SelectionPointer start, SelectionPointer end, string content, IEnumerable<RecognizedTextBoundingBox> boundingBoxes)
        {
            if (_selectionStart != start || _selectionEnd != end)
            {
                _selectionStart = start;
                _selectionEnd = end;

                Selection = new RecognizedTextSelection(content, boundingBoxes.ToList());
                SelectionChanged?.Invoke(this, new RecognizedTextSelectionChangedEventArgs(Selection));
            }
        }

        readonly struct SelectionPointer
        {
            public readonly int Block;
            public readonly int Line;
            public readonly int Word;

            public SelectionPointer(int block, int line, int word)
            {
                Block = block;
                Line = line;
                Word = word;
            }

            public static bool operator ==(SelectionPointer a, SelectionPointer b) => a.Block == b.Block && a.Line == b.Line && a.Word == b.Word;
            public static bool operator !=(SelectionPointer a, SelectionPointer b) => !(a == b);

            public override bool Equals(object obj) => obj is SelectionPointer p && this == p;
            public override int GetHashCode() => HashCode.Combine(Block, Line, Word);

            public static SelectionPointer Empty { get; } = new SelectionPointer(-1, -1, -1);
        }
    }
}
