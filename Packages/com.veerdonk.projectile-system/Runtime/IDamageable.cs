namespace Veerdonk.ProjectileSystem
{
    /// <summary>
    /// Marks a collider as <see cref="HitKind.Target"/>. The package does not apply HP.
    /// </summary>
    public interface IDamageable
    {
        /// <summary>Id copied onto <see cref="HitInfo.TargetId"/>.</summary>
        TargetId TargetId { get; }
    }
}
