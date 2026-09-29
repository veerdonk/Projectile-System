using System;
using System.Runtime.Serialization;
using UnityEngine;

namespace Veerdonk.ProjectileSystem
{
    /// <summary>
    /// Serializable copy of a <see cref="MultiplyPatternAsset"/>.
    /// Numbered cells spawn in order from the geometric center of the square.
    /// <see cref="Spacing"/> is meters between adjacent cell centers.
    /// <see cref="Multiply.Spread"/> is the cone half-angle in degrees of the farthest numbered cell.
    /// </summary>
    [Serializable]
    public sealed class GridMultiplyPattern : IMultiplyPattern, IDeserializationCallback
    {
        public const int DefaultSize = 3;
        public const int MinSize = 2;
        public const float DefaultSpacing = 2f;

        [SerializeField] int size = DefaultSize;
        [SerializeField] float spacing = DefaultSpacing;
        [SerializeField] string sourceName;
        [SerializeField] int[] cells;

        /// <summary>N of the N×N grid. Minimum <see cref="MinSize"/>.</summary>
        public int Size
        {
            get
            {
                EnsureCells();
                return size;
            }
        }

        /// <summary>Meters between adjacent cell centers. <c>&lt;= 0</c> stacks every pip at the origin.</summary>
        public float Spacing
        {
            get => spacing;
            set => spacing = value;
        }

        /// <summary>Asset name copied when a <see cref="MultiplyPatternAsset"/> was picked. Empty when authored in code.</summary>
        public string SourceName => sourceName;

        /// <summary>Cells whose number is greater than 0.</summary>
        public int NumberedCount
        {
            get
            {
                EnsureCells();
                int count = 0;
                for (int i = 0; i < cells.Length; i++)
                {
                    if (cells[i] > 0)
                        count++;
                }

                return count;
            }
        }

        /// <summary>Pip number at <paramref name="row"/>, <paramref name="column"/>. 0 is empty. Row 0 is the top (+Y).</summary>
        public int Cell(int row, int column)
        {
            EnsureCells();
            if (!InBounds(row, column))
                return 0;
            return cells[Index(row, column)];
        }

        /// <summary>
        /// Replaces the grid. <paramref name="rowMajor"/> is row 0 at the top, left to right.
        /// Numbers compact to <c>1..k</c> with no gaps.
        /// </summary>
        public void AssignLayout(int newSize, float newSpacing, int[] rowMajor)
        {
            size = newSize < MinSize ? MinSize : newSize;
            spacing = newSpacing;
            cells = new int[size * size];
            if (rowMajor != null)
            {
                int copy = rowMajor.Length < cells.Length ? rowMajor.Length : cells.Length;
                Array.Copy(rowMajor, cells, copy);
            }

            Compact();
        }

