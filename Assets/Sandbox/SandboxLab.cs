using UnityEngine;
using UnityEngine.InputSystem;

namespace Sandbox
{
    /// <summary>
    /// Phase 0 lab driver: lane objects, fire control, and pool-count readout.
    /// Does not simulate projectiles; Fire only makes a spawn request visible.
    /// </summary>
    public sealed class SandboxLab : MonoBehaviour
    {
        public const string GroundName = "Ground";
        public const string SpawnPointName = "SpawnPoint";
        public const string MarkerName = "Marker";
        public const string DummyName = "DummyTarget";
        public const string WallName = "WorldWall";

        static readonly Vector3 CameraPosition = new Vector3(-12f, 8f, -5f);
        static readonly Vector3 CameraLookAt = new Vector3(0f, 1.2f, 12f);

        [SerializeField] SandboxSpawnPoint spawnPoint;
        [SerializeField] SandboxDummyTarget dummyTarget;
        [SerializeField] Transform worldWall;
        [SerializeField] Transform requestMarker;

        int requestCount;
        Vector3 lastRequestPosition;
        Vector3 lastRequestDirection;
        bool hasRequest;
        float requestFlashUntil;

        // Placeholders until phase 1 pooling exists.
        int activeInstances;
        int poolAvailable;

        void Awake()
        {
            BuildLaneIfNeeded(transform);
            BindChildren();
            ApplyLaneColors();
            AimCameraAtLane();
        }

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
                Fire();

            if (requestMarker != null)
            {
                float scale = Time.unscaledTime < requestFlashUntil ? 1.45f : 1f;
                requestMarker.localScale = Vector3.one * scale;
            }
        }

        void OnGUI()
        {
            const float width = 420f;
            GUILayout.BeginArea(new Rect(16f, 16f, width, 220f), GUI.skin.box);
            GUILayout.Label("Projectile lab — phase 0");
            GUILayout.Label("Press Space or Fire. A spawn request is recorded; nothing flies yet.");

            if (GUILayout.Button("Fire", GUILayout.Height(28f)))
                Fire();

            GUILayout.Space(6f);
            GUILayout.Label(hasRequest
                ? $"Last spawn request #{requestCount}\n  pos {lastRequestPosition}\n  dir {lastRequestDirection}"
                : "Last spawn request: (none)");
            GUILayout.Label($"Active instances: {activeInstances}");
            GUILayout.Label($"Pool available: {poolAvailable}");
            if (dummyTarget != null)
                GUILayout.Label($"Dummy HP: {dummyTarget.Hp}/{dummyTarget.MaxHp}");
            GUILayout.EndArea();
        }

        [ContextMenu("Fire")]
        public void Fire()
        {
            BindChildren();
            if (spawnPoint == null)
            {
                Debug.LogWarning("SandboxLab: no spawn point; cannot fire.", this);
                return;
            }

            lastRequestPosition = spawnPoint.Position;
            lastRequestDirection = spawnPoint.Direction;
            requestCount++;
            hasRequest = true;
            requestFlashUntil = Time.unscaledTime + 1.5f;
            if (requestMarker != null)
                requestMarker.gameObject.SetActive(true);

            Debug.DrawRay(lastRequestPosition, lastRequestDirection * 6f, Color.yellow, 3f);
            Debug.Log(
                $"Sandbox spawn request #{requestCount} pos={lastRequestPosition} dir={lastRequestDirection}",
                this);
        }

        public void BindChildren()
        {
            if (spawnPoint == null)
                spawnPoint = GetComponentInChildren<SandboxSpawnPoint>(true);
            if (dummyTarget == null)
                dummyTarget = GetComponentInChildren<SandboxDummyTarget>(true);
            if (worldWall == null)
            {
                Transform found = transform.Find(WallName);
                worldWall = found;
            }

            if (requestMarker == null && spawnPoint != null)
            {
                Transform marker = spawnPoint.transform.Find(MarkerName);
                requestMarker = marker;
            }
        }

