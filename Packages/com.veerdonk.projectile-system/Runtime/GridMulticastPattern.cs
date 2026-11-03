using System;
using UnityEngine;

namespace Veerdonk.ProjectileSystem
{
    /// <summary>
    /// Aims <see cref="Multicast"/> instances at the numbered cells of a <see cref="GridMultiplyPattern"/>, usually copied
    /// from a <see cref="MultiplyPatternAsset"/>. Instance <c>i</c> flies toward cell <c>i + 1</c>; instances past the
    /// numbered cells fly along the spawn direction. <see cref="Spread"/> is the cone half-angle in degrees of the farthest
    /// cell in use, as <see cref="GridMultiplyPattern.GetDirection"/> reads it.
    /// </summary>
    [Serializable]
    public sealed class GridMulticastPattern : IMulticastPattern
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

        /// <summary>Cell layout. Its Spacing and numbers set the directions; it places nothing.</summary>
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
            return layout.GetDirection(context, forward, spread, index, used);
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
