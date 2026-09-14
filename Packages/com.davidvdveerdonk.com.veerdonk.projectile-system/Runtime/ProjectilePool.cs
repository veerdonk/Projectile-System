using System;
using System.Collections.Generic;
using UnityEngine;

namespace Davidvdveerdonk.ComVeerdonkProjectileSystem
{
    sealed class ProjectilePool
    {
        readonly Transform parent;
        readonly bool createViews;
        readonly Dictionary<string, Stack<ProjectileView>> views = new Dictionary<string, Stack<ProjectileView>>();
        readonly Dictionary<string, int> available = new Dictionary<string, int>();

        public ProjectilePool(Transform parent, bool createViews)
        {
            this.parent = parent;
            this.createViews = createViews;
        }

        public int AvailableCount
        {
            get
            {
                int total = 0;
                foreach (int count in available.Values)
                    total += count;
                return total;
            }
        }

        public void Prewarm(Recipe recipe, int count)
        {
            if (recipe == null)
                throw new ArgumentNullException(nameof(recipe));
            if (string.IsNullOrEmpty(recipe.Id))
                throw new ArgumentException("Recipe.Id is required.", nameof(recipe));
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));

            EnsureBucket(recipe.Id);
            for (int i = 0; i < count; i++)
            {
                if (createViews)
                    views[recipe.Id].Push(CreateView(recipe));
                available[recipe.Id]++;
            }
        }

        public ProjectileView Rent(Recipe recipe)
        {
            if (recipe == null)
                throw new ArgumentNullException(nameof(recipe));
            if (string.IsNullOrEmpty(recipe.Id))
                throw new ArgumentException("Recipe.Id is required.", nameof(recipe));

            EnsureBucket(recipe.Id);

            ProjectileView view = null;
            if (createViews)
            {
                Stack<ProjectileView> stack = views[recipe.Id];
                view = stack.Count > 0 ? stack.Pop() : CreateView(recipe);
            }

            if (available[recipe.Id] > 0)
                available[recipe.Id]--;

            return view;
        }

        public void Return(string recipeId, ProjectileView view)
        {
            if (string.IsNullOrEmpty(recipeId))
                throw new ArgumentException("Recipe id is required.", nameof(recipeId));

            EnsureBucket(recipeId);

            if (createViews && view != null)
            {
                view.Release();
                views[recipeId].Push(view);
            }

            available[recipeId]++;
        }

        void EnsureBucket(string recipeId)
        {
            if (!available.ContainsKey(recipeId))
                available[recipeId] = 0;
            if (createViews && !views.ContainsKey(recipeId))
                views[recipeId] = new Stack<ProjectileView>();
        }

        ProjectileView CreateView(Recipe recipe)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Projectile:" + recipe.Id;
            if (parent != null)
                go.transform.SetParent(parent, false);

            Collider collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(collider);
                else
                    UnityEngine.Object.DestroyImmediate(collider);
            }

            var view = go.AddComponent<ProjectileView>();
            view.Release();
            return view;
        }
    }
}
