namespace Veerdonk.ProjectileSystem
{
    /// <summary>Which way <see cref="Multiply"/> children leave.</summary>
    public enum MultiplyDirection
    {
        /// <summary>The hit normal on a hit that is not pierced, travel past a pierced body, travel on OnExpire and OnInterval.</summary>
        Surface,

        /// <summary>World up in every case. The pattern lies in the horizontal plane, its top row along the parent's horizontal travel.</summary>
        Up
    }
}
