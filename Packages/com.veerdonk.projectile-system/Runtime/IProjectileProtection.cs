namespace Veerdonk.ProjectileSystem
{
    /// <summary>
    /// Marks a collider with <see cref="ProtectionLevel.Basic"/> or <see cref="ProtectionLevel.Strong"/>.
    /// The package classifies the hit; it does not subtract HP.
    /// </summary>
    public interface IProjectileProtection
    {
        /// <summary>Basic or Strong. None is the unmarked default, not a marker value to implement.</summary>
        ProtectionLevel Level { get; }
    }
}
