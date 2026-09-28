namespace Veerdonk.ProjectileSystem
{
    /// <summary>
    /// How a shot is delivered. Not a seventh stage and not a modifier.
    /// <see cref="Straight"/> is a kinematic SphereCast; gravity is not applied.
    /// <see cref="Arc"/> is kinematic with gravity. When <see cref="SpawnRequest.HomingAcquirePointSet"/>
    /// is true, <see cref="IProjectileWorld.Spawn"/> calls <see cref="BallisticAim.TrySolve"/> before OnSpawn.
    /// <see cref="Hitscan"/> is one polyline of <see cref="Recipe.Radius"/> SphereCasts inside
    /// <see cref="IProjectileWorld.Spawn"/>, then the instance returns to the pool. There is no view.
    /// An unbounded Hitscan or Beam pulse is 10 km. Homing on Hitscan is the maximum kink
    /// in degrees per segment. Homing on Beam turns at degrees per second along the pulse
    /// (distance / speed). <see cref="SpawnRequest.IgnoreCollider"/> is the same Direct skip.
    /// <see cref="Beam"/> stays live and repeats that polyline until <see cref="Recipe.LifetimeSeconds"/> or
    /// <see cref="IProjectileWorld.Despawn"/>.
    /// </summary>
    public enum Delivery
    {
        /// <summary>Kinematic SphereCast. Gravity on the Recipe does not curve the shot.</summary>
        Straight = 0,

        /// <summary>Kinematic SphereCast with gravity. Spawn lofts toward the aim point when it is set.</summary>
        Arc = 1,

        /// <summary>One Radius polyline query inside Spawn, then expire. No flying view.</summary>
        Hitscan = 2,

        /// <summary>
        /// Live instance at the spawn pose. Each pulse is one Hitscan polyline.
        /// The first pulse runs inside <see cref="IProjectileWorld.Spawn"/>. Later pulses follow
        /// <see cref="Recipe.BeamPulseSeconds"/>. Pierce does not despawn the instance.
        /// </summary>
        Beam = 3
    }
}
