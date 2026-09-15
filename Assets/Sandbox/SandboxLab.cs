using Davidvdveerdonk.ComVeerdonkProjectileSystem;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sandbox
{
    /// <summary>
    /// Phase 5 lab driver: fires Recipes through <see cref="IProjectileWorld"/> and shows pool / dummy HP.
    /// </summary>
    public sealed class SandboxLab : MonoBehaviour
    {
        public const string GroundName = "Ground";
        public const string SpawnPointName = "SpawnPoint";
        public const string MarkerName = "Marker";
        public const string DummyName = "DummyTarget";
        public const string Dummy2Name = "DummyTarget2";
        public const string Dummy3Name = "DummyTarget3";
        public const string DummyBounceName = "DummyBounce";
        public const string WallName = "WorldWall";
        const int PrewarmCount = 8;

        static readonly Vector3 CameraPosition = new Vector3(-12f, 8f, -5f);
        static readonly Vector3 CameraLookAt = new Vector3(0f, 1.2f, 12f);
        static readonly Vector3 Dummy1Position = new Vector3(0f, 1f, 8f);
        static readonly Vector3 Dummy2Position = new Vector3(0f, 1f, 14f);
        static readonly Vector3 Dummy3Position = new Vector3(0f, 1f, 20f);
        static readonly Vector3 DummyBouncePosition = new Vector3(4f, 1f, 12f);
        static readonly Vector3 WallPosition = new Vector3(8f, 2f, 8f);
        static readonly Vector3 WallScale = new Vector3(0.5f, 4f, 16f);
        static readonly Vector3 BounceDirection = new Vector3(1f, 0f, 1f);
        static readonly Vector3 HomingDirection = Quaternion.AngleAxis(25f, Vector3.up) * Vector3.forward;
        static readonly Color Dummy1Color = new Color(0.82f, 0.22f, 0.18f);
        static readonly Color Dummy2Color = new Color(0.85f, 0.45f, 0.15f);
        static readonly Color Dummy3Color = new Color(0.55f, 0.12f, 0.12f);
        static readonly Color DummyBounceColor = new Color(0.55f, 0.2f, 0.7f);

        [SerializeField] SandboxSpawnPoint spawnPoint;
        [SerializeField] SandboxDummyTarget dummyTarget;
        [SerializeField] Transform worldWall;
        [SerializeField] Transform requestMarker;

        SandboxDummyTarget[] dummies = System.Array.Empty<SandboxDummyTarget>();
        ProjectileWorld world;
        Recipe straightRecipe;
        Recipe lifetimeRecipe;
        Recipe onHitRecipe;
        Recipe pierceRecipe;
        Recipe bounceRecipe;
        Recipe intervalRecipe;
        Recipe expireRecipe;
        Recipe multicastRecipe;
        Recipe strongHomingRecipe;
        Recipe weakHomingRecipe;
        Recipe childRecipe;
        Recipe multicastExtraRecipe;
        int selectedRecipe;
        string lastRecipeId;
        Vector2 hudScroll;

        int multiplyHitCount = 3;
        int pierceCount = 2;
        int bounceCount = 1;
        int multiplyIntervalCount = 2;
        int multiplyExpireCount = 1;
        int multicastCount = 3;
        int homingStrong = 100;
        int homingWeak = 10;

        int requestCount;
        Vector3 lastRequestPosition;
        Vector3 lastRequestDirection;
        bool hasRequest;
        float requestFlashUntil;

        void Awake()
        {
            BuildLaneIfNeeded(transform);
            BindChildren();
            ApplyLaneColors();
            AimCameraAtLane();
            BuildWorld();
        }

        void OnDestroy()
        {
            UnbindDummies();
            world?.Dispose();
            world = null;
        }

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.digit1Key.wasPressedThisFrame)
                    selectedRecipe = 0;
                if (keyboard.digit2Key.wasPressedThisFrame)
                    selectedRecipe = 1;
                if (keyboard.digit3Key.wasPressedThisFrame)
                    selectedRecipe = 2;
                if (keyboard.digit4Key.wasPressedThisFrame)
                    selectedRecipe = 3;
                if (keyboard.digit5Key.wasPressedThisFrame)
                    selectedRecipe = 4;
                if (keyboard.digit6Key.wasPressedThisFrame)
                    selectedRecipe = 5;
                if (keyboard.digit7Key.wasPressedThisFrame)
                    selectedRecipe = 6;
                if (keyboard.digit8Key.wasPressedThisFrame)
                    selectedRecipe = 7;
                if (keyboard.digit9Key.wasPressedThisFrame)
                    selectedRecipe = 8;
                if (keyboard.digit0Key.wasPressedThisFrame)
                    selectedRecipe = 9;
                if (keyboard.spaceKey.wasPressedThisFrame)
                    Fire(SelectedRecipe());
            }

            if (world != null)
            {
                RefreshHomingCandidates();
                world.Tick(Time.deltaTime);
            }

            if (requestMarker != null)
            {
                float scale = Time.unscaledTime < requestFlashUntil ? 1.45f : 1f;
                requestMarker.localScale = Vector3.one * scale;
            }
        }

        void OnGUI()
        {
            const float width = 440f;
            float height = Mathf.Min(760f, Screen.height - 32f);
            GUILayout.BeginArea(new Rect(16f, 16f, width, height), GUI.skin.box);
            hudScroll = GUILayout.BeginScrollView(hudScroll);
            GUILayout.Label("Projectile lab — phase 5 (homing)");
            GUILayout.Label("Space fires the selected Recipe. 1–9 and 0 select. - / + changes n for the next shot.");

            FireButton("Fire straight", 0, straightRecipe);
            FireButton("Fire lifetime (3s)", 1, lifetimeRecipe);
            FireButtonWithMagnitude("Multiply on hit", 2, onHitRecipe, ref multiplyHitCount, 0, 8);
            FireButtonWithMagnitude("Pierce", 3, pierceRecipe, ref pierceCount, 0, 8);
            FireButtonWithMagnitude("Bounce", 4, bounceRecipe, ref bounceCount, 0, 8);
            FireButtonWithMagnitude("Multiply interval", 5, intervalRecipe, ref multiplyIntervalCount, 0, 8);
            FireButtonWithMagnitude("Multiply on expire", 6, expireRecipe, ref multiplyExpireCount, 0, 8);
            FireButtonWithMagnitude("Multicast", 7, multicastRecipe, ref multicastCount, 1, 8);
            FireButtonWithMagnitude("Homing A", 8, strongHomingRecipe, ref homingStrong, 0, 300, 10);
            FireButtonWithMagnitude("Homing B", 9, weakHomingRecipe, ref homingWeak, 0, 300, 10);

            GUILayout.Space(6f);
            GUILayout.Label(hasRequest
                ? $"Last spawn request #{requestCount}\n  pos {lastRequestPosition}\n  dir {lastRequestDirection}\n  recipe {lastRecipeId}"
                : "Last spawn request: (none)");
            GUILayout.Label($"Active instances: {(world != null ? world.ActiveCount : 0)}");
            GUILayout.Label($"Pool available: {(world != null ? world.AvailableCount : 0)}");
            for (int i = 0; i < dummies.Length; i++)
            {
                SandboxDummyTarget dummy = dummies[i];
                if (dummy != null)
                    GUILayout.Label($"{dummy.name} HP: {dummy.Hp}/{dummy.MaxHp}");
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void FireButton(string title, int slot, Recipe recipe)
        {
            if (!GUILayout.Button(title, GUILayout.Height(28f)))
                return;

            selectedRecipe = slot;
            Fire(recipe);
        }

        void FireButtonWithMagnitude(
            string name,
            int slot,
            Recipe recipe,
            ref int magnitude,
            int min,
            int max,
            int step = 1)
        {
            GUILayout.BeginHorizontal();
            bool fire = GUILayout.Button($"Fire {name}({magnitude})", GUILayout.Height(28f));
            if (GUILayout.Button("-", GUILayout.Width(28), GUILayout.Height(28)))
                magnitude = Mathf.Max(min, magnitude - step);
            if (GUILayout.Button("+", GUILayout.Width(28), GUILayout.Height(28)))
                magnitude = Mathf.Min(max, magnitude + step);
            GUILayout.EndHorizontal();
            if (!fire)
                return;

            selectedRecipe = slot;
            Fire(recipe);
        }

        [ContextMenu("Fire")]
        public void Fire()
        {
            Fire(SelectedRecipe());
        }

        void Fire(Recipe recipe)
        {
            BindChildren();
            if (spawnPoint == null)
            {
                Debug.LogWarning("SandboxLab: no spawn point; cannot fire.", this);
                return;
            }

            if (world == null || recipe == null)
            {
                Debug.LogWarning("SandboxLab: world or recipe missing; cannot fire.", this);
                return;
            }

            ApplyMagnitudes();

            lastRequestPosition = spawnPoint.Position;
            lastRequestDirection = FireDirection(recipe);
            lastRecipeId = recipe.Id;
            requestCount++;
            hasRequest = true;
            requestFlashUntil = Time.unscaledTime + 1.5f;
            if (requestMarker != null)
                requestMarker.gameObject.SetActive(true);

            world.Spawn(new SpawnRequest
            {
                Recipe = recipe,
                Position = lastRequestPosition,
                Direction = lastRequestDirection
            });

            Debug.DrawRay(lastRequestPosition, lastRequestDirection * 6f, Color.yellow, 3f);
            Debug.Log(
                $"Sandbox spawn request #{requestCount} recipe={recipe.Id} pos={lastRequestPosition} dir={lastRequestDirection}",
                this);
        }

        Recipe SelectedRecipe()
        {
            switch (selectedRecipe)
            {
                case 1:
                    return lifetimeRecipe;
                case 2:
                    return onHitRecipe;
                case 3:
                    return pierceRecipe;
                case 4:
                    return bounceRecipe;
                case 5:
                    return intervalRecipe;
                case 6:
                    return expireRecipe;
                case 7:
                    return multicastRecipe;
                case 8:
                    return strongHomingRecipe;
                case 9:
                    return weakHomingRecipe;
                default:
                    return straightRecipe;
            }
        }

        Vector3 FireDirection(Recipe recipe)
        {
            if (recipe == bounceRecipe)
                return BounceDirection;
            if (recipe == strongHomingRecipe || recipe == weakHomingRecipe)
                return HomingDirection;
            return spawnPoint.Direction;
        }

        void BuildWorld()
        {
            childRecipe = CreateRecipe("onhit-child", speed: 8f, lifetime: 2f, radius: 0.2f);
            straightRecipe = CreateRecipe("straight", speed: 20f, lifetime: 5f);
            lifetimeRecipe = CreateRecipe("lifetime", speed: 4f, lifetime: 3f);
            onHitRecipe = CreateRecipe("onhit-parent", speed: 20f, lifetime: 5f);
            pierceRecipe = CreateRecipe("pierce", speed: 20f, lifetime: 5f);
            bounceRecipe = CreateRecipe("bounce", speed: 20f, lifetime: 5f);
            intervalRecipe = CreateRecipe("interval", speed: 10f, lifetime: 5f);
            expireRecipe = CreateRecipe("expire", speed: 4f, lifetime: 1f);
            multicastExtraRecipe = CreateRecipe("multicast-extra", speed: 18f, lifetime: 5f);
            multicastRecipe = CreateRecipe("multicast", speed: 18f, lifetime: 5f);
            strongHomingRecipe = CreateRecipe("homing-strong", speed: 20f, lifetime: 5f);
            weakHomingRecipe = CreateRecipe("homing-weak", speed: 20f, lifetime: 5f);
            ApplyMagnitudes();

            var poolRoot = new GameObject("ProjectilePool");
            poolRoot.transform.SetParent(transform, false);
            world = new ProjectileWorld(poolRoot.transform);
            world.Prewarm(straightRecipe, PrewarmCount);
            world.Prewarm(lifetimeRecipe, PrewarmCount);
            world.Prewarm(onHitRecipe, PrewarmCount);
            world.Prewarm(pierceRecipe, PrewarmCount);
            world.Prewarm(bounceRecipe, PrewarmCount);
            world.Prewarm(intervalRecipe, PrewarmCount);
            world.Prewarm(expireRecipe, PrewarmCount);
            world.Prewarm(multicastRecipe, PrewarmCount);
            world.Prewarm(strongHomingRecipe, PrewarmCount);
            world.Prewarm(weakHomingRecipe, PrewarmCount);

            BindDummies();
        }

        void ApplyMagnitudes()
        {
            if (onHitRecipe != null)
                onHitRecipe.OnHit = new IModifier[] { new Multiply(multiplyHitCount, childRecipe) };
            if (pierceRecipe != null)
                pierceRecipe.OnHit = new IModifier[] { new Pierce(pierceCount) };
            if (bounceRecipe != null)
                bounceRecipe.OnHit = new IModifier[] { new Bounce(bounceCount) };
            if (intervalRecipe != null)
                intervalRecipe.OnInterval = new IModifier[] { new Multiply(multiplyIntervalCount, childRecipe, intervalSeconds: 0.5f) };
            if (expireRecipe != null)
                expireRecipe.OnExpire = new IModifier[] { new Multiply(multiplyExpireCount, childRecipe) };
            if (multicastRecipe != null)
                multicastRecipe.OnSpawn = new IModifier[] { new Multicast(multicastCount, multicastExtraRecipe) };
            if (strongHomingRecipe != null)
                strongHomingRecipe.OnTick = new IModifier[] { new Homing(homingStrong) };
            if (weakHomingRecipe != null)
                weakHomingRecipe.OnTick = new IModifier[] { new Homing(homingWeak) };
        }

        static Recipe CreateRecipe(string id, float speed, float lifetime, float radius = 0.25f)
        {
            return new Recipe
            {
                Id = id,
                Speed = speed,
                Radius = radius,
                LifetimeSeconds = lifetime,
                PresentationId = "sphere"
            };
        }

        public void BindChildren()
        {
            if (spawnPoint == null)
                spawnPoint = GetComponentInChildren<SandboxSpawnPoint>(true);
            dummies = GetComponentsInChildren<SandboxDummyTarget>(true);
            dummyTarget = dummies.Length > 0 ? dummies[0] : null;
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

        void RefreshHomingCandidates()
        {
            for (int i = 0; i < dummies.Length; i++)
            {
                SandboxDummyTarget dummy = dummies[i];
                if (dummy != null)
                    world.SetHomingCandidate(dummy.TargetId, dummy.transform.position);
            }
        }

        void BindDummies()
        {
            BindChildren();
            for (int i = 0; i < dummies.Length; i++)
                dummies[i]?.Bind(world);
        }

        void UnbindDummies()
        {
            for (int i = 0; i < dummies.Length; i++)
                dummies[i]?.Unbind();
        }

        /// <summary>
        /// Creates ground, spawn, dummies, and wall under <paramref name="root"/> when missing.
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

            created |= EnsureDummy(root, DummyName, Dummy1Position, 1, Dummy1Color);
            created |= EnsureDummy(root, Dummy2Name, Dummy2Position, 2, Dummy2Color);
            created |= EnsureDummy(root, Dummy3Name, Dummy3Position, 3, Dummy3Color);
            created |= EnsureDummy(root, DummyBounceName, DummyBouncePosition, 4, DummyBounceColor);

            Transform wall = root.Find(WallName);
            if (wall == null)
            {
                created = true;
                GameObject wallGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wallGo.name = WallName;
                wall = wallGo.transform;
                wall.SetParent(root, false);
                Tint(wallGo, new Color(0.45f, 0.45f, 0.5f));
            }

            wall.localPosition = WallPosition;
            wall.localScale = WallScale;
            Tint(wall.gameObject, new Color(0.45f, 0.45f, 0.5f));

            return created;
        }

        static bool EnsureDummy(Transform root, string name, Vector3 position, int targetId, Color color)
        {
            Transform existing = root.Find(name);
            bool created = false;
            if (existing == null)
            {
                created = true;
                GameObject dummy = GameObject.CreatePrimitive(PrimitiveType.Cube);
                dummy.name = name;
                dummy.transform.SetParent(root, false);
                dummy.transform.localScale = Vector3.one * 2f;
                dummy.AddComponent<SandboxDummyTarget>();
                existing = dummy.transform;
            }

            existing.localPosition = position;
            var target = existing.GetComponent<SandboxDummyTarget>();
            target?.Configure(targetId);
            Tint(existing.gameObject, color);
            return created;
        }

        void ApplyLaneColors()
        {
            Transform ground = transform.Find(GroundName);
            if (ground != null)
                Tint(ground.gameObject, new Color(0.28f, 0.32f, 0.28f));
            if (requestMarker != null)
                Tint(requestMarker.gameObject, new Color(1f, 0.82f, 0.15f));
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
