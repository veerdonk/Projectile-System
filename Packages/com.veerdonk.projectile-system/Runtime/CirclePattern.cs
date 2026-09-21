using System;
using UnityEngine;

namespace Veerdonk.ProjectileSystem
{
    /// <summary>
    /// Triangle, then center-plus-ring, then an inner hexagon and outer rings.
    /// Adjacent chord on a ring is <c>spread</c> unless that would sit inside a previous ring.
    /// </summary>
    [Serializable]
    public sealed class CirclePattern : IMultiplyPattern
    {
        /// <inheritdoc />
        public Vector3 GetOffset(ModifierContext context, Vector3 forward, float spread, int index, int count)
        {
            if (spread <= 0f || count <= 0 || index < 0 || index >= count)
                return Vector3.zero;

            return MultiplyPatternPlane.FromLocal(forward, Local(index, count, spread));
        }

        static Vector2 Local(int index, int count, float s)
        {
            if (count <= 10)
                return Small(index, count, s);
            if (index < 10)
                return Small(index, 10, s);
            return Extra(index - 10, count - 10, s);
        }

        static Vector2 Small(int index, int count, float s)
        {
            if (count == 1)
                return Vector2.zero;
            if (count == 2)
                return index == 0 ? new Vector2(-0.5f * s, 0f) : new Vector2(0.5f * s, 0f);
            if (count == 3)
                return Triangle(index, s);
            if (count == 4)
                return index < 3 ? Triangle(index, s) : Vector2.zero;
            if (index == 0)
                return Vector2.zero;
            return OnRing(index - 1, count - 1, ChordRadius(s, count - 1));
        }

        static Vector2 Extra(int extraIndex, int extraCount, float s)
        {
            const int innerCapacity = 6;
            int innerCount = extraCount < innerCapacity ? extraCount : innerCapacity;
            if (extraIndex < innerCount)
                return OnRing(extraIndex, innerCount, s);

            float previousOuter = ChordRadius(s, 9);
            int outsideIndex = extraIndex - innerCapacity;
            int outsideCount = extraCount - innerCapacity;
            int ring = 2;
            while (outsideCount > 0)
            {
                int capacity = 6 * ring;
                int onRing = outsideCount < capacity ? outsideCount : capacity;
                if (outsideIndex < onRing)
                    return OnRing(outsideIndex, onRing, OutsideRadius(s, onRing, previousOuter));

                outsideIndex -= onRing;
                outsideCount -= onRing;
                previousOuter = OutsideRadius(s, onRing, previousOuter);
                ring++;
            }

            return Vector2.zero;
        }

        static Vector2 Triangle(int index, float s)
        {
            float r = s / Mathf.Sqrt(3f);
            if (index == 0)
                return new Vector2(0f, r);
            if (index == 1)
                return new Vector2(-0.5f * s, -0.5f * r);
            return new Vector2(0.5f * s, -0.5f * r);
        }

        static Vector2 OnRing(int index, int count, float radius)
        {
            float theta = index * (2f * Mathf.PI / count);
            return new Vector2(radius * Mathf.Sin(theta), radius * Mathf.Cos(theta));
        }

        static float ChordRadius(float s, int ringCount)
        {
            return s / (2f * Mathf.Sin(Mathf.PI / ringCount));
        }

        static float OutsideRadius(float s, int count, float previousOuter)
        {
            if (count <= 1)
                return previousOuter + s;

            float chord = ChordRadius(s, count);
            return chord < previousOuter ? previousOuter + s : chord;
        }
    }
}
