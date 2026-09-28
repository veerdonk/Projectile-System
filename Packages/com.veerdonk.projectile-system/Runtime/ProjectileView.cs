using System.Collections.Generic;
using UnityEngine;

namespace Veerdonk.ProjectileSystem
{
    /// <summary>
    /// GameObject presentation for one instance. Prefabs may already carry this;
    /// the factory adds it if missing. Does not write simulation state.
    /// </summary>
    public sealed class ProjectileView : MonoBehaviour
    {
        bool scaleToRadius = true;
        readonly List<Transform> beamSegments = new List<Transform>();
        readonly List<Mesh> beamMeshes = new List<Mesh>();
        readonly List<Vector3> drawnBeamPoints = new List<Vector3>();
        static Mesh cylinderTemplate;
        static Vector3[] cylinderVertices;
        static Vector2[] cylinderUv;
        static float cylinderMinY;
        static float cylinderMaxY;

        internal bool ScaleToRadius
        {
            get => scaleToRadius;
            set => scaleToRadius = value;
        }

        /// <summary>Activates the view at <paramref name="position"/>. Primitive factories also apply radius scale.</summary>
        public void Bind(Vector3 position, float radius)
        {
            transform.position = position;
            SetRadius(radius);
            gameObject.SetActive(true);
        }

        /// <summary>Scales the primitive mesh to <c>2 * radius</c>. Prefab views ignore this. Beam segments keep world size.</summary>
        public void SetRadius(float radius)
        {
            if (!scaleToRadius || beamSegments.Count > 0)
                return;

            float diameter = Mathf.Max(radius, 0.01f) * 2f;
            transform.localScale = new Vector3(diameter, diameter, diameter);
        }

        /// <summary>Moves the view and looks along <paramref name="velocity"/> when it is non-zero.</summary>
        public void SetPose(Vector3 position, Vector3 velocity)
        {
            transform.position = position;
            if (velocity.sqrMagnitude > 1e-8f)
                transform.rotation = Quaternion.LookRotation(velocity);
        }

        void OnDestroy()
        {
            for (int i = 0; i < beamSegments.Count; i++)
            {
                if (beamSegments[i] == null)
                    continue;
                MeshFilter filter = beamSegments[i].GetComponent<MeshFilter>();
                if (filter != null)
                    filter.sharedMesh = null;
            }

            for (int i = 0; i < beamMeshes.Count; i++)
            {
                Mesh mesh = beamMeshes[i];
                if (mesh == null)
                    continue;
                if (Application.isPlaying)
                    Destroy(mesh);
                else
                    DestroyImmediate(mesh);
            }
        }

        /// <summary>Hides the view for return to the pool. Does not Destroy.</summary>
        public void Release()
        {
            HideBeamSegments();
            drawnBeamPoints.Clear();
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Shows one primitive cylinder per polyline segment, scaled so the diameter is <c>2 * radius</c>.
        /// The root renderer is hidden; its material, when present, is copied onto the segments.
        /// The texture's length coordinate runs from 0 at the first point to 1 at the last, across every segment.
        /// </summary>
        public void SetBeamSegments(IReadOnlyList<Vector3> points, float radius)
        {
            if (points == null || points.Count < 2)
            {
                HideBeamSegments();
                drawnBeamPoints.Clear();
                return;
            }

            scaleToRadius = false;
            if (transform.localScale != Vector3.one)
                transform.localScale = Vector3.one;

            if (SameBeamPolyline(points))
                return;

            drawnBeamPoints.Clear();
            for (int i = 0; i < points.Count; i++)
                drawnBeamPoints.Add(points[i]);

            transform.SetPositionAndRotation(points[0], Quaternion.identity);
            transform.localScale = Vector3.one;
            Renderer[] rootRenderers = GetComponents<Renderer>();
            Material material = null;
            for (int i = 0; i < rootRenderers.Length; i++)
            {
                Renderer renderer = rootRenderers[i];
                if (renderer == null)
                    continue;

                if (material == null)
                    material = renderer.sharedMaterial;
                renderer.enabled = false;
            }

            int count = points.Count - 1;
            while (beamSegments.Count < count)
                beamSegments.Add(CreateBeamSegment());

            float total = 0f;
            for (int i = 0; i < count; i++)
                total += Vector3.Distance(points[i], points[i + 1]);

            float diameter = Mathf.Max(radius, 0.01f) * 2f;
            float traveled = 0f;
            for (int i = 0; i < count; i++)
            {
                float length = Vector3.Distance(points[i], points[i + 1]);
                float v0 = total > 1e-4f ? traveled / total : 0f;
                float v1 = total > 1e-4f ? (traveled + length) / total : 1f;
                Transform segment = beamSegments[i];
                segment.gameObject.SetActive(true);
                PlaceBeamSegment(segment, points[i], points[i + 1], diameter, material, v0, v1);
                traveled += length;
            }

            for (int i = count; i < beamSegments.Count; i++)
                beamSegments[i].gameObject.SetActive(false);
        }

        bool SameBeamPolyline(IReadOnlyList<Vector3> points)
        {
            if (drawnBeamPoints.Count != points.Count)
                return false;

            for (int i = 0; i < points.Count; i++)
            {
                if ((drawnBeamPoints[i] - points[i]).sqrMagnitude > 1e-8f)
                    return false;
            }

            return true;
        }

        void HideBeamSegments()
        {
            for (int i = 0; i < beamSegments.Count; i++)
            {
                if (beamSegments[i] != null)
                    beamSegments[i].gameObject.SetActive(false);
            }
        }

        Transform CreateBeamSegment()
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "BeamSegment";
            go.transform.SetParent(transform, false);
            Collider collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
                DestroyImmediate(collider);
            }

            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null)
                renderer.localBounds = new Bounds(Vector3.zero, new Vector3(4f, 4f, 4f));