        /// <summary>
        /// Creates ground, spawn, dummy, and wall under <paramref name="root"/> when missing.
        /// Returns true if any object was created.
        /// </summary>
        public static bool BuildLaneIfNeeded(Transform root)
        {
            bool created = false;

            if (root.Find(GroundName) == null)
            {
                created = true;
                GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ground.name = GroundName;
                ground.transform.SetParent(root, false);
                ground.transform.localPosition = new Vector3(0f, -0.1f, 14f);
                ground.transform.localScale = new Vector3(24f, 0.2f, 40f);
                Tint(ground, new Color(0.28f, 0.32f, 0.28f));
            }

            Transform spawnTransform = root.Find(SpawnPointName);
            if (spawnTransform == null)
            {
                created = true;
                GameObject spawnGo = new GameObject(SpawnPointName);
                spawnTransform = spawnGo.transform;
                spawnTransform.SetParent(root, false);
                spawnTransform.localPosition = new Vector3(0f, 1f, 0f);
                spawnTransform.localRotation = Quaternion.identity;
                spawnGo.AddComponent<SandboxSpawnPoint>();
            }

            if (spawnTransform.Find(MarkerName) == null)
            {
                created = true;
                GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                marker.name = MarkerName;
                marker.transform.SetParent(spawnTransform, false);
                marker.transform.localPosition = Vector3.zero;
                marker.transform.localScale = Vector3.one * 0.35f;
                Collider markerCollider = marker.GetComponent<Collider>();
                if (markerCollider != null)
                {
                    if (Application.isPlaying)
                        Object.Destroy(markerCollider);
                    else
                        Object.DestroyImmediate(markerCollider);
                }

                Tint(marker, new Color(1f, 0.82f, 0.15f));
            }

            if (root.Find(DummyName) == null)
            {
                created = true;
                GameObject dummy = GameObject.CreatePrimitive(PrimitiveType.Cube);
                dummy.name = DummyName;
                dummy.transform.SetParent(root, false);
                dummy.transform.localPosition = new Vector3(0f, 1f, 14f);
                dummy.transform.localScale = Vector3.one * 2f;
                dummy.AddComponent<SandboxDummyTarget>();
                Tint(dummy, new Color(0.82f, 0.22f, 0.18f));
            }

            if (root.Find(WallName) == null)
            {
                created = true;
                GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = WallName;
                wall.transform.SetParent(root, false);
                wall.transform.localPosition = new Vector3(0f, 2f, 24f);
                wall.transform.localScale = new Vector3(12f, 4f, 0.5f);
                Tint(wall, new Color(0.45f, 0.45f, 0.5f));
            }

            return created;
        }

        void ApplyLaneColors()
        {
            Transform ground = transform.Find(GroundName);
            if (ground != null)
                Tint(ground.gameObject, new Color(0.28f, 0.32f, 0.28f));
            if (requestMarker != null)
                Tint(requestMarker.gameObject, new Color(1f, 0.82f, 0.15f));
            if (dummyTarget != null)
                Tint(dummyTarget.gameObject, new Color(0.82f, 0.22f, 0.18f));
            if (worldWall != null)
                Tint(worldWall.gameObject, new Color(0.45f, 0.45f, 0.5f));
        }

        public static void AimCameraAtLane()
        {
            Camera cam = Camera.main;
            if (cam == null)
                return;

            cam.transform.position = CameraPosition;
            cam.transform.LookAt(CameraLookAt);
        }

        static void Tint(GameObject go, Color color)
        {
            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer == null)
                return;

            Material source = renderer.sharedMaterial;
            bool alreadyInstanced = source != null && source.name.IndexOf("(Instance)", System.StringComparison.Ordinal) >= 0;
            Material mat = alreadyInstanced
                ? source
                : source != null
                    ? new Material(source)
                    : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", color);
            renderer.sharedMaterial = mat;
        }
    }
}
