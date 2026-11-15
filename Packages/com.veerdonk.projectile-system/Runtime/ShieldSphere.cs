using System.Collections.Generic;
using UnityEngine;

namespace Veerdonk.ProjectileSystem
{
    /// <summary>What a shot does when it crosses a shield sphere inward. Set per shot on <see cref="SpawnRequest.ShieldResponse"/>.</summary>
    public enum ShieldResponse
    {
        /// <summary>The shot ends at the crossing point. No OnHit, no OnExpire; <see cref="ExpireReason.Blocked"/>.</summary>
        Break,
        /// <summary>The shot's velocity is reflected about the sphere's normal and it flies on outside. Not a hit; spends no Bounce charge.</summary>
        Bounce
    }

    /// <summary>Payload for <see cref="IProjectileWorld.ShieldBlocked"/>.</summary>
    public struct ShieldBlockInfo
    {
        /// <summary>Instance that crossed the shield.</summary>
        public ProjectileId InstanceId;
        /// <summary>Id returned by <see cref="IProjectileWorld.AddShieldSphere"/>.</summary>
        public int ShieldId;
        /// <summary>Crossing point of the shot's centre on the sphere's surface.</summary>
        public Vector3 Position;
        /// <summary>Outward surface normal at <see cref="Position"/>.</summary>
        public Vector3 Normal;
        /// <summary><see cref="HitOutcome.Ended"/> (the shot or Beam / Hitscan segment ends) or <see cref="HitOutcome.Bounced"/>.</summary>
        public HitOutcome Outcome;
    }

    sealed class ShieldSphereSet
    {
        readonly List<Sphere> spheres = new List<Sphere>();
        int nextId = 1;

        public int Count => spheres.Count;

        public int Add(Vector3 center, float radius)
        {
            if (!PulseClock.IsUsableRadius(radius)
                || !float.IsFinite(center.x) || !float.IsFinite(center.y) || !float.IsFinite(center.z))
                return 0;

            int id = nextId++;
            spheres.Add(new Sphere { Id = id, Center = center, Radius = radius });
            return id;
        }

        public bool Remove(int id)
        {
            for (int i = 0; i < spheres.Count; i++)
            {
                if (spheres[i].Id != id)
                    continue;

                spheres.RemoveAt(i);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Earliest inward crossing of the segment <paramref name="from"/> → <paramref name="to"/> over every sphere that
        /// <paramref name="from"/> is not inside. <paramref name="fraction"/> is in [0, 1] along the segment.
        /// </summary>
        public bool TryFindEntry(
            Vector3 from,
            Vector3 to,
            out float fraction,
            out int shieldId,
            out Vector3 point,
            out Vector3 normal)
        {
            fraction = float.PositiveInfinity;
            shieldId = 0;
            point = default;
            normal = default;
            if (spheres.Count == 0)
                return false;

            Vector3 d = to - from;
            float a = Vector3.Dot(d, d);
            if (!(a > 1e-12f))
                return false;

            Sphere best = default;
            for (int i = 0; i < spheres.Count; i++)
            {
                Sphere sphere = spheres[i];
                Vector3 f = from - sphere.Center;
                float c = Vector3.Dot(f, f) - sphere.Radius * sphere.Radius;
                if (c < 0f)
                    continue;

                float b = 2f * Vector3.Dot(f, d);
                if (b >= 0f)
                    continue;

                float discriminant = b * b - 4f * a * c;
                if (!(discriminant > 0f))
                    continue;

                float t = (-b - Mathf.Sqrt(discriminant)) / (2f * a);
                if (t < 0f)
                    t = 0f;
                if (t > 1f || t >= fraction)
                    continue;

                fraction = t;
                best = sphere;
            }

            if (best.Id == 0)
                return false;

            shieldId = best.Id;
            point = from + d * fraction;
            Vector3 outward = point - best.Center;
            normal = outward.sqrMagnitude > 1e-12f ? outward.normalized : -d.normalized;
            return true;
        }

        struct Sphere
        {
            public int Id;
            public Vector3 Center;
            public float Radius;
        }
    }
}