            MeshFilter filter = go.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null)
            {
                EnsureCylinderTemplate(filter.sharedMesh);
                Mesh copy = Instantiate(filter.sharedMesh);
                copy.name = "BeamSegmentMesh";
                filter.sharedMesh = copy;
                beamMeshes.Add(copy);
            }

            return go.transform;
        }

        static void EnsureCylinderTemplate(Mesh shared)
        {
            if (cylinderTemplate != null || shared == null)
                return;

            cylinderTemplate = shared;
            cylinderVertices = shared.vertices;
            cylinderUv = shared.uv;
            cylinderMinY = 0f;
            cylinderMaxY = 0f;
            if (cylinderVertices == null || cylinderVertices.Length == 0)
                return;

            cylinderMinY = cylinderVertices[0].y;
            cylinderMaxY = cylinderVertices[0].y;
            for (int i = 1; i < cylinderVertices.Length; i++)
            {
                float y = cylinderVertices[i].y;
                if (y < cylinderMinY)
                    cylinderMinY = y;
                if (y > cylinderMaxY)
                    cylinderMaxY = y;
            }
        }

        static void ApplyLengthUv(Mesh mesh, float v0, float v1)
        {
            if (mesh == null || cylinderVertices == null || cylinderUv == null || cylinderUv.Length == 0)
                return;
            if (cylinderVertices.Length != cylinderUv.Length)
                return;

            var uv = new Vector2[cylinderUv.Length];
            float span = cylinderMaxY - cylinderMinY;
            for (int i = 0; i < uv.Length; i++)
            {
                float t = span > 1e-6f ? (cylinderVertices[i].y - cylinderMinY) / span : 0f;
                uv[i] = new Vector2(cylinderUv[i].x, Mathf.Lerp(v0, v1, t));
            }

            mesh.uv = uv;
        }

        static void PlaceBeamSegment(
            Transform segment,
            Vector3 start,
            Vector3 end,
            float diameter,
            Material material,
            float v0,
            float v1)
        {
            Vector3 delta = end - start;
            float length = delta.magnitude;
            if (length < 0.0001f)
                length = 0.0001f;

            Transform parent = segment.parent;
            Vector3 mid = (start + end) * 0.5f;
            Quaternion rotation = Quaternion.FromToRotation(
                Vector3.up,
                delta.sqrMagnitude > 1e-8f ? delta : Vector3.forward);
            Vector3 scale = new Vector3(diameter, length * 0.5f, diameter);
            segment.SetParent(null, true);
            segment.localScale = scale;
            segment.SetPositionAndRotation(mid, rotation);
            if (parent != null)
                segment.SetParent(parent, true);

            MeshFilter filter = segment.GetComponent<MeshFilter>();
            if (filter != null)
                ApplyLengthUv(filter.sharedMesh, v0, v1);

            MeshRenderer renderer = segment.GetComponent<MeshRenderer>();
            if (renderer == null)
                return;

            renderer.localBounds = new Bounds(Vector3.zero, Vector3.one * Mathf.Max(length, diameter, 2f) * 4f);
            if (material != null)
                renderer.sharedMaterial = material;
        }
    }
}
