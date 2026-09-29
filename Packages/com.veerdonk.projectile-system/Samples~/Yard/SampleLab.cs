using System.Collections.Generic;
using Veerdonk.ProjectileSystem;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Veerdonk.ProjectileSystem.Samples
{
    /// <summary>
    /// Play Mode sample: turret, mouse aim, RecipeAsset presets, working-copy sheet, Reset.
    /// </summary>
    public sealed class SampleLab : MonoBehaviour
    {
        const int PrewarmCount = 8;

        [System.Serializable]
        public sealed class Preset
        {
            public string label;
            public RecipeAsset asset;
            public bool useBallisticAim;
        }

        enum LastFire
        {
            None,
            Solved,
            Unreachable
        }

        [SerializeField] Preset[] presets;
        [SerializeField] MultiplyPatternAsset[] multiplyPatterns = System.Array.Empty<MultiplyPatternAsset>();
        [SerializeField] GameObject cubeViewPrefab;
        GameObject laserViewPrefab;
        [SerializeField] SandboxSpawnPoint muzzle;
        [SerializeField] Transform turret;
        [SerializeField] Transform gun;

        SandboxDummyTarget[] dummies = System.Array.Empty<SandboxDummyTarget>();
        SandboxDummyTarget selectedDummy;
        ProjectileWorld world;
        GameObject poolRoot;
        Recipe working;
        int selectedPreset;
        bool presetMenuOpen;
        bool hasGroundPoint;
        Vector3 lastAimPoint;
        Collider lastAimCollider;
        LastFire lastFire;
        ProjectileId heldBeam;
        bool fireButtonHeld;
        Rect sheetRect = new Rect(12f, 12f, 430f, 700f);

        void Awake()
        {
            SampleYard.BuildIfNeeded(transform);
            BindChildren();
            SampleYard.ApplyColors(transform);
            SampleYard.AimCamera();
            LoadWorkingCopy();
            CreateWorld();
        }

        void OnDestroy()
        {
            TearDownWorld();
            if (laserViewPrefab == null)
                return;

            if (Application.isPlaying)
                Destroy(laserViewPrefab);
            else
                DestroyImmediate(laserViewPrefab);
            laserViewPrefab = null;
        }

        void Update()
        {
            bool overUi = IsPointerOverSheet();
            if (!overUi)
                AimTurret();

            if (IsBeamRecipe())
                UpdateHeldBeam(overUi);
            else
            {
                ReleaseHeldBeam();
                Keyboard keyboard = Keyboard.current;
                if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
                    Fire();

                Mouse mouse = Mouse.current;
                if (mouse != null && mouse.leftButton.wasPressedThisFrame && !overUi)
                    Fire();
            }

            if (world != null)
            {
                RefreshHomingCandidates();
                world.Tick(Time.deltaTime);
            }
        }

        void OnGUI()
        {
            float height = Mathf.Min(760f, Screen.height - 24f);
            sheetRect = new Rect(12f, 12f, 430f, height);
            GUILayout.BeginArea(sheetRect, GUI.skin.box);
            GUILayout.Label("Projectile sample");
            DrawPresetDropdown();
            GUILayout.BeginHorizontal();
            if (IsBeamRecipe())
                fireButtonHeld = GUILayout.RepeatButton("Fire", GUILayout.Height(28f));
            else if (GUILayout.Button("Fire", GUILayout.Height(28f)))
                Fire();
            if (GUILayout.Button("Reset", GUILayout.Height(28f)))
                ResetLab();
            GUILayout.EndHorizontal();
            if (UsesBallisticAim())
                GUILayout.Label(LastFireLabel());

            GUILayout.Label(
                $"Active { (world != null ? world.ActiveCount : 0) }   pool { (world != null ? world.AvailableCount : 0) }");
            if (selectedDummy != null)
            {
                string status = string.IsNullOrEmpty(selectedDummy.LastStatusId) ? "-" : selectedDummy.LastStatusId;
                GUILayout.Label($"{selectedDummy.name} HP {selectedDummy.Hp:0.##}/{selectedDummy.MaxHp:0.##}  status {status}");
            }

            GUILayout.Space(6f);
            SampleRecipeSheet.Draw(working, presets, multiplyPatterns);
            GUILayout.EndArea();
        }

        bool IsPointerOverSheet()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
                return false;

            Vector2 pixel = mouse.position.ReadValue();
            var gui = new Vector2(pixel.x, Screen.height - pixel.y);
            return sheetRect.Contains(gui);
        }

        void DrawPresetDropdown()
        {
            string current = CurrentLabel();
            if (GUILayout.Button(current, GUILayout.Height(26f)))
                presetMenuOpen = !presetMenuOpen;

            if (!presetMenuOpen || presets == null)
                return;

            for (int i = 0; i < presets.Length; i++)
            {
                Preset preset = presets[i];
                if (preset == null || preset.asset == null)
                    continue;
                string label = string.IsNullOrEmpty(preset.label) ? preset.asset.name : preset.label;
                if (!GUILayout.Button(label, GUILayout.Height(22f)))
                    continue;

                SelectPreset(i);
                presetMenuOpen = false;
            }
        }

        string CurrentLabel()
        {
            if (presets == null || selectedPreset < 0 || selectedPreset >= presets.Length)
                return "(no preset)";
            Preset preset = presets[selectedPreset];
            if (preset == null || preset.asset == null)
                return "(no preset)";
            return string.IsNullOrEmpty(preset.label) ? preset.asset.name : preset.label;
        }

        void SelectPreset(int index)
        {
            ReleaseHeldBeam();
            selectedPreset = index;
            lastFire = LastFire.None;
            LoadWorkingCopy();
            if (world != null && working != null)
                world.Prewarm(working, PrewarmCount);
        }

        void LoadWorkingCopy()
        {
            SampleRecipeSheet.ClearBuffers();
            RecipeAsset asset = CurrentAsset();
            working = asset != null ? asset.ToRecipe() : null;
        }

        RecipeAsset CurrentAsset()
        {
            Preset preset = CurrentPreset();
            return preset != null ? preset.asset : null;
        }

        Preset CurrentPreset()
        {
            if (presets == null || selectedPreset < 0 || selectedPreset >= presets.Length)
                return null;
            return presets[selectedPreset];
        }

        bool UsesBallisticAim()
        {
            Preset preset = CurrentPreset();
            return preset != null && preset.useBallisticAim;
        }

        bool UsesAimPointHoming()
        {
            Preset preset = CurrentPreset();
            if (preset == null)
                return false;

            return preset.label == "Homing" || preset.label == "Pierce+Homing";
        }

        bool TryBallisticDirection(Vector3 origin, Vector3 target, out Vector3 direction)
        {
            return BallisticClearance.TrySolve(
                origin,
                target,
                working.Speed,
                working.Gravity,
                SampleYard.StartRadius(working),
                working.LifetimeSeconds,
                lastAimCollider,
                out direction);
        }

        string LastFireLabel()
        {
            switch (lastFire)
            {
                case LastFire.Solved:
                    return "Last fire: solved";
                case LastFire.Unreachable:
                    return "Last fire: unreachable";
                default:
                    return "Last fire: —";
            }
        }

        void AimTurret()
        {
            if (turret == null || gun == null)
                BindChildren();
            Camera cam = Camera.main;
            Mouse mouse = Mouse.current;
            if (cam == null || mouse == null || turret == null)
                return;

            Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
            if (!GroundAim.TryAimPoint(ray, out Vector3 point, out Collider collider))
                return;

            lastAimPoint = point;
            lastAimCollider = collider;
            hasGroundPoint = true;

            Vector3 yaw = point - turret.position;
            yaw.y = 0f;
            if (yaw.sqrMagnitude >= 1e-8f)
                turret.rotation = Quaternion.LookRotation(yaw.normalized, Vector3.up);

            if (gun == null)
                return;

            if (UsesBallisticAim() && working != null && muzzle != null)
            {
                Vector3 origin = SampleYard.SpawnPosition(muzzle.Position, working);
                if (TryBallisticDirection(origin, point, out Vector3 loft))
                {
                    gun.rotation = Quaternion.LookRotation(loft, Vector3.up);
                    return;
                }
            }

            Vector3 toPoint = point - gun.position;
            if (toPoint.sqrMagnitude < 1e-8f)
                return;

            gun.rotation = Quaternion.LookRotation(toPoint.normalized, Vector3.up);
        }

        [ContextMenu("Fire")]
        public void Fire()
        {
            if (IsBeamRecipe())
            {
                if (heldBeam == default)
                    heldBeam = SpawnShot();
                return;
            }

            SpawnShot();
        }

        ProjectileId SpawnShot()
        {
            BindChildren();
            if (world == null || working == null || muzzle == null)
            {
                Debug.LogWarning("SampleLab: world, recipe, or muzzle missing; cannot fire.", this);
                return default;
            }

            Vector3 position = SampleYard.SpawnPosition(muzzle.Position, working);
            var request = new SpawnRequest
            {
                Recipe = working,
                Position = position,
                Direction = ShotDirection()
            };
            if (hasGroundPoint && (UsesAimPointHoming() || UsesBallisticAim()))
            {
                request.HomingAcquirePointSet = true;
                request.HomingAcquirePoint = lastAimPoint;
            }

            if (UsesBallisticAim())
            {
                if (!hasGroundPoint)
                {
                    lastFire = LastFire.Unreachable;
                    return default;
                }

                ProjectileId id = world.Spawn(request);
                lastFire = id == default ? LastFire.Unreachable : LastFire.Solved;
                return id;
            }

            return world.Spawn(request);
        }

        bool IsBeamRecipe()
        {
            return working != null && working.Delivery == Delivery.Beam;
        }

        void UpdateHeldBeam(bool overUi)
        {
            if (!BeamWanted(overUi))
            {
                ReleaseHeldBeam();
                return;
            }

            if (heldBeam != default)
            {
                AimHeldBeam();
                return;
            }

            heldBeam = SpawnShot();
        }

        void AimHeldBeam()
        {
            if (world == null || muzzle == null || working == null)
                return;

            Vector3 position = SampleYard.SpawnPosition(muzzle.Position, working);
            world.AimBeam(heldBeam, position, ShotDirection());
        }

        Vector3 ShotDirection()
        {
            if (muzzle == null)
                return Vector3.forward;

            if (IsBeamRecipe())
            {
                Vector3 barrel = muzzle.Direction;
                return barrel.sqrMagnitude > 1e-8f ? barrel.normalized : Vector3.forward;
            }

            return GroundAim.Flatten(muzzle.Direction);
        }

        bool BeamWanted(bool overUi)
        {
            Mouse mouse = Mouse.current;
            Keyboard keyboard = Keyboard.current;
            bool pointer = mouse != null && mouse.leftButton.isPressed && !overUi;
            bool space = keyboard != null && keyboard.spaceKey.isPressed;
            return pointer || space || fireButtonHeld;
        }

        void ReleaseHeldBeam()
        {
            if (heldBeam == default || world == null)
            {
                heldBeam = default;
                return;
            }

            ProjectileId id = heldBeam;
            heldBeam = default;
            world.Despawn(id);
        }

        [ContextMenu("Reset")]
        public void ResetLab()
        {
            BindChildren();
            for (int i = 0; i < dummies.Length; i++)
                dummies[i]?.Restore();
            SampleYard.ResetTurretPose(transform);
            lastFire = LastFire.None;
            heldBeam = default;
            fireButtonHeld = false;
            LoadWorkingCopy();
            CreateWorld();
        }

        void CreateWorld()
        {
            TearDownWorld();
            poolRoot = new GameObject("ProjectilePool");
            poolRoot.transform.SetParent(transform, false);
            world = new ProjectileWorld(poolRoot.transform, CreateViewFactory());
            if (presets != null)
            {
                for (int i = 0; i < presets.Length; i++)
                {
                    RecipeAsset asset = presets[i] != null ? presets[i].asset : null;
                    if (asset != null)
                        world.Prewarm(asset.ToRecipe(), PrewarmCount);
                }
            }

            if (working != null)
                world.Prewarm(working, PrewarmCount);

            world.StatusApplied += OnStatusApplied;
            BindDummies();
        }

        void TearDownWorld()
        {
            UnbindDummies();
            if (world != null)
            {
                world.StatusApplied -= OnStatusApplied;
                world.Dispose();
                world = null;
            }

            if (poolRoot != null)
            {
                if (Application.isPlaying)
                    Destroy(poolRoot);
                else
                    DestroyImmediate(poolRoot);
                poolRoot = null;
            }
        }

        PrefabViewFactory CreateViewFactory()
        {
            GameObject cubePrefab = cubeViewPrefab != null
                ? cubeViewPrefab
                : CreateRuntimeViewPrefab(PrimitiveType.Cube, "RuntimeCubeView");
            if (laserViewPrefab == null)
            {
                laserViewPrefab = CreateRuntimeViewPrefab(PrimitiveType.Cylinder, "RuntimeLaserView");
                TintLaser(laserViewPrefab);
            }

            return new PrefabViewFactory(new Dictionary<string, GameObject>
            {
                ["cube"] = cubePrefab,
                ["laser"] = laserViewPrefab
            });
        }

        static void TintLaser(GameObject view)
        {
            Renderer renderer = view.GetComponent<Renderer>();
            if (renderer == null || renderer.sharedMaterial == null)
                return;

            Material tinted = new Material(renderer.sharedMaterial);
            if (tinted.HasProperty("_BaseColor"))
                tinted.SetColor("_BaseColor", Color.red);
            if (tinted.HasProperty("_Color"))
                tinted.SetColor("_Color", Color.red);
            renderer.sharedMaterial = tinted;
        }

        GameObject CreateRuntimeViewPrefab(PrimitiveType primitive, string objectName)
        {
            GameObject go = GameObject.CreatePrimitive(primitive);
            go.name = objectName;
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.one * 0.5f;
            Collider collider = go.GetComponent<Collider>();
            if (collider != null)
                collider.enabled = false;
            go.SetActive(false);
            return go;
        }

        public void BindChildren()
        {
            turret = SampleYard.FindTurret(transform);
            gun = SampleYard.FindGun(transform);
            muzzle = SampleYard.FindMuzzle(transform);
            dummies = GetComponentsInChildren<SandboxDummyTarget>(true);
            selectedDummy = null;
            for (int i = 0; i < dummies.Length; i++)
            {
                if (dummies[i] != null && dummies[i].name == SampleYard.DummyName)
                {
                    selectedDummy = dummies[i];
                    break;
                }
            }

            if (selectedDummy == null && dummies.Length > 0)
                selectedDummy = dummies[0];
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

        void OnStatusApplied(StatusInfo info)
        {
            for (int i = 0; i < dummies.Length; i++)
            {
                SandboxDummyTarget dummy = dummies[i];
                if (dummy != null && dummy.TargetId == info.TargetId)
                    dummy.Apply(info.Effect);
            }
        }
    }
}
