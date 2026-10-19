using UnityEngine;

namespace Veerdonk.ProjectileSystem
{
    /// <summary>Step settings for <see cref="IProjectileWorld.PredictPath"/>.</summary>
    public struct PathPrediction
    {
        /// <summary>Fixed step in seconds. Values &lt;= 0 use 1/60 s.</summary>
        public float StepSeconds;
        /// <summary>Safety cap on the number of steps. Values &lt;= 0 use 1024.</summary>
        public int MaxSteps;
    }

    /// <summary>First contact of a path from <see cref="IProjectileWorld.PredictPath"/>.</summary>
    public struct PathHit
    {
        /// <summary>Contact point, as <see cref="HitInfo.Point"/>. Also the last entry of the path.</summary>
        public Vector3 Point;
        /// <summary>Surface normal at the contact.</summary>
        public Vector3 Normal;
        /// <summary><see cref="HitKind.World"/> or <see cref="HitKind.Target"/>, as <see cref="HitInfo.Kind"/>.</summary>
        public HitKind Kind;
        /// <summary>Target that was hit when <see cref="Kind"/> is <see cref="HitKind.Target"/>.</summary>
        public TargetId TargetId;
        /// <summary>Flight time at the end of the step that hits: the tick on which a live shot raises its Hit.</summary>
        public float Time;
    }
}
