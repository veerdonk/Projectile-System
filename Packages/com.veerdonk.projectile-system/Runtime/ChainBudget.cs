namespace Veerdonk.ProjectileSystem
{
    /// <summary>
    /// How many more times a spawning modifier may expand, this instance included. An instance that came from such a
    /// modifier inherits its parent's count minus one; anything else starts fresh from the Recipe. Counting down is
    /// what makes a Recipe that spawns itself terminate.
    /// </summary>
    struct ChainBudget
    {
        public bool IsInherited;
        public int Left;

        public static ChainBudget Inherit(int left)
        {
            return new ChainBudget { IsInherited = true, Left = left };
        }

        public static int Start(int recipeGenerations)
        {
            return recipeGenerations > 0 ? 1 + recipeGenerations : 1;
        }

        public int Resolve(int recipeGenerations)
        {
            return IsInherited ? Left : Start(recipeGenerations);
        }
    }
}
