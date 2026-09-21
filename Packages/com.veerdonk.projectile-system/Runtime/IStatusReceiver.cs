namespace Veerdonk.ProjectileSystem
{
    /// <summary>
    /// Game/sandbox contract for applying a status. The package never calls this;
    /// listeners map <see cref="IProjectileWorld.StatusApplied"/> to <see cref="Apply"/>.
    /// </summary>
    public interface IStatusReceiver
    {
        /// <summary>Game applies <paramref name="effect"/>. The package never calls this.</summary>
        void Apply(IStatusEffect effect);
    }
}
