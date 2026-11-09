using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using UnityEngine;

namespace Veerdonk.ProjectileSystem
{
    /// <summary>
    /// Spawns <see cref="Count"/> instances of an explicit child Recipe from OnHit, OnExpire, or OnInterval.
    /// Positions come from <see cref="Pattern"/>. Square, Circle, and RandomCircle keep the parent
    /// travel direction, or the hit normal when the hit is not pierced through, and read <see cref="Spread"/> as meters (0 stacks).
    /// A Target or Basic hit with <see cref="Pierce"/> charges still left spawns children past that body along travel.
    /// <see cref="GridMultiplyPattern"/> places with its own Spacing and aims a cone using
    /// <see cref="Spread"/> as degrees. The Spread modifier scales that instance copy, not Spacing.
    /// On OnInterval the period comes from <see cref="Interval"/> on the Recipe's OnSpawn list.
    /// <see cref="Direction"/> <see cref="MultiplyDirection.Up"/> sends every child along world up instead.
    /// <see cref="LockRange"/> above 0 spreads spawn homing locks over the nearby candidates: child <c>i</c> locks the
    /// <c>i mod m</c>-th nearest of the <c>m</c> in range.
    /// Children spawned by a Target hit start with that target as their Homing fallback.
    /// </summary>
    [Serializable]
    [ModifierStage(RecipeStage.OnInterval | RecipeStage.OnHit | RecipeStage.OnExpire)]
    public sealed class Multiply : IModifier, IDeserializationCallback
    {
        const float SurfaceSkin = 0.01f;

        [SerializeField] int count;
        [SerializeField] RecipeAsset childAsset;
        [SerializeField] float spread;
        [SerializeReference] IMultiplyPattern pattern;
        [SerializeField] MultiplyDirection direction;
        [SerializeField, Min(0f)] float lockRange;

        Recipe childRecipe;

        static readonly List<LockCandidate> LockCandidates = new List<LockCandidate>();

        public Multiply()
        {
            pattern = new SquarePattern();
        }

        public Multiply(int count, Recipe childRecipe, float spread = 0f, IMultiplyPattern pattern = null)
        {
            this.count = count;
            this.childRecipe = childRecipe ?? throw new ArgumentNullException(nameof(childRecipe));
            this.spread = spread;
            this.pattern = pattern ?? new SquarePattern();
        }

        public Multiply(int count, RecipeAsset childAsset, float spread = 0f, IMultiplyPattern pattern = null)
            : this(count, new Recipe(), spread, pattern)
        {
            this.childAsset = childAsset ?? throw new ArgumentNullException(nameof(childAsset));
            childRecipe = null;
        }

        /// <summary>Like the four-argument constructor, plus <see cref="Direction"/> and <see cref="LockRange"/>.</summary>
        public Multiply(
            int count,
            Recipe childRecipe,
            float spread,
            IMultiplyPattern pattern,
            MultiplyDirection direction,
            float lockRange = 0f)
            : this(count, childRecipe, spread, pattern)
        {
            this.direction = direction;
            this.lockRange = lockRange;
        }

        /// <summary>Like the four-argument asset constructor, plus <see cref="Direction"/> and <see cref="LockRange"/>.</summary>
        public Multiply(
            int count,
            RecipeAsset childAsset,
            float spread,
            IMultiplyPattern pattern,
            MultiplyDirection direction,
            float lockRange = 0f)
            : this(count, childAsset, spread, pattern)
        {
            this.direction = direction;
            this.lockRange = lockRange;
        }

        /// <summary>Children to spawn. Positions come from <see cref="Pattern"/>.</summary>
        public int Count
        {
            get => count;
            set => count = value;
        }

        /// <summary>Runtime child used by tests and <see cref="RecipeAsset.ToRecipe"/>.</summary>
        public Recipe ChildRecipe => childRecipe;

        /// <summary>Authored child asset. Resolved by <see cref="RecipeAsset.ToRecipe"/>.</summary>
        public RecipeAsset ChildAsset => childAsset;

