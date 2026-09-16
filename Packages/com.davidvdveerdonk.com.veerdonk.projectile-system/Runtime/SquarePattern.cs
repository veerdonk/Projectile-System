using UnityEngine;

namespace Davidvdveerdonk.ComVeerdonkProjectileSystem
{
    /// <summary>
    /// Dice-pip layouts through 9, then odd-sized grids (5×5, 7×7, …). Cell size is <c>spread</c>.
    /// </summary>
    public sealed class SquarePattern : IMultiplyPattern
    {
        public Vector3 GetOffset(ModifierContext context, Vector3 forward, float spread, int index, int count)
        {
            if (spread <= 0f || count <= 0 || index < 0 || index >= count)
                return Vector3.zero;

            return MultiplyPatternPlane.FromLocal(forward, Local(index, count, spread));
        }

        static Vector2 Local(int index, int count, float s)
        {
            if (count <= 9)
                return Named(index, count, s);

            if (index < 9)
                return Named(index, 9, s);

            int extra = index - 9;
            for (int r = 2; ; r++)
            {
                int ringCount = 8 * r;
                if (extra < ringCount)
                    return RingCell(r, extra) * s;
                extra -= ringCount;
            }
        }

        static Vector2 Named(int index, int count, float s)
        {
            switch (count)
            {
                case 1:
                    return Vector2.zero;
                case 2:
                    return index == 0 ? new Vector2(-0.5f * s, 0f) : new Vector2(0.5f * s, 0f);
                case 3:
                    if (index == 0)
                        return new Vector2(-s, 0f);
                    if (index == 1)
                        return Vector2.zero;
                    return new Vector2(s, 0f);
                case 4:
                    return Corner(index, s);
                case 5:
                    return index < 4 ? Corner(index, s) : Vector2.zero;
                case 6:
                    return Die6(index, s);
                case 7:
                    return index < 6 ? Die6(index, s) : Vector2.zero;
                case 8:
                    if (index < 6)
                        return Die6(index, s);
                    if (index == 6)
                        return Vector2.zero;
                    return new Vector2(0f, s);
                default:
                    if (index < 6)
                        return Die6(index, s);
                    if (index == 6)
                        return Vector2.zero;
                    if (index == 7)
                        return new Vector2(0f, s);
                    return new Vector2(0f, -s);
            }
        }

        static Vector2 Corner(int index, float s)
        {
            switch (index)
            {
                case 0:
                    return new Vector2(-s, s);
                case 1:
                    return new Vector2(s, s);
                case 2:
                    return new Vector2(-s, -s);
                default:
                    return new Vector2(s, -s);
            }
        }

        static Vector2 Die6(int index, float s)
        {
            switch (index)
            {
                case 0:
                    return new Vector2(-s, s);
                case 1:
                    return new Vector2(-s, 0f);
                case 2:
                    return new Vector2(-s, -s);
                case 3:
                    return new Vector2(s, s);
                case 4:
                    return new Vector2(s, 0f);
                default:
                    return new Vector2(s, -s);
            }
        }

        static Vector2 RingCell(int r, int extra)
        {
            if (extra == 0)
                return new Vector2(0f, r);
            if (extra == 1)
                return new Vector2(0f, -r);
            if (extra == 2)
                return new Vector2(r, 0f);
            if (extra == 3)
                return new Vector2(-r, 0f);

            extra -= 4;
            int edgeSlots = 8 * (r - 1);
            if (extra < edgeSlots)
            {
                int k = extra / 8 + 1;
                switch (extra % 8)
                {
                    case 0:
                        return new Vector2(k, r);
                    case 1:
                        return new Vector2(-k, r);
                    case 2:
                        return new Vector2(k, -r);
                    case 3:
                        return new Vector2(-k, -r);
                    case 4:
                        return new Vector2(r, k);
                    case 5:
                        return new Vector2(r, -k);
                    case 6:
                        return new Vector2(-r, k);
                    default:
                        return new Vector2(-r, -k);
                }
            }

            extra -= edgeSlots;
            switch (extra)
            {
                case 0:
                    return new Vector2(-r, r);
                case 1:
                    return new Vector2(r, r);
                case 2:
                    return new Vector2(-r, -r);
                default:
                    return new Vector2(r, -r);
            }
        }
    }
}
