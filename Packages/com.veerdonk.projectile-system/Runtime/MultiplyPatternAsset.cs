using UnityEngine;

namespace Veerdonk.ProjectileSystem
{
    /// <summary>
    /// Authored square grid for <see cref="Multiply"/> (<c>Create &gt; Projectile &gt; Multiply Pattern</c>).
    /// The Pattern dropdown copies <see cref="Spacing"/> and the cell numbers onto a <see cref="GridMultiplyPattern"/>.
    /// The live Recipe does not keep this asset.
    /// </summary>
    [CreateAssetMenu(fileName = "MultiplyPattern", menuName = "Projectile/Multiply Pattern")]
    public sealed class MultiplyPatternAsset : ScriptableObject
    {
        [SerializeField] GridMultiplyPattern pattern = new GridMultiplyPattern();

        /// <summary>N of the N×N grid. Default 3, minimum 2.</summary>
        public int Size => Pattern.Size;

        /// <inheritdoc cref="GridMultiplyPattern.Spacing"/>
        public float Spacing
        {
            get => Pattern.Spacing;
            set => Pattern.Spacing = value;
        }

        /// <inheritdoc cref="GridMultiplyPattern.Cell"/>
        public int Cell(int row, int column)
        {
            return Pattern.Cell(row, column);
        }

        /// <inheritdoc cref="GridMultiplyPattern.LeftClick"/>
        public void LeftClick(int row, int column)
        {
            Pattern.LeftClick(row, column);
        }

        /// <inheritdoc cref="GridMultiplyPattern.RightClick"/>
        public void RightClick(int row, int column)
        {
            Pattern.RightClick(row, column);
        }

        /// <inheritdoc cref="GridMultiplyPattern.Grow"/>
        public void Grow()
        {
            Pattern.Grow();
        }

        /// <inheritdoc cref="GridMultiplyPattern.Shrink"/>
        public void Shrink()
        {
            Pattern.Shrink();
        }

        /// <inheritdoc cref="GridMultiplyPattern.AssignLayout"/>
        public void AssignLayout(int size, float spacing, int[] rowMajor)
        {
            Pattern.AssignLayout(size, spacing, rowMajor);
        }

        /// <summary>Field copy named after this asset. Not an alias of the asset.</summary>
        public GridMultiplyPattern ToPattern()
        {
            GridMultiplyPattern copy = Pattern.Clone();
            copy.SetSourceName(name);
            return copy;
        }

        GridMultiplyPattern Pattern
        {
            get
            {
                if (pattern == null)
                    pattern = new GridMultiplyPattern();
                return pattern;
            }
        }

        void OnEnable()
        {
            if (pattern == null)
                pattern = new GridMultiplyPattern();
        }
    }
}
