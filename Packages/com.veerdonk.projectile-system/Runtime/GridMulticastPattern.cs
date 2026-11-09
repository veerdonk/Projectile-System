using System;
using UnityEngine;

namespace Veerdonk.ProjectileSystem
{
    /// <summary>
    /// Lays <see cref="Multicast"/> instances out on the numbered cells of a <see cref="GridMultiplyPattern"/>, usually
    /// copied from a <see cref="MultiplyPatternAsset"/>. Instance <c>i</c> starts at cell <c>i + 1</c>, offset from the
    /// shot's position in the plane across its direction (the grid's center is the shot's position, and
    /// <see cref="GridMultiplyPattern.Spacing"/> is meters between lanes), and flies toward that cell. Instances past the
    /// numbered cells start at the shot's position and fly along the spawn direction. <see cref="Spread"/> is the cone
    /// half-angle in degrees of the farthest cell in use, as <see cref="GridMultiplyPattern.GetDirection"/> reads it; 0
    /// gives parallel lanes. A column drawn on the right of the grid lands on the right of a shooter looking along the
    /// shot's direction, and the top row lands above.
    /// </summary>
    [Serializable]
    public sealed class GridMulticastPattern : IMulticastOffsetPattern
    {
        /// <summary>Spread a new pattern starts with, in degrees. Matches <see cref="RandomConePattern.DefaultHalfAngleDegrees"/>.</summary>
        public const float DefaultSpreadDegrees = RandomConePattern.DefaultHalfAngleDegrees;

        [SerializeField] GridMultiplyPattern grid = new GridMultiplyPattern();
        [SerializeField] float spread = DefaultSpreadDegrees;

        /// <summary>Empty 3×3 grid (every instance flies straight) and <see cref="DefaultSpreadDegrees"/>.</summary>
        public GridMulticastPattern()
        {
        }

        /// <summary>Copies <paramref name="grid"/>; later changes to it do not reach this pattern.</summary>
        public GridMulticastPattern(GridMultiplyPattern grid, float spread = DefaultSpreadDegrees)
        {
            if (grid == null)
                throw new ArgumentNullException(nameof(grid));
            this.grid = grid.Clone();
            this.spread = spread;
        }

        /// <summary>Cell layout. Its Spacing and numbers set the start offsets and the directions.</summary>
        public GridMultiplyPattern Grid
        {
            get
            {
                if (grid == null)
                    grid = new GridMultiplyPattern();
                return grid;
            }
        }

        /// <summary>Cone half-angle in degrees of the farthest cell in use. <c>&lt;= 0</c> keeps every instance parallel.</summary>
        public float Spread
        {
            get => spread;
            set => spread = value;
        }

        /// <inheritdoc cref="GridMultiplyPattern.SourceName"/>
        public string SourceName => Grid.SourceName;

        /// <inheritdoc />
        public Vector3 GetDirection(ModifierContext context, Vector3 forward, int index, int count)
        {
            GridMultiplyPattern layout = Grid;
            int used = layout.ResolveCount(count);
            if (index < 0 || index >= used)
                return forward;
            return MultiplyPatternPlane.MirrorX(forward, layout.GetDirection(context, forward, spread, index, used));
        }

        /// <inheritdoc />
        public Vector3 GetOffset(ModifierContext context, Vector3 forward, int index, int count)
        {
            GridMultiplyPattern layout = Grid;
            int used = layout.ResolveCount(count);
            if (index < 0 || index >= used)
                return Vector3.zero;
            return MultiplyPatternPlane.MirrorX(forward, layout.GetOffset(context, forward, spread, index, used));
        }

        /// <summary>Field copy. Not the same instance, and its grid is not shared.</summary>
        public GridMulticastPattern Clone()
        {
            return new GridMulticastPattern(Grid, spread);
        }

        /// <summary>Same grid size, spacing, and cell numbers. Ignores <see cref="Spread"/> and <see cref="SourceName"/>.</summary>
        public bool HasSameLayout(GridMulticastPattern other)
        {
            return other != null && Grid.HasSameLayout(other.Grid);
        }
    }
}