        /// <summary>
        /// Square, Circle, and RandomCircle: meters between pips (0 stacks).
        /// <see cref="GridMultiplyPattern"/>: cone half-angle in degrees (0 keeps every child parallel).
        /// Copied onto the instance at spawn so the Spread modifier can scale it. That scale does not change grid Spacing.
        /// </summary>
        public float Spread
        {
            get => spread;
            set => spread = value;
        }

        /// <summary>Layout of child positions. Null authors as <see cref="SquarePattern"/>.</summary>
        public IMultiplyPattern Pattern
        {
            get => pattern;
            set => pattern = value ?? new SquarePattern();
        }

        /// <summary>
        /// Which way the children leave. <see cref="MultiplyDirection.Surface"/> (default) is the hit normal, travel past a
        /// pierced body, or travel without a hit. <see cref="MultiplyDirection.Up"/> is world up in every case.
        /// </summary>
        public MultiplyDirection Direction
        {
            get => direction;
            set => direction = value;
        }

        /// <summary>
        /// Meters around the split origin (hit point, pierce exit, or position) whose candidates the children's spawn homing
        /// locks are spread over: child <c>i</c> locks the <c>i mod m</c>-th nearest of the <c>m</c> in range, and the parent's
        /// hit target only when it is the only one. 0 (default) assigns nothing.
        /// </summary>
        public float LockRange
        {
            get => lockRange;
            set => lockRange = value;
        }

        void IDeserializationCallback.OnDeserialization(object sender)
        {
            if (pattern == null)
                pattern = new SquarePattern();
        }

        /// <inheritdoc />
        public void Apply(ModifierContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            if (Count <= 0)
                return;

            IMultiplyPattern layout = Pattern;
            int n = layout.ResolveCount(Count);
            if (n <= 0)
                return;

            if (layout is RandomCirclePattern randomCircle)
                randomCircle.MinDistance = ChildRecipe.Radius * 2f;

            Vector3 origin;
            Vector3 forward;
            bool onSurface = false;
            if (context.HasHit && ContinuesPastTarget(context))
            {
                forward = ContactTravel(context);
                origin = context.PierceExitSet ? context.PierceExit : context.Hit.Point;
            }
            else if (context.HasHit)
            {
                origin = context.Hit.Point;
                forward = context.Hit.Normal;
                onSurface = true;
            }
            else
            {
                origin = context.Position;
                forward = context.Velocity;
            }

            Vector3 layoutForward = forward;
            Quaternion turn = Quaternion.identity;
            bool flattenIntoSurface = false;
            if (Direction == MultiplyDirection.Up)
            {
                layoutForward = Vector3.up;
                turn = TurnTowardTravel(context.HasHit ? context.ContactVelocity : context.Velocity, context.Velocity);
                flattenIntoSurface = onSurface && forward.sqrMagnitude > 1e-12f;
            }

            Vector3 normal = flattenIntoSurface ? forward.normalized : Vector3.zero;
            bool mirror = !onSurface && Direction != MultiplyDirection.Up;

            HomingTarget hitTarget = context.HasHit && context.Hit.Kind == HitKind.Target
                ? HomingTarget.Assigned(context.Hit.TargetId)
                : default;
            int locks = GatherLocks(context, origin, hitTarget);

            for (int i = 0; i < n; i++)
            {
                Vector3 offset = turn * layout.GetOffset(context, layoutForward, context.Spread, i, n);
                if (mirror)
                    offset = MultiplyPatternPlane.MirrorX(layoutForward, offset);
                if (flattenIntoSurface)
                {
                    float into = Vector3.Dot(offset, normal);
                    if (into < 0f)
                        offset -= normal * into;
                }

                Vector3 spawnPosition = origin + offset;
                if (context.HasHit)
                    spawnPosition = OffsetOffSurface(spawnPosition, forward, ChildRecipe.Radius);
                Vector3 childDirection = turn * layout.GetDirection(context, layoutForward, context.Spread, i, n);
                if (mirror)
                    childDirection = MultiplyPatternPlane.MirrorX(layoutForward, childDirection);
                HomingTarget childLock = locks > 0 ? HomingTarget.Assigned(LockCandidates[i % locks].Id) : default;
                context.RequestMultiplyChild(ChildRecipe, spawnPosition, childDirection, childLock, hitTarget);
            }

            LockCandidates.Clear();
        }

