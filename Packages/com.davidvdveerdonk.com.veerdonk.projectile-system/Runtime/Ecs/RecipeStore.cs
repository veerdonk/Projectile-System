using System;
using System.Collections.Generic;

namespace Davidvdveerdonk.ComVeerdonkProjectileSystem
{
    sealed class RecipeStore
    {
        readonly List<Recipe> recipes = new List<Recipe>();
        readonly Dictionary<string, int> idToIndex = new Dictionary<string, int>();

        public int GetOrAdd(Recipe recipe)
        {
            if (recipe == null)
                throw new ArgumentNullException(nameof(recipe));
            if (string.IsNullOrEmpty(recipe.Id))
                throw new ArgumentException("Recipe.Id is required.", nameof(recipe));

            if (idToIndex.TryGetValue(recipe.Id, out int existing))
            {
                recipes[existing] = recipe;
                return existing;
            }

            int index = recipes.Count;
            recipes.Add(recipe);
            idToIndex.Add(recipe.Id, index);
            return index;
        }

        public Recipe Get(int index)
        {
            return recipes[index];
        }
    }
}
