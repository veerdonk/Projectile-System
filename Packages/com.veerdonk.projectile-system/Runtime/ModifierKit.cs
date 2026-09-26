using System;
using UnityEngine;

namespace Veerdonk.ProjectileSystem
{
    /// <summary>
    /// One stage list and the modifiers to append there. <see cref="Stage"/> must be exactly one of the six
    /// lists. A combination kit is several layers, not a flags combination.
    /// </summary>
    [Serializable]
    public struct ModifierKitLayer
    {
        [SerializeField] RecipeStage stage;
        [SerializeReference] IModifier[] modifiers;

        /// <summary>One list and the modifiers that belong on it.</summary>
        public ModifierKitLayer(RecipeStage stage, params IModifier[] modifiers)
        {
            this.stage = stage;
            this.modifiers = modifiers;
        }

        /// <summary>Exactly one of the six <see cref="RecipeStage"/> lists.</summary>
        public RecipeStage Stage
        {
            get => stage;
            set => stage = value;
        }

        /// <summary>Modifiers appended to <see cref="Stage"/>, in order. Empty is a no-op.</summary>
        public IModifier[] Modifiers
        {
            get => modifiers;
            set => modifiers = value;
        }
    }

    /// <summary>
    /// Inspector-authored extras appended onto a cloned base <see cref="Recipe"/>. No speed, lifetime, or presentation.
    /// <see cref="ApplyTo"/> does not change <see cref="Recipe.Id"/>. An <see cref="RecipeStage.OnInterval"/> layer
    /// does nothing unless the base, or an <see cref="RecipeStage.OnSpawn"/> layer, already has <see cref="Interval"/>.
    /// This asset does not insert one.
    /// </summary>
    [CreateAssetMenu(fileName = "ModifierKit", menuName = "Projectile/Modifier Kit")]
    public sealed class ModifierKit : ScriptableObject
    {
        [SerializeField] ModifierKitLayer[] layers = Array.Empty<ModifierKitLayer>();

        /// <summary>Layers applied in order. A null or empty array is a no-op.</summary>
        public ModifierKitLayer[] Layers
        {
            get => layers ?? Array.Empty<ModifierKitLayer>();
            set => layers = value ?? Array.Empty<ModifierKitLayer>();
        }

        /// <summary>
        /// Clones each layer's modifiers onto <paramref name="recipe"/>, in layer order then list order.
        /// Does not change <see cref="Recipe.Id"/>. A null recipe throws. An empty layer is skipped.
        /// </summary>
        public void ApplyTo(Recipe recipe)
        {
            if (recipe == null)
                throw new ArgumentNullException(nameof(recipe));
            if (layers == null)
                return;

            for (int i = 0; i < layers.Length; i++)
            {
                IModifier[] modifiers = layers[i].Modifiers;
                if (modifiers == null || modifiers.Length == 0)
                    continue;

                recipe.Append(layers[i].Stage, RecipeAsset.CloneModifierList(modifiers));
            }
        }
    }
}
