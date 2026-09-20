using System.Collections.Generic;
using Davidvdveerdonk.ComVeerdonkProjectileSystem;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sandbox
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
        }

        [SerializeField] Preset[] presets;
        [SerializeField] GameObject cubeViewPrefab;
        [SerializeField] SandboxSpawnPoint muzzle;
        [SerializeField] Transform turret;

        SandboxDummyTarget[] dummies = System.Array.Empty<SandboxDummyTarget>();
        SandboxDummyTarget selectedDummy;
        ProjectileWorld world;
        GameObject poolRoot;
        Recipe working;
        int selectedPreset;
        bool presetMenuOpen;
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
        }

        void Update()
        {
            bool overUi = IsPointerOverSheet();
            if (!overUi)
                AimTurret();

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
                Fire();

            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && !overUi)
                Fire();

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
            if (GUILayout.Button("Fire", GUILayout.Height(28f)))
                Fire();
            if (GUILayout.Button("Reset", GUILayout.Height(28f)))
                ResetLab();
            GUILayout.EndHorizontal();
            GUILayout.Label(
                $"Active { (world != null ? world.ActiveCount : 0) }   pool { (world != null ? world.AvailableCount : 0) }");
            if (selectedDummy != null)
            {
                string status = string.IsNullOrEmpty(selectedDummy.LastStatusId) ? "-" : selectedDummy.LastStatusId;
                GUILayout.Label($"{selectedDummy.name} HP {selectedDummy.Hp:0.##}/{selectedDummy.MaxHp:0.##}  status {status}");
            }

            GUILayout.Space(6f);
            SampleRecipeSheet.Draw(working);
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
            selectedPreset = index;
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
            if (presets == null || selectedPreset < 0 || selectedPreset >= presets.Length)
                return null;
            Preset preset = presets[selectedPreset];
            return preset != null ? preset.asset : null;
        }

        void AimTurret()
        {
            if (turret == null)
                BindChildren();
            Camera cam = Camera.main;
            Mouse mouse = Mouse.current;
            if (cam == null || mouse == null || turret == null)
                return;

            Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
            if (!GroundAim.TryDirection(ray, turret.position, out Vector3 direction))
                return;

            turret.rotation = Quaternion.LookRotation(direction, Vector3.up);
        }

        [ContextMenu("Fire")]
        public void Fire()
        {
            BindChildren();
            if (world == null || working == null || muzzle == null)
            {
                Debug.LogWarning("SampleLab: world, recipe, or muzzle missing; cannot fire.", this);
                return;
            }

            Vector3 direction = Flatten(muzzle.Direction);
            world.Spawn(new SpawnRequest
            {
                Recipe = working,
                Position = SampleYard.SpawnPosition(muzzle.Position, working),
                Direction = direction
            });
        }

        [ContextMenu("Reset")]
        public void ResetLab()
        {
            BindChildren();
            for (int i = 0; i < dummies.Length; i++)
                dummies[i]?.Restore();
            SampleYard.ResetTurretYaw(transform);
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
            return new PrefabViewFactory(new Dictionary<string, GameObject>
            {
                ["cube"] = cubePrefab
            });
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
            if (turret == null)
                turret = SampleYard.FindTurret(transform);
            if (muzzle == null)
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

        static Vector3 Flatten(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-8f)
                return Vector3.forward;
            return direction.normalized;
        }
    }
}
