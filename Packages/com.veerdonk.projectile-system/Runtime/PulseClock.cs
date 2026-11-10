using UnityEngine;

namespace Veerdonk.ProjectileSystem
{
    static class PulseClock
    {
        internal const float MinIntervalSeconds = 0.01f;

        internal static float Clamp(float interval)
        {
            if (!(interval > 0f))
                return 0f;
            return interval < MinIntervalSeconds ? MinIntervalSeconds : interval;
        }

        internal static int Crossings(float previousElapsed, float elapsed, float interval)
        {
            interval = Clamp(interval);
            if (interval <= 0f || !(elapsed > previousElapsed))
                return 0;

            int before = Mathf.FloorToInt(previousElapsed / interval);
            int after = Mathf.FloorToInt(elapsed / interval);
            int fires = after - before;
            return fires > 0 ? fires : 0;
        }

        internal static bool IsUsableRadius(float radius)
        {
            return float.IsFinite(radius) && radius > 0f;
        }
    }
}
