namespace Veerdonk.ProjectileSystem
{
    /// <summary>
    /// How a shot is delivered. Not a seventh stage and not a modifier.
    /// <see cref="Straight"/> is a kinematic SphereCast; gravity is not applied.
    /// <see cref="Arc"/> is kinematic with gravity. When <see cref="SpawnRequest.HomingAcquirePointSet"/>
    /// is true, <see cref="IProjectileWorld.Spawn"/> calls <see cref="BallisticAim.TrySolve"/> before OnSpawn.
    /// <see cref="Hitscan"/> is one polyline of <see cref="Recipe.Radius"/> SphereCasts inside
    /// <see cref="IProjectileWorld.Spawn"/>, then the instance returns to the pool. There is no view.
    /// An unbounded Hitscan is 10 km. Homing on Hitscan is the maximum kink in degrees per segment,
    /// not degrees per second. <see cref="SpawnRequest.IgnoreCollider"/> is the same Direct skip.
    /// </summary>
    public enum Delivery
    {
        /// <summary>Kinematic SphereCast. Gravity on the Recipe does not curve the shot.</summary>
        Straight = 0,

        /// <summary>Kinematic SphereCast with gravity. Spawn lofts toward the aim point when it is set.</summary>
        Arc = 1,

        /// <summary>One Radius polyline query inside Spawn, then expire. No flying view.</summary>
        Hitscan = 2
    }
}