        /// <summary>Empty cell takes the next integer. An occupied cell is left alone.</summary>
        public void LeftClick(int row, int column)
        {
            EnsureCells();
            if (!InBounds(row, column))
                return;

            int index = Index(row, column);
            if (cells[index] > 0)
                return;

            int max = 0;
            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i] > max)
                    max = cells[i];
            }

            cells[index] = max + 1;
        }

        /// <summary>Clears that cell and renumbers the rest to <c>1..k</c>.</summary>
        public void RightClick(int row, int column)
        {
            EnsureCells();
            if (!InBounds(row, column))
                return;

            int index = Index(row, column);
            if (cells[index] <= 0)
                return;

            cells[index] = 0;
            Compact();
        }

        /// <summary>Grows N to N+1. The new row is the bottom and the new column is the right. Existing numbers stay.</summary>
        public void Grow()
        {
            EnsureCells();
            int nextSize = size + 1;
            var next = new int[nextSize * nextSize];
            for (int row = 0; row < size; row++)
            {
                for (int column = 0; column < size; column++)
                    next[row * nextSize + column] = cells[Index(row, column)];
            }

            size = nextSize;
            cells = next;
        }

        /// <summary>Shrinks N to N−1, not below <see cref="MinSize"/>. Drops the last row and column, then compact-renumbers.</summary>
        public void Shrink()
        {
            EnsureCells();
            if (size <= MinSize)
                return;

            int nextSize = size - 1;
            var next = new int[nextSize * nextSize];
            for (int row = 0; row < nextSize; row++)
            {
                for (int column = 0; column < nextSize; column++)
                    next[row * nextSize + column] = cells[Index(row, column)];
            }

            size = nextSize;
            cells = next;
            Compact();
        }

        /// <summary>Field copy. Not the same instance, and not a <see cref="MultiplyPatternAsset"/>.</summary>
        public GridMultiplyPattern Clone()
        {
            EnsureCells();
            var copy = new GridMultiplyPattern
            {
                size = size,
                spacing = spacing,
                sourceName = sourceName,
                cells = (int[])cells.Clone()
            };
            return copy;
        }

        /// <summary>Same size, spacing, and cell numbers. Ignores <see cref="SourceName"/>.</summary>
        public bool HasSameLayout(GridMultiplyPattern other)
        {
            if (other == null)
                return false;

            EnsureCells();
            other.EnsureCells();
            if (size != other.size || !Mathf.Approximately(spacing, other.spacing) || cells.Length != other.cells.Length)
                return false;

            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i] != other.cells[i])
                    return false;
            }

            return true;
        }

        /// <inheritdoc />
        public Vector3 GetOffset(ModifierContext context, Vector3 forward, float spread, int index, int count)
        {
            if (!TryLocal(index + 1, count, out Vector2 local))
                return Vector3.zero;
            return MultiplyPatternPlane.FromLocal(forward, local);
        }

        /// <inheritdoc />
        public Vector3 GetDirection(ModifierContext context, Vector3 forward, float spread, int index, int count)
        {
            if (spread <= 0f)
                return forward;
            if (!TryLocal(index + 1, count, out Vector2 local) || local.sqrMagnitude <= 1e-12f)
                return forward;

            float maxSqr = MaxLocalSqr(count);
            if (maxSqr <= 1e-12f)
                return forward;

            float degrees = spread * Mathf.Sqrt(local.sqrMagnitude / maxSqr);
            Vector3 forwardN = forward.sqrMagnitude < 1e-12f ? Vector3.forward : forward.normalized;
            Vector3 lateral = MultiplyPatternPlane.FromLocal(forwardN, local);
            if (lateral.sqrMagnitude < 1e-12f)
                return forward;

            lateral.Normalize();
            float rad = degrees * Mathf.Deg2Rad;
            return forwardN * Mathf.Cos(rad) + lateral * Mathf.Sin(rad);
        }

        /// <inheritdoc />
        public int ResolveCount(int requested)
        {
            if (requested <= 0)
                return 0;

            int numbered = NumberedCount;
            return requested < numbered ? requested : numbered;
        }

        internal void SetSourceName(string value)
        {
            sourceName = value;
        }

        void IDeserializationCallback.OnDeserialization(object sender)
        {
            EnsureCells();
        }

        bool TryLocal(int number, int usedCount, out Vector2 local)
        {
            local = default;
            if (number <= 0 || number > usedCount || spacing <= 0f)
                return false;

            EnsureCells();
            for (int row = 0; row < size; row++)
            {
                for (int column = 0; column < size; column++)
                {
                    if (cells[Index(row, column)] != number)
                        continue;

                    float center = (size - 1) * 0.5f;
                    local = new Vector2((column - center) * spacing, (center - row) * spacing);
                    return true;
                }
            }

            return false;
        }

        float MaxLocalSqr(int usedCount)
        {
            float max = 0f;
            for (int number = 1; number <= usedCount; number++)
            {
                if (!TryLocal(number, usedCount, out Vector2 local))
                    continue;
                float sqr = local.sqrMagnitude;
                if (sqr > max)
                    max = sqr;
            }

            return max;
        }

        void Compact()
        {
            EnsureCells();
            int max = 0;
            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i] > max)
                    max = cells[i];
            }

            if (max == 0)
                return;

            var map = new int[max + 1];
            int next = 1;
            for (int number = 1; number <= max; number++)
            {
                for (int i = 0; i < cells.Length; i++)
                {
                    if (cells[i] != number)
                        continue;
                    map[number] = next++;
                    break;
                }
            }

            for (int i = 0; i < cells.Length; i++)
            {
                int number = cells[i];
                cells[i] = number > 0 && number <= max ? map[number] : 0;
            }
        }

        void EnsureCells()
        {
            if (size < MinSize)
                size = MinSize;

            int needed = size * size;
            if (cells != null && cells.Length == needed)
                return;

            var next = new int[needed];
            if (cells != null)
            {
                int copy = cells.Length < needed ? cells.Length : needed;
                Array.Copy(cells, next, copy);
            }

            cells = next;
        }

        int Index(int row, int column)
        {
            return row * size + column;
        }

        bool InBounds(int row, int column)
        {
            return row >= 0 && column >= 0 && row < size && column < size;
        }
    }
}
