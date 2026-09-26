using System;
using System.Collections.Generic;
using UnityEngine;

namespace Veerdonk.ProjectileSystem
{
    static class OccupancyExchange
    {
        public static void Exchange(
            List<TargetId> previous,
            List<HitInfo> overlaps,
            List<TargetId> current,
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
                if (Contains(current, areaHit.TargetId))
                    continue;

                current.Add(areaHit.TargetId);
                if (Contains(previous, areaHit.TargetId))
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
                if (Contains(current, id))
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

        static bool Contains(List<TargetId> ids, TargetId id)
        {
            for (int i = 0; i < ids.Count; i++)
            {
                if (ids[i] == id)
                    return true;
            }

            return false;
        }
    }
}
