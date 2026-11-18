using System;
using System.Collections.Generic;
using UnityEngine;

namespace Veerdonk.ProjectileSystem
{
    static class OccupancyExchange
    {
        public static void Exchange(
            TargetSet previous,
            List<HitInfo> overlaps,
            TargetSet current,
            ProjectileId instanceId,
            float damage,
            float criticalChance,
            float effectRadius,
            IStatusEffect status,
            Action<HitInfo> onHit,
            Action<StatusInfo> onStatus)
        {
            current.Clear();
            for (int i = 0; i < overlaps.Count; i++)
            {
                HitInfo areaHit = overlaps[i];
                if (areaHit.Kind != HitKind.Target || areaHit.Protection == ProtectionLevel.Strong)
                    continue;
                if (!current.Add(areaHit.TargetId))
                    continue;

                if (previous.Contains(areaHit.TargetId))
                    continue;

                areaHit.InstanceId = instanceId;
                areaHit.Source = HitSource.OccupancyEnter;
                areaHit.Damage = damage;
                areaHit.CriticalChance = criticalChance;
                areaHit.EffectRadius = effectRadius;
                onHit?.Invoke(areaHit);
                if (status == null)
                    continue;

                onStatus?.Invoke(new StatusInfo
                {
                    InstanceId = instanceId,
                    TargetId = areaHit.TargetId,
                    Effect = status
                });
            }

            for (int i = 0; i < previous.Count; i++)
            {
                TargetId id = previous[i];
                if (current.Contains(id))
                    continue;

                onHit?.Invoke(new HitInfo
                {
                    InstanceId = instanceId,
                    Kind = HitKind.Target,
                    Source = HitSource.OccupancyExit,
                    TargetId = id,
                    Damage = 0f,
                    CriticalChance = criticalChance,
                    EffectRadius = effectRadius
                });
            }

            previous.Clear();
            for (int i = 0; i < current.Count; i++)
                previous.Add(current[i]);
        }
    }

    // Ids in insertion order without duplicates; membership is a hash lookup instead of a scan of the order.
    sealed class TargetSet
    {
        readonly List<TargetId> order = new List<TargetId>();
        readonly HashSet<TargetId> members = new HashSet<TargetId>();

        public int Count => order.Count;

        public TargetId this[int index] => order[index];

        public bool Contains(TargetId id)
        {
            return members.Contains(id);
        }

        public bool Add(TargetId id)
        {
            if (!members.Add(id))
                return false;

            order.Add(id);
            return true;
        }

        public void Clear()
        {
            order.Clear();
            members.Clear();
        }
    }
}