        static Quaternion TurnTowardTravel(Vector3 travel, Vector3 otherwise)
        {
            if (travel.sqrMagnitude <= 1e-12f)
                travel = otherwise;
            var flat = new Vector3(travel.x, 0f, travel.z);
            if (flat.sqrMagnitude <= 1e-8f)
                return Quaternion.identity;
            return Quaternion.LookRotation(flat, Vector3.up);
        }

        int GatherLocks(ModifierContext context, Vector3 origin, HomingTarget hitTarget)
        {
            LockCandidates.Clear();
            if (!(LockRange > 0f))
                return 0;

            float maxSqr = LockRange * LockRange;
            bool hitTargetInRange = false;
            HitTargetCatalog hitTargets = context.HitTargets;
            if (hitTargets != null)
            {
                int hitCount = hitTargets.Count;
                for (int i = 0; i < hitCount; i++)
                {
                    if (hitTargets.TryGet(i, out TargetId id, out Vector3 position))
                        ConsiderLock(context, id, position, origin, maxSqr, hitTarget, ref hitTargetInRange);
                }
            }

            HomingCatalog catalog = context.HomingCatalog;
            int count = catalog.Count;
            for (int i = 0; i < count; i++)
            {
                if (!catalog.TryGet(i, out TargetId id, out Vector3 position))
                    continue;
                if (hitTargets != null && hitTargets.Contains(id))
                    continue;
                ConsiderLock(context, id, position, origin, maxSqr, hitTarget, ref hitTargetInRange);
            }

            if (LockCandidates.Count == 0 && hitTargetInRange)
                LockCandidates.Add(new LockCandidate { Id = hitTarget.Id });

            return LockCandidates.Count;
        }

        static void ConsiderLock(
            ModifierContext context,
            TargetId id,
            Vector3 position,
            Vector3 origin,
            float maxSqr,
            HomingTarget hitTarget,
            ref bool hitTargetInRange)
        {
            if (context.ShooterTarget.IsAssigned && id.Equals(context.ShooterTarget.Id))
                return;

            float sqrDistance = (position - origin).sqrMagnitude;
            if (sqrDistance > maxSqr)
                return;

            if (hitTarget.IsAssigned && id.Equals(hitTarget.Id))
            {
                hitTargetInRange = true;
                return;
            }

            int at = LockCandidates.Count;
            while (at > 0 && LockCandidates[at - 1].SqrDistance > sqrDistance)
                at--;
            LockCandidates.Insert(at, new LockCandidate { Id = id, SqrDistance = sqrDistance });
        }

        struct LockCandidate
        {
            public TargetId Id;
            public float SqrDistance;
        }

        static bool ContinuesPastTarget(ModifierContext context)
        {
            if (context.PierceAtContact <= 0)
                return false;

            HitInfo hit = context.Hit;
            return hit.Kind == HitKind.Target || hit.Protection == ProtectionLevel.Basic;
        }

        static Vector3 ContactTravel(ModifierContext context)
        {
            if (context.ContactVelocity.sqrMagnitude > 1e-12f)
                return context.ContactVelocity;
            if (context.Velocity.sqrMagnitude > 1e-12f)
                return context.Velocity;
            return Vector3.forward;
        }

        static Vector3 OffsetOffSurface(Vector3 point, Vector3 direction, float radius)
        {
            Vector3 along = direction.sqrMagnitude > 1e-12f ? direction.normalized : Vector3.up;
            return point + along * (radius + SurfaceSkin);
        }
    }
}
