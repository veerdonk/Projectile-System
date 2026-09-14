using System;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace Davidvdveerdonk.ComVeerdonkProjectileSystem
{
    sealed class ProjectilePool : IDisposable
    {
        readonly World world;
        readonly Transform parent;
        readonly bool createViews;
        readonly EntityArchetype pooledArchetype;
        readonly Dictionary<int, Stack<Entity>> free = new Dictionary<int, Stack<Entity>>();
        readonly Dictionary<int, int> available = new Dictionary<int, int>();
        readonly Dictionary<int, Stack<ProjectileView>> viewStacks = new Dictionary<int, Stack<ProjectileView>>();
        readonly Dictionary<Entity, ProjectileView> liveViews = new Dictionary<Entity, ProjectileView>();
        readonly List<ProjectileView> allViews = new List<ProjectileView>();

        public ProjectilePool(World world, Transform parent, bool createViews)
        {
            this.world = world ?? throw new ArgumentNullException(nameof(world));
            this.parent = parent;
            this.createViews = createViews;
            pooledArchetype = world.EntityManager.CreateArchetype(
                typeof(Projectile),
                typeof(ProjectilePosition),
                typeof(ProjectileVelocity),
                typeof(ProjectileCandidate),
                typeof(Disabled));
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

        public void Prewarm(int recipeIndex, Recipe recipe, int count)
        {
            if (recipe == null)
                throw new ArgumentNullException(nameof(recipe));
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));

            EnsureBucket(recipeIndex);
            EntityManager em = world.EntityManager;
            for (int i = 0; i < count; i++)
            {
                free[recipeIndex].Push(em.CreateEntity(pooledArchetype));
                available[recipeIndex]++;
                if (createViews)
                    viewStacks[recipeIndex].Push(CreateView(recipe));
            }
        }

        public Entity Rent(int recipeIndex, Recipe recipe, out ProjectileView view)
        {
            if (recipe == null)
                throw new ArgumentNullException(nameof(recipe));

            EnsureBucket(recipeIndex);
            EntityManager em = world.EntityManager;
            Entity entity = free[recipeIndex].Count > 0
                ? free[recipeIndex].Pop()
                : em.CreateEntity(pooledArchetype);

            em.SetEnabled(entity, true);

            view = null;
            if (createViews)
            {
                Stack<ProjectileView> stack = viewStacks[recipeIndex];
                view = stack.Count > 0 ? stack.Pop() : CreateView(recipe);
                liveViews[entity] = view;
            }

            if (available[recipeIndex] > 0)
                available[recipeIndex]--;

            return entity;
        }

        public void Return(int recipeIndex, Entity entity)
        {
            EntityManager em = world.EntityManager;
            em.SetEnabled(entity, false);
            EnsureBucket(recipeIndex);
            free[recipeIndex].Push(entity);
            available[recipeIndex]++;

            if (liveViews.TryGetValue(entity, out ProjectileView view))
            {
                liveViews.Remove(entity);
                view.Release();
                viewStacks[recipeIndex].Push(view);
            }
        }

        public bool TryGetView(Entity entity, out ProjectileView view)
        {
            return liveViews.TryGetValue(entity, out view);
        }

        public void Dispose()
        {
            for (int i = 0; i < allViews.Count; i++)
            {
                ProjectileView view = allViews[i];
                if (view == null)
                    continue;
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(view.gameObject);
                else
                    UnityEngine.Object.DestroyImmediate(view.gameObject);
            }

            allViews.Clear();
            liveViews.Clear();
            viewStacks.Clear();
            free.Clear();
            available.Clear();
        }

        void EnsureBucket(int recipeIndex)
        {
            if (!available.ContainsKey(recipeIndex))
                available[recipeIndex] = 0;
            if (!free.ContainsKey(recipeIndex))
                free[recipeIndex] = new Stack<Entity>();
            if (createViews && !viewStacks.ContainsKey(recipeIndex))
                viewStacks[recipeIndex] = new Stack<ProjectileView>();
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
            allViews.Add(view);
            return view;
        }
    }
}
