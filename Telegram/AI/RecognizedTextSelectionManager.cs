using System;
using System.Collections.Generic;
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

        private readonly RecognizedTextSpatialIndex _spatialIndex;

        public IList<RecognizedLine> Lines => _lines;
        public IList<RecognizedTextBlock> Blocks => _blocks;

        public RecognizedTextSelectionManager(RecognizedText result)
        {
            _blocks = ClusterIntoBlocks(result.Lines);
            _lines = _blocks.SelectMany(x => x.Lines).ToList();

            _spatialIndex = new RecognizedTextSpatialIndex(_blocks);
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

            // Helper function to rotate point back to axis-aligned
            (float X, float Y) RotatePoint(float x, float y, float angle)
            {
                float cos = (float)Math.Cos(-angle);
                float sin = (float)Math.Sin(-angle);
                return (x * cos - y * sin, x * sin + y * cos);
            }

            // Get the rotated center Y coordinate for sorting
            float GetRotatedCenterY(RecognizedTextBoundingBox box)
            {
                var centroid = (
                    (box.TopLeft.X + box.TopRight.X + box.BottomRight.X + box.BottomLeft.X) / 4.0f,
                    (box.TopLeft.Y + box.TopRight.Y + box.BottomRight.Y + box.BottomLeft.Y) / 4.0f
                );
                var rotated = RotatePoint(centroid.Item1, centroid.Item2, rotationAngle);
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

            // Helper function to rotate point back to axis-aligned
            (float X, float Y) RotatePoint(Vector2 p, float angle)
            {
                float cos = (float)Math.Cos(-angle);
                float sin = (float)Math.Sin(-angle);
                return (p.X * cos - p.Y * sin, p.X * sin + p.Y * cos);
            }

            // Get rotated coordinates for alignment comparison
            var aLeftRotated = RotatePoint(a.TopLeft, rotationAngle);
            var aRightRotated = RotatePoint(a.TopRight, rotationAngle);
            var aCenterRotated = ((aLeftRotated.X + aRightRotated.X) / 2.0f, (aLeftRotated.Y + aRightRotated.Y) / 2.0f);

            var bLeftRotated = RotatePoint(b.TopLeft, rotationAngle);
            var bRightRotated = RotatePoint(b.TopRight, rotationAngle);
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
            static float VerticalOverlap(BoxInfo a, BoxInfo b)
            {
                float overlap = Math.Max(0, Math.Min(a.MaxY, b.MaxY) - Math.Max(a.MinY, b.MinY));
                float minHeight = Math.Min(a.MaxY - a.MinY, b.MaxY - b.MinY);
                return overlap / minHeight;
            }

            static float HorizontalOverlap(List<BoxInfo> column, BoxInfo box)
            {
                float colMinX = column.Min(b => b.MinX);
                float colMaxX = column.Max(b => b.MaxX);

                float overlap = Math.Max(0, Math.Min(colMaxX, box.MaxX) - Math.Max(colMinX, box.MinX));
                float minWidth = Math.Min(colMaxX - colMinX, box.MaxX - box.MinX);
                return minWidth > 0 ? overlap / minWidth : 0f;
            }

            var boxInfos = items.Select(obj =>
            {
                var b = RecognizedTextBoundingBoxHelper.Compute(obj.Lines.Select(x => x.BoundingBox));

                float minX = Math.Min(Math.Min(b.TopLeft.X, b.TopRight.X), Math.Min(b.BottomLeft.X, b.BottomRight.X));
                float maxX = Math.Max(Math.Max(b.TopLeft.X, b.TopRight.X), Math.Max(b.BottomLeft.X, b.BottomRight.X));
                float minY = Math.Min(Math.Min(b.TopLeft.Y, b.TopRight.Y), Math.Min(b.BottomLeft.Y, b.BottomRight.Y));
                float maxY = Math.Max(Math.Max(b.TopLeft.Y, b.TopRight.Y), Math.Max(b.BottomLeft.Y, b.BottomRight.Y));

                return new BoxInfo(obj, minX, minY, maxX, maxY);
            }).ToList();

            var rows = new List<List<BoxInfo>>();
            foreach (var box in boxInfos.OrderBy(b => b.MinY))
            {
                var row = rows.FirstOrDefault(r => r.Any(rBox => VerticalOverlap(rBox, box) > 0.5f));
                if (row == null)
                {
                    row = new List<BoxInfo>();
                    rows.Add(row);
                }
                row.Add(box);
            }

            var orderedBlocks = new List<RecognizedTextBlock>();

            foreach (var row in rows.OrderBy(r => r.Min(b => b.MinY)))
            {
                var columns = new List<List<BoxInfo>>();

                foreach (var box in row.OrderBy(b => b.MinX))
                {
                    var col = columns.FirstOrDefault(c => HorizontalOverlap(c, box) > 0.5f);
                    if (col == null)
                    {
                        col = new List<BoxInfo>();
                        columns.Add(col);
                    }
                    col.Add(box);
                }

                foreach (var col in columns.OrderBy(c => c.Min(b => b.MinX)))
                {
                    orderedBlocks.AddRange(col.OrderBy(b => b.MinY).Select(b => b.Item));
                }
            }

            return orderedBlocks;
        }

        private readonly struct BoxInfo
        {
            public readonly RecognizedTextBlock Item;
            public readonly float MinX;
            public readonly float MinY;
            public readonly float MaxX;
            public readonly float MaxY;

            public BoxInfo(RecognizedTextBlock item, float minX, float minY, float maxX, float maxY)
            {
                Item = item;
                MinX = minX;
                MinY = minY;
                MaxX = maxX;
                MaxY = maxY;
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

        private RecognizedTextPointer _selectionStart = RecognizedTextPointer.Empty;
        private RecognizedTextPointer _selectionEnd = RecognizedTextPointer.Empty;

        public RecognizedTextSelection Selection { get; private set; }

        public void SelectTextBetween(Point startPoint, Point endPoint)
        {
            var start = startPoint.ToVector2();
            var end = endPoint.ToVector2();

            start *= _inverseScale;
            end *= _inverseScale;

            SelectTextBetween(_spatialIndex.FindNearestWord(start), _spatialIndex.FindNearestWord(end));
        }

        private void SelectTextBetween(RecognizedTextPointer selectionStart, RecognizedTextPointer selectionEnd)
        {
            if (_selectionStart == selectionStart && _selectionEnd == selectionEnd)
            {
                return;
            }

            if (selectionStart == RecognizedTextPointer.Empty || selectionEnd == RecognizedTextPointer.Empty)
            {
                ClearSelection();
                return;
            }

            var backwardBlocks = selectionStart.BlockIndex > selectionEnd.BlockIndex;
            var minBlock = Math.Min(selectionStart.BlockIndex, selectionEnd.BlockIndex);
            var maxBlock = Math.Max(selectionStart.BlockIndex, selectionEnd.BlockIndex);

            var lines = new List<IList<RecognizedTextBoundingBox>>();
            var content = new StringBuilder();

            for (int i = minBlock; i <= maxBlock; i++)
            {
                var block = _blocks[i];

                int firstLine = 0;
                int lastLine = block.Lines.Count - 1;

                if (i == selectionStart.BlockIndex)
                {
                    (backwardBlocks ? ref lastLine : ref firstLine) = selectionStart.LineIndex;
                }

                if (i == selectionEnd.BlockIndex)
                {
                    (backwardBlocks ? ref firstLine : ref lastLine) = selectionEnd.LineIndex;
                }

                var backwardLines = backwardBlocks || firstLine > lastLine;
                var minLine = Math.Min(firstLine, lastLine);
                var maxLine = Math.Max(firstLine, lastLine);

                for (int j = minLine; j <= maxLine; j++)
                {
                    var line = block.Lines[j];

                    var firstWord = 0;
                    var lastWord = line.Words.Count - 1;

                    if (i == selectionStart.BlockIndex && j == selectionStart.LineIndex)
                    {
                        (backwardLines ? ref lastWord : ref firstWord) = selectionStart.WordIndex;
                    }

                    if (i == selectionEnd.BlockIndex && j == selectionEnd.LineIndex)
                    {
                        (backwardLines ? ref firstWord : ref lastWord) = selectionEnd.WordIndex;
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

            UpdateSelection(selectionStart, selectionEnd, content.ToString(), lines.Select(y => RecognizedTextBoundingBoxHelper.Compute(y)));
        }

        public void ExpandSelection(Point point, bool singleLine)
        {
            var start = point.ToVector2();
            start *= _inverseScale;

            var nearestWord = _spatialIndex.FindNearestWord(start);
            if (nearestWord == RecognizedTextPointer.Empty)
            {
                ClearSelection();
                return;
            }

            var block = _blocks[nearestWord.BlockIndex];

            if (singleLine)
            {
                var line = block.Lines[nearestWord.LineIndex];

                SelectTextBetween(
                    new RecognizedTextPointer(nearestWord.BlockIndex, nearestWord.LineIndex, 0),
                    new RecognizedTextPointer(nearestWord.BlockIndex, nearestWord.LineIndex, line.Words.Count - 1));
            }
            else
            {
                var line = block.Lines[^1];

                SelectTextBetween(
                    new RecognizedTextPointer(nearestWord.BlockIndex, 0, 0),
                    new RecognizedTextPointer(nearestWord.BlockIndex, block.Lines.Count - 1, line.Words.Count - 1));
            }
        }

        public void SelectAll()
        {
            var selectionStart = new RecognizedTextPointer(0, 0, 0);
            var selectionEnd = new RecognizedTextPointer(_blocks.Count - 1, _blocks[^1].Lines.Count - 1, _blocks[^1].Lines[^1].Words.Count - 1);

            var content = string.Join('\n', _blocks.Select(x => string.Join('\n', x.Lines.Select(x => x.Text))));
            var boundingBoxes = _blocks.SelectMany(x => x.Lines.Select(x => x.BoundingBox));

            UpdateSelection(selectionStart, selectionEnd, content, boundingBoxes);
        }

        public void ClearSelection()
        {
            if (_selectionStart != RecognizedTextPointer.Empty && _selectionEnd != RecognizedTextPointer.Empty)
            {
                _selectionStart = RecognizedTextPointer.Empty;
                _selectionEnd = RecognizedTextPointer.Empty;

                Selection = null;
                SelectionChanged?.Invoke(this, new RecognizedTextSelectionChangedEventArgs(null));
            }
        }

        private void UpdateSelection(RecognizedTextPointer start, RecognizedTextPointer end, string content, IEnumerable<RecognizedTextBoundingBox> boundingBoxes)
        {
            if (_selectionStart != start || _selectionEnd != end)
            {
                _selectionStart = start;
                _selectionEnd = end;

                Selection = new RecognizedTextSelection(content, boundingBoxes.ToList());
                SelectionChanged?.Invoke(this, new RecognizedTextSelectionChangedEventArgs(Selection));
            }
        }
    }
}
