using System.Collections.Generic;
using Davidvdveerdonk.ComVeerdonkProjectileSystem;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sandbox
{
    /// <summary>
    /// Phase 19 lab driver: fires Recipes through <see cref="IProjectileWorld"/> and shows pool / dummy HP / status.
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
        public const string DummySplashName = "DummySplash";
        public const string DummyMuzzleName = "DummyMuzzle";
        public const string DummyBasicName = "DummyBasic";
        public const string StrongShieldName = "StrongShield";
        public const string WallName = "WorldWall";
        public const string Wall2Name = "WorldWall2";
        const int PrewarmCount = 8;

        static readonly Vector3 CameraPosition = new Vector3(-12f, 8f, -5f);
        static readonly Vector3 CameraLookAt = new Vector3(0f, 1.2f, 12f);
        static readonly Vector3 Dummy1Position = new Vector3(0f, 1f, 8f);
        static readonly Vector3 Dummy2Position = new Vector3(0f, 1f, 14f);
        static readonly Vector3 Dummy3Position = new Vector3(0f, 1f, 20f);
        static readonly Vector3 DummyBouncePosition = new Vector3(4f, 1f, 12f);
        static readonly Vector3 DummySplashPosition = new Vector3(2f, 1f, 8f);
        static readonly Vector3 DummyMuzzlePosition = new Vector3(2f, 1f, 0f);
        static readonly Vector3 DummyBasicPosition = new Vector3(-2.5f, 1f, 8f);
        static readonly Vector3 StrongShieldPosition = new Vector3(-4f, 1f, 4f);
        static readonly Vector3 StrongShieldScale = Vector3.one * 2f;
        static readonly Vector3 WallPosition = new Vector3(8f, 2f, 8f);
        static readonly Vector3 WallScale = new Vector3(0.5f, 4f, 16f);
        static readonly Vector3 Wall2Position = new Vector3(-6f, 2f, 8f);
        static readonly Vector3 Wall2Scale = new Vector3(0.5f, 4f, 16f);
        static readonly Vector3 BounceDirection = new Vector3(1f, 0f, 1f);
        static readonly Vector3 BounceMultiplyDirection = Vector3.right;
        static readonly Vector3 HomingDirection = Quaternion.AngleAxis(25f, Vector3.up) * Vector3.forward;
        const float CannonballGravity = 80f;
        const float CannonballPitchDegrees = 35f;
        const float AimedCannonballSpeed = 32f;
        const int AimedCannonballSlot = 30;
        const int BounceAtBasicSlot = 31;
        const int StraightAtBasicSlot = 32;
        const int AreaAtStrongSlot = 33;
        const int SphereViewSlot = 34;
        const int CubeViewSlot = 35;
        const int OnHitGroundFieldSlot = 36;
        const int OnExpireGroundFieldSlot = 37;
        const int StatusGroundFieldSlot = 38;
        const int HudPageCount = 2;
        const float GroundFieldRadius = 2.25f;
        const float GroundFieldLifetime = 2f;
        const float GroundFieldDamage = 3f;
        const float GroundFieldInterval = 0.5f;
        static readonly Color Dummy1Color = new Color(0.82f, 0.22f, 0.18f);
        static readonly Color Dummy2Color = new Color(0.85f, 0.45f, 0.15f);
        static readonly Color Dummy3Color = new Color(0.55f, 0.12f, 0.12f);
        static readonly Color DummyBounceColor = new Color(0.55f, 0.2f, 0.7f);
        static readonly Color DummySplashColor = new Color(0.2f, 0.55f, 0.85f);
        static readonly Color DummyMuzzleColor = new Color(0.15f, 0.7f, 0.55f);
        static readonly Color DummyBasicColor = new Color(0.35f, 0.6f, 0.75f);
        static readonly Color StrongShieldColor = new Color(0.9f, 0.75f, 0.2f);

        [SerializeField] SandboxSpawnPoint spawnPoint;
        [SerializeField] SandboxDummyTarget dummyTarget;
        [SerializeField] Transform worldWall;
        [SerializeField] Transform worldWall2;
        [SerializeField] Transform requestMarker;
        [SerializeField] GameObject cubeViewPrefab;
        [SerializeField] GameObject groundFieldViewPrefab;

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
        Recipe pierceHomingRecipe;
        Recipe bounceMultiplyRecipe;
        Recipe travelSpeedRecipe;
        Recipe intervalSpeedRecipe;
        Recipe bounceSpeedRecipe;
        Recipe applyStatusRecipe;
        Recipe pierceApplyStatusRecipe;
        Recipe combatStatsRecipe;
        Recipe travelDamageRecipe;
        Recipe pierceDamageRecipe;
        Recipe travelManaRecipe;
        Recipe travelSizeRecipe;
        Recipe travelSpreadRecipe;
        Recipe onHitAreaRecipe;
        Recipe areaOnlyRecipe;
        Recipe travelAreaRecipe;
        Recipe intervalAuraRecipe;
        Recipe cannonballRecipe;
        Recipe gravityCannonballRecipe;
        Recipe aimedCannonballRecipe;
        Recipe cubeRecipe;
        Recipe onHitGroundFieldRecipe;
        Recipe expireGroundFieldRecipe;
        Recipe statusGroundFieldRecipe;
        Recipe childRecipe;
        Recipe multicastExtraRecipe;
        int selectedRecipe;
        int hudPage;
        string lastRecipeId;
        Vector2 hudScroll;
        bool ballisticAimFailed;
        string ballisticAimMessage;

        int multiplyHitCount = 3;
        int pierceCount = 2;
        int bounceCount = 1;
        int multiplyIntervalCount = 2;
        int multiplyExpireCount = 1;
        int multicastCount = 3;
        int homingStrong = 100;
        int homingWeak = 10;
        float travelSpeedScale = 2f;
        float intervalSpeedScale = 0.5f;
        float bounceSpeedScale = 2f;
        float travelDamageScale = 2f;
        float pierceDamageScale = 2f;
        float travelManaScale = 2f;
        float travelSizeScale = 2f;
        float travelSpreadScale = 2f;
        float travelGravityScale = 2f;
        float aimedCannonballSpeed = AimedCannonballSpeed;
        bool aimedCannonballHighArc;
        float intervalSeconds = 0.5f;
        float multiplySpread = 2f;
        int multiplyPatternIndex;

        int requestCount;
        Vector3 lastRequestPosition;
        Vector3 lastRequestDirection;
        bool hasRequest;
        float requestFlashUntil;
        bool hasInstanceMana;
        float lastInstanceManaCost;
        bool hasInstanceRadius;
        float lastInstanceRadius;

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
            if (world != null)
                world.StatusApplied -= OnStatusApplied;
            world?.Dispose();
            world = null;
        }

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.nKey.wasPressedThisFrame)
                    hudPage = (hudPage + 1) % HudPageCount;
                if (hudPage == 0)
                    SelectRecipeFromPage0(keyboard);
                else
                    SelectRecipeFromPage1(keyboard);
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
            GUILayout.Label("Projectile lab — phase 19 (ground fields)");
            GUILayout.Label("Space fires the selected Recipe. N pages the fire list. Page 1: 1–9, 0, Q, E, R, T, Y, U, I, O, P, F, G, H, J, K, L, Z, X, C, V, B. Page 2: 1 aims at DummyTarget, 2 Bounce at Basic, 3 Straight at Basic, 4 AreaEffect at Strong, 5 Sphere view, 6 Cube view, 7 OnHit GroundField, 8 OnExpire GroundField, 9 GroundField status. - / + changes n for the next shot.");
            DrawHudPageRow();
            if (hudPage == 0)
                DrawPage0FireButtons();
            else
                DrawPage1FireButtons();

            GUILayout.Space(6f);
            Recipe selected = SelectedRecipe();
            if (selected != null)
            {
                GUILayout.Label(
                    $"Selected Recipe {selected.Id}\n" +
                    $"  damage {selected.Damage:0.##}  crit {selected.CriticalChance:0.##}  mana {selected.ManaCost:0.##}\n" +
                    $"  radius {selected.Radius:0.##}  effect radius {selected.EffectRadius:0.##}  gravity {selected.Gravity:0.##}");
                GUILayout.Label(hasInstanceMana
                    ? $"Last instance mana {lastInstanceManaCost:0.##} (Recipe mana stays {selected.ManaCost:0.##})"
                    : "Last instance mana: (none)");
                GUILayout.Label(hasInstanceRadius
                    ? $"Last instance radius {lastInstanceRadius:0.##} (Recipe radius stays {selected.Radius:0.##})"
                    : "Last instance radius: (none)");
            }

            GUILayout.Space(6f);
            if (ballisticAimFailed)
                GUILayout.Label(ballisticAimMessage);
            GUILayout.Label(hasRequest
                ? $"Last spawn request #{requestCount}\n  pos {lastRequestPosition}\n  dir {lastRequestDirection}\n  recipe {lastRecipeId}"
                : "Last spawn request: (none)");
            GUILayout.Label($"Active instances: {(world != null ? world.ActiveCount : 0)}");
            GUILayout.Label($"Pool available: {(world != null ? world.AvailableCount : 0)}");
            for (int i = 0; i < dummies.Length; i++)
            {
                SandboxDummyTarget dummy = dummies[i];
                if (dummy != null)
                {
                    string status = string.IsNullOrEmpty(dummy.LastStatusId) ? "-" : dummy.LastStatusId;
                    GUILayout.Label($"{dummy.name} HP: {dummy.Hp:0.##}/{dummy.MaxHp:0.##}  status: {status}");
                }
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void SelectRecipeFromPage0(Keyboard keyboard)
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
            if (keyboard.qKey.wasPressedThisFrame)
                selectedRecipe = 10;
            if (keyboard.eKey.wasPressedThisFrame)
                selectedRecipe = 11;
            if (keyboard.rKey.wasPressedThisFrame)
                selectedRecipe = 12;
            if (keyboard.tKey.wasPressedThisFrame)
                selectedRecipe = 13;
            if (keyboard.yKey.wasPressedThisFrame)
                selectedRecipe = 14;
            if (keyboard.uKey.wasPressedThisFrame)
                selectedRecipe = 15;
            if (keyboard.iKey.wasPressedThisFrame)
                selectedRecipe = 16;
            if (keyboard.oKey.wasPressedThisFrame)
                selectedRecipe = 17;
            if (keyboard.pKey.wasPressedThisFrame)
                selectedRecipe = 18;
            if (keyboard.fKey.wasPressedThisFrame)
                selectedRecipe = 19;
            if (keyboard.gKey.wasPressedThisFrame)
                selectedRecipe = 20;
            if (keyboard.hKey.wasPressedThisFrame)
                selectedRecipe = 21;
            if (keyboard.jKey.wasPressedThisFrame)
                selectedRecipe = 22;
            if (keyboard.kKey.wasPressedThisFrame)
                selectedRecipe = 23;
            if (keyboard.lKey.wasPressedThisFrame)
                selectedRecipe = 24;
            if (keyboard.zKey.wasPressedThisFrame)
                selectedRecipe = 25;
            if (keyboard.xKey.wasPressedThisFrame)
                selectedRecipe = 26;
            if (keyboard.cKey.wasPressedThisFrame)
                selectedRecipe = 27;
            if (keyboard.vKey.wasPressedThisFrame)
                selectedRecipe = 28;
            if (keyboard.bKey.wasPressedThisFrame)
                selectedRecipe = 29;
        }

        void DrawHudPageRow()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Fire page {hudPage + 1}/{HudPageCount}", GUILayout.Height(28f));
            if (GUILayout.Button("N next", GUILayout.Width(72), GUILayout.Height(28)))
                hudPage = (hudPage + 1) % HudPageCount;
            GUILayout.EndHorizontal();
        }

        void DrawPage0FireButtons()
        {
            FireButton("Fire straight", 0, straightRecipe);
            FireButton("Fire lifetime (3s)", 1, lifetimeRecipe);
            FireButtonWithMagnitude("Multiply on hit", 2, onHitRecipe, ref multiplyHitCount, 0, 16);
            FireButtonWithMagnitude("Pierce", 3, pierceRecipe, ref pierceCount, 0, 8);
            FireButtonWithMagnitude("Bounce", 4, bounceRecipe, ref bounceCount, 0, 8);
            FireButtonWithMagnitude("Multiply interval", 5, intervalRecipe, ref multiplyIntervalCount, 0, 16);
            FireButtonWithMagnitude("Multiply on expire", 6, expireRecipe, ref multiplyExpireCount, 0, 16);
            FireButtonWithMagnitude("Multicast", 7, multicastRecipe, ref multicastCount, 1, 8);
            FireButtonWithMagnitude("Homing A", 8, strongHomingRecipe, ref homingStrong, 0, 300, 10);
            FireButtonWithMagnitude("Homing B", 9, weakHomingRecipe, ref homingWeak, 0, 300, 10);
            FireButton("Fire Pierce(1)+Homing(300)", 10, pierceHomingRecipe);
            FireButton("Fire Bounce(2)+Multiply(2)", 11, bounceMultiplyRecipe);
            FireButtonWithScale("Speed on travel", 12, travelSpeedRecipe, ref travelSpeedScale, 0.25f, 8f, 0.25f);
            FireButtonWithScale("Speed on interval", 13, intervalSpeedRecipe, ref intervalSpeedScale, 0.25f, 8f, 0.25f);
            FireButtonWithScale("Bounce then Speed", 14, bounceSpeedRecipe, ref bounceSpeedScale, 0.25f, 8f, 0.25f);
            FireButton("Fire ApplyStatus", 15, applyStatusRecipe);
            FireButton("Fire Pierce(1)+ApplyStatus", 16, pierceApplyStatusRecipe);
            FireButton("Fire ApplyStatus at wall", 17, applyStatusRecipe);
            FireButton("Fire Damage 5", 18, combatStatsRecipe);
            FireButtonWithScale("Damage on travel", 19, travelDamageRecipe, ref travelDamageScale, 0.25f, 8f, 0.25f);
            FireButtonWithScale("Pierce then Damage", 20, pierceDamageRecipe, ref pierceDamageScale, 0.25f, 8f, 0.25f);
            FireButtonWithScale("Mana on travel", 21, travelManaRecipe, ref travelManaScale, 0.25f, 8f, 0.25f);
            FireButtonWithScale("Size on travel", 22, travelSizeRecipe, ref travelSizeScale, 0.25f, 8f, 0.25f);
            FireButtonWithScale("Spread on travel", 23, travelSpreadRecipe, ref travelSpreadScale, 0.25f, 8f, 0.25f);
            FireButton("OnHit AreaEffect", 24, onHitAreaRecipe);
            FireButton("Area-only OnHit", 25, areaOnlyRecipe);
            FireButton("OnTravel AreaEffect", 26, travelAreaRecipe);
            FireButton("Interval aura", 27, intervalAuraRecipe);
            FireButton("Cannonball", 28, cannonballRecipe);
            FireButtonWithScale("Cannonball Gravity", 29, gravityCannonballRecipe, ref travelGravityScale, 0.25f, 8f, 0.25f);
            ScaleRow($"Interval {intervalSeconds:0.##}s (keys 6, T, and C)", ref intervalSeconds, 0.1f, 3f, 0.1f);
            CycleMultiplyPatternRow();
            ScaleRow($"Multiply spread {multiplySpread:0.##}m", ref multiplySpread, 0f, 8f, 0.5f);
        }

        void SelectRecipeFromPage1(Keyboard keyboard)
        {
            if (keyboard.digit1Key.wasPressedThisFrame)
                selectedRecipe = AimedCannonballSlot;
            if (keyboard.digit2Key.wasPressedThisFrame)
                selectedRecipe = BounceAtBasicSlot;
            if (keyboard.digit3Key.wasPressedThisFrame)
                selectedRecipe = StraightAtBasicSlot;
            if (keyboard.digit4Key.wasPressedThisFrame)
                selectedRecipe = AreaAtStrongSlot;
            if (keyboard.digit5Key.wasPressedThisFrame)
                selectedRecipe = SphereViewSlot;
            if (keyboard.digit6Key.wasPressedThisFrame)
                selectedRecipe = CubeViewSlot;
            if (keyboard.digit7Key.wasPressedThisFrame)
                selectedRecipe = OnHitGroundFieldSlot;
            if (keyboard.digit8Key.wasPressedThisFrame)
                selectedRecipe = OnExpireGroundFieldSlot;
            if (keyboard.digit9Key.wasPressedThisFrame)
                selectedRecipe = StatusGroundFieldSlot;
        }

        void DrawPage1FireButtons()
        {
            FireButtonWithScale(
                aimedCannonballHighArc ? "Aimed cannonball high" : "Aimed cannonball",
                AimedCannonballSlot,
                aimedCannonballRecipe,
                ref aimedCannonballSpeed,
                8f,
                48f,
                4f);
            GUILayout.BeginHorizontal();
            GUILayout.Label(aimedCannonballHighArc ? "Arc: high" : "Arc: low", GUILayout.Height(28f));
            if (GUILayout.Button("Toggle", GUILayout.Width(72), GUILayout.Height(28)))
                aimedCannonballHighArc = !aimedCannonballHighArc;
            GUILayout.EndHorizontal();
            FireButton("Bounce at Basic", BounceAtBasicSlot, bounceRecipe);
            FireButton("Straight at Basic", StraightAtBasicSlot, straightRecipe);
            FireButton("AreaEffect at Strong", AreaAtStrongSlot, onHitAreaRecipe);
            FireButton("Sphere view", SphereViewSlot, straightRecipe);
            FireButton("Cube view", CubeViewSlot, cubeRecipe);
            FireButton("OnHit GroundField", OnHitGroundFieldSlot, onHitGroundFieldRecipe);
            FireButton("OnExpire GroundField", OnExpireGroundFieldSlot, expireGroundFieldRecipe);
            FireButton("GroundField status", StatusGroundFieldSlot, statusGroundFieldRecipe);
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

        void FireButtonWithScale(
            string name,
            int slot,
            Recipe recipe,
            ref float scale,
            float min,
            float max,
            float step)
        {
            GUILayout.BeginHorizontal();
            bool fire = GUILayout.Button($"Fire {name}({scale:0.##})", GUILayout.Height(28f));
            if (GUILayout.Button("-", GUILayout.Width(28), GUILayout.Height(28)))
                scale = Mathf.Max(min, scale - step);
            if (GUILayout.Button("+", GUILayout.Width(28), GUILayout.Height(28)))
                scale = Mathf.Min(max, scale + step);
            GUILayout.EndHorizontal();
            if (!fire)
                return;

            selectedRecipe = slot;
            Fire(recipe);
        }

        void ScaleRow(string label, ref float value, float min, float max, float step)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Height(28f));
            if (GUILayout.Button("-", GUILayout.Width(28), GUILayout.Height(28)))
                value = Mathf.Max(min, value - step);
            if (GUILayout.Button("+", GUILayout.Width(28), GUILayout.Height(28)))
                value = Mathf.Min(max, value + step);
            GUILayout.EndHorizontal();
        }

        void CycleMultiplyPatternRow()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Multiply pattern: {MultiplyPatternName(multiplyPatternIndex)}", GUILayout.Height(28f));
            if (GUILayout.Button("Cycle", GUILayout.Width(64), GUILayout.Height(28)))
                multiplyPatternIndex = (multiplyPatternIndex + 1) % 3;
            GUILayout.EndHorizontal();
        }

        static string MultiplyPatternName(int index)
        {
            switch (index)
            {
                case 1:
                    return "Circle";
                case 2:
                    return "RandomCircle";
                default:
                    return "Square";
            }
        }

        IMultiplyPattern SelectedMultiplyPattern()
        {
            switch (multiplyPatternIndex)
            {
                case 1:
                    return new CirclePattern();
                case 2:
                    return new RandomCirclePattern();
                default:
                    return new SquarePattern();
            }
        }

        Multiply CreateMultiply(int count)
        {
            return new Multiply(count, childRecipe, multiplySpread, SelectedMultiplyPattern());
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
            if (!TryFireDirection(recipe, lastRequestPosition, out lastRequestDirection, out string aimError))
            {
                ballisticAimFailed = true;
                ballisticAimMessage = aimError;
                lastRecipeId = recipe.Id;
                Debug.LogWarning($"SandboxLab: {aimError}", this);
                return;
            }

            ballisticAimFailed = false;
            ballisticAimMessage = null;
            lastRecipeId = recipe.Id;
            requestCount++;
            hasRequest = true;
            requestFlashUntil = Time.unscaledTime + 1.5f;
            if (requestMarker != null)
                requestMarker.gameObject.SetActive(true);

            ProjectileId spawned = world.Spawn(new SpawnRequest
            {
                Recipe = recipe,
                Position = lastRequestPosition,
                Direction = lastRequestDirection
            });

            if (world.TryGetManaCost(spawned, out float instanceMana))
            {
                hasInstanceMana = true;
                lastInstanceManaCost = instanceMana;
            }
            else
            {
                hasInstanceMana = false;
            }

            if (world.TryGetRadius(spawned, out float instanceRadius))
            {
                hasInstanceRadius = true;
                lastInstanceRadius = instanceRadius;
            }
            else
            {
                hasInstanceRadius = false;
            }

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
                case 10:
                    return pierceHomingRecipe;
                case 11:
                    return bounceMultiplyRecipe;
                case 12:
                    return travelSpeedRecipe;
                case 13:
                    return intervalSpeedRecipe;
                case 14:
                    return bounceSpeedRecipe;
                case 15:
                case 17:
                    return applyStatusRecipe;
                case 16:
                    return pierceApplyStatusRecipe;
                case 18:
                    return combatStatsRecipe;
                case 19:
                    return travelDamageRecipe;
                case 20:
                    return pierceDamageRecipe;
                case 21:
                    return travelManaRecipe;
                case 22:
                    return travelSizeRecipe;
                case 23:
                    return travelSpreadRecipe;
                case 24:
                    return onHitAreaRecipe;
                case 25:
                    return areaOnlyRecipe;
                case 26:
                    return travelAreaRecipe;
                case 27:
                    return intervalAuraRecipe;
                case 28:
                    return cannonballRecipe;
                case 29:
                    return gravityCannonballRecipe;
                case AimedCannonballSlot:
                    return aimedCannonballRecipe;
                case BounceAtBasicSlot:
                    return bounceRecipe;
                case StraightAtBasicSlot:
                    return straightRecipe;
                case AreaAtStrongSlot:
                    return onHitAreaRecipe;
                case SphereViewSlot:
                    return straightRecipe;
                case CubeViewSlot:
                    return cubeRecipe;
                case OnHitGroundFieldSlot:
                    return onHitGroundFieldRecipe;
                case OnExpireGroundFieldSlot:
                    return expireGroundFieldRecipe;
                case StatusGroundFieldSlot:
                    return statusGroundFieldRecipe;
                default:
                    return straightRecipe;
            }
        }

        bool TryFireDirection(Recipe recipe, Vector3 origin, out Vector3 direction, out string error)
        {
            error = null;
            if (recipe == aimedCannonballRecipe)
            {
                Vector3 target = dummyTarget != null
                    ? dummyTarget.transform.position
                    : Dummy1Position;
                if (!BallisticAim.TrySolve(
                        origin,
                        target,
                        recipe.Speed,
                        recipe.Gravity,
                        out direction,
                        aimedCannonballHighArc))
                {
                    error =
                        $"BallisticAim unreachable (speed {recipe.Speed:0.##}, gravity {recipe.Gravity:0.##}, " +
                        $"{(aimedCannonballHighArc ? "high" : "low")} arc) — no spawn";
                    return false;
                }

                return true;
            }

            if (selectedRecipe == BounceAtBasicSlot || selectedRecipe == StraightAtBasicSlot)
            {
                direction = DirectionToward(origin, DummyBasicPosition);
                return true;
            }

            if (selectedRecipe == AreaAtStrongSlot)
            {
                direction = DirectionToward(origin, StrongShieldPosition);
                return true;
            }

            direction = FireDirection(recipe);
            return true;
        }

        static Vector3 DirectionToward(Vector3 origin, Vector3 target)
        {
            Vector3 delta = target - origin;
            delta.y = 0f;
            return delta.sqrMagnitude > 1e-12f ? delta.normalized : Vector3.forward;
        }

        Vector3 FireDirection(Recipe recipe)
        {
            if (recipe == bounceRecipe || recipe == bounceSpeedRecipe)
                return BounceDirection;
            if (selectedRecipe == 17)
                return BounceDirection;
            if (recipe == bounceMultiplyRecipe)
                return BounceMultiplyDirection;
            if (recipe == strongHomingRecipe || recipe == weakHomingRecipe)
                return HomingDirection;
            if (recipe == cannonballRecipe || recipe == gravityCannonballRecipe)
                return CannonballDirection(spawnPoint.Direction);
            return spawnPoint.Direction;
        }

        static Vector3 CannonballDirection(Vector3 forward)
        {
            Vector3 flat = forward;
            if (flat.sqrMagnitude < 1e-12f)
                flat = Vector3.forward;
            else
                flat.Normalize();

            Vector3 right = Vector3.Cross(Vector3.up, flat);
            if (right.sqrMagnitude < 1e-12f)
                right = Vector3.right;
            else
                right.Normalize();

            return Quaternion.AngleAxis(-CannonballPitchDegrees, right) * flat;
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
            pierceHomingRecipe = CreateRecipe("pierce-homing", speed: 20f, lifetime: 5f);
            pierceHomingRecipe.OnHit = new IModifier[] { new Pierce(1) };
            pierceHomingRecipe.OnTick = new IModifier[] { new Homing(300f) };
            bounceMultiplyRecipe = CreateRecipe("bounce-multiply", speed: 20f, lifetime: 5f);
            travelSpeedRecipe = CreateRecipe("speed-travel", speed: 20f, lifetime: 5f);
            intervalSpeedRecipe = CreateRecipe("speed-interval", speed: 20f, lifetime: 5f);
            bounceSpeedRecipe = CreateRecipe("bounce-speed", speed: 20f, lifetime: 5f);
            applyStatusRecipe = CreateRecipe("apply-status", speed: 20f, lifetime: 5f);
            applyStatusRecipe.OnHit = new IModifier[]
            {
                new ApplyStatus(new DummyStatusEffect()),
                new ApplyStatus(new DummyStatusEffect())
            };
            pierceApplyStatusRecipe = CreateRecipe("pierce-status", speed: 20f, lifetime: 5f);
            pierceApplyStatusRecipe.OnHit = new IModifier[]
            {
                new Pierce(1),
                new ApplyStatus(new DummyStatusEffect()),
                new ApplyStatus(new DummyStatusEffect())
            };
            combatStatsRecipe = CreateRecipe("combat-stats", speed: 20f, lifetime: 5f);
            combatStatsRecipe.Damage = 5f;
            combatStatsRecipe.CriticalChance = 0.25f;
            combatStatsRecipe.ManaCost = 12f;
            combatStatsRecipe.EffectRadius = 2f;
            travelDamageRecipe = CreateRecipe("damage-travel", speed: 20f, lifetime: 5f);
            travelDamageRecipe.Damage = 5f;
            pierceDamageRecipe = CreateRecipe("pierce-damage", speed: 20f, lifetime: 5f);
            pierceDamageRecipe.Damage = 5f;
            travelManaRecipe = CreateRecipe("mana-travel", speed: 20f, lifetime: 5f);
            travelManaRecipe.ManaCost = 12f;
            travelSizeRecipe = CreateRecipe("size-travel", speed: 20f, lifetime: 5f);
            travelSpreadRecipe = CreateRecipe("spread-travel", speed: 20f, lifetime: 5f);
            onHitAreaRecipe = CreateRecipe("area-onhit", speed: 20f, lifetime: 5f);
            onHitAreaRecipe.EffectRadius = 3f;
            areaOnlyRecipe = CreateRecipe("area-only", speed: 20f, lifetime: 5f);
            areaOnlyRecipe.Damage = 5f;
            areaOnlyRecipe.EffectRadius = 3f;
            travelAreaRecipe = CreateRecipe("area-travel", speed: 20f, lifetime: 5f);
            travelAreaRecipe.EffectRadius = 3f;
            intervalAuraRecipe = CreateRecipe("area-aura", speed: 20f, lifetime: 5f);
            intervalAuraRecipe.EffectRadius = 3f;
            cannonballRecipe = CreateRecipe("cannonball", speed: 20f, lifetime: 5f);
            cannonballRecipe.Gravity = CannonballGravity;
            gravityCannonballRecipe = CreateRecipe("cannonball-gravity", speed: 20f, lifetime: 5f);
            gravityCannonballRecipe.Gravity = CannonballGravity;
            aimedCannonballRecipe = CreateRecipe("cannonball-aimed", speed: AimedCannonballSpeed, lifetime: 5f);
            aimedCannonballRecipe.Gravity = CannonballGravity;
            cubeRecipe = CreateRecipe("cube", speed: 20f, lifetime: 5f, presentationId: "cube");
            onHitGroundFieldRecipe = CreateRecipe("ground-onhit", speed: 20f, lifetime: 5f);
            expireGroundFieldRecipe = CreateRecipe("ground-expire", speed: 4f, lifetime: 1f);
            statusGroundFieldRecipe = CreateRecipe("ground-status", speed: 20f, lifetime: 5f);
            ApplyMagnitudes();

            var poolRoot = new GameObject("ProjectilePool");
            poolRoot.transform.SetParent(transform, false);
            world = new ProjectileWorld(poolRoot.transform, CreateViewFactory(), CreateGroundFieldViewFactory());
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
            world.Prewarm(pierceHomingRecipe, PrewarmCount);
            world.Prewarm(bounceMultiplyRecipe, PrewarmCount);
            world.Prewarm(travelSpeedRecipe, PrewarmCount);
            world.Prewarm(intervalSpeedRecipe, PrewarmCount);
            world.Prewarm(bounceSpeedRecipe, PrewarmCount);
            world.Prewarm(applyStatusRecipe, PrewarmCount);
            world.Prewarm(pierceApplyStatusRecipe, PrewarmCount);
            world.Prewarm(combatStatsRecipe, PrewarmCount);
            world.Prewarm(travelDamageRecipe, PrewarmCount);
            world.Prewarm(pierceDamageRecipe, PrewarmCount);
            world.Prewarm(travelManaRecipe, PrewarmCount);
            world.Prewarm(travelSizeRecipe, PrewarmCount);
            world.Prewarm(travelSpreadRecipe, PrewarmCount);
            world.Prewarm(onHitAreaRecipe, PrewarmCount);
            world.Prewarm(areaOnlyRecipe, PrewarmCount);
            world.Prewarm(travelAreaRecipe, PrewarmCount);
            world.Prewarm(intervalAuraRecipe, PrewarmCount);
            world.Prewarm(cannonballRecipe, PrewarmCount);
            world.Prewarm(gravityCannonballRecipe, PrewarmCount);
            world.Prewarm(aimedCannonballRecipe, PrewarmCount);
            world.Prewarm(cubeRecipe, PrewarmCount);
            world.Prewarm(onHitGroundFieldRecipe, PrewarmCount);
            world.Prewarm(expireGroundFieldRecipe, PrewarmCount);
            world.Prewarm(statusGroundFieldRecipe, PrewarmCount);

            world.StatusApplied += OnStatusApplied;
            BindDummies();
        }

        void ApplyMagnitudes()
        {
            if (onHitRecipe != null)
                onHitRecipe.OnHit = new IModifier[] { CreateMultiply(multiplyHitCount) };
            if (pierceRecipe != null)
                pierceRecipe.OnHit = new IModifier[] { new Pierce(pierceCount) };
            if (bounceRecipe != null)
                bounceRecipe.OnHit = new IModifier[] { new Bounce(bounceCount) };
            if (intervalRecipe != null)
            {
                intervalRecipe.OnSpawn = new IModifier[] { new Interval(intervalSeconds) };
                intervalRecipe.OnInterval = new IModifier[] { CreateMultiply(multiplyIntervalCount) };
            }
            if (expireRecipe != null)
                expireRecipe.OnExpire = new IModifier[] { CreateMultiply(multiplyExpireCount) };
            if (bounceMultiplyRecipe != null)
                bounceMultiplyRecipe.OnHit = new IModifier[] { new Bounce(2), CreateMultiply(2) };
            if (multicastRecipe != null)
                multicastRecipe.OnSpawn = new IModifier[] { new Multicast(multicastCount, multicastExtraRecipe) };
            if (strongHomingRecipe != null)
                strongHomingRecipe.OnTick = new IModifier[] { new Homing(homingStrong) };
            if (weakHomingRecipe != null)
                weakHomingRecipe.OnTick = new IModifier[] { new Homing(homingWeak) };
            if (travelSpeedRecipe != null)
                travelSpeedRecipe.OnTravel = new IModifier[] { new Speed(travelSpeedScale) };
            if (intervalSpeedRecipe != null)
            {
                intervalSpeedRecipe.OnSpawn = new IModifier[] { new Interval(intervalSeconds) };
                intervalSpeedRecipe.OnInterval = new IModifier[] { new Speed(intervalSpeedScale) };
            }
            if (bounceSpeedRecipe != null)
                bounceSpeedRecipe.OnHit = new IModifier[] { new Bounce(1), new Speed(bounceSpeedScale) };
            if (travelDamageRecipe != null)
                travelDamageRecipe.OnTravel = new IModifier[] { new Damage(travelDamageScale) };
            if (pierceDamageRecipe != null)
                pierceDamageRecipe.OnHit = new IModifier[] { new Pierce(1), new Damage(pierceDamageScale) };
            if (travelManaRecipe != null)
                travelManaRecipe.OnTravel = new IModifier[] { new Mana(travelManaScale) };
            if (travelSizeRecipe != null)
                travelSizeRecipe.OnTravel = new IModifier[] { new Size(travelSizeScale) };
            if (travelSpreadRecipe != null)
            {
                travelSpreadRecipe.OnTravel = new IModifier[] { new Spread(travelSpreadScale) };
                travelSpreadRecipe.OnHit = new IModifier[] { CreateMultiply(2) };
            }
            if (onHitAreaRecipe != null)
                onHitAreaRecipe.OnHit = new IModifier[] { new AreaEffect(3f) };
            if (areaOnlyRecipe != null)
                areaOnlyRecipe.OnHit = new IModifier[] { new AreaEffect(3f, includeDirectHit: false) };
            if (travelAreaRecipe != null)
                travelAreaRecipe.OnTravel = new IModifier[] { new AreaEffect(3f) };
            if (intervalAuraRecipe != null)
            {
                intervalAuraRecipe.OnSpawn = new IModifier[] { new Interval(intervalSeconds) };
                intervalAuraRecipe.OnInterval = new IModifier[] { new AreaEffect(3f) };
            }
            if (gravityCannonballRecipe != null)
                gravityCannonballRecipe.OnTravel = new IModifier[] { new Gravity(travelGravityScale) };
            if (aimedCannonballRecipe != null)
            {
                aimedCannonballRecipe.Speed = aimedCannonballSpeed;
                aimedCannonballRecipe.Gravity = CannonballGravity;
            }
            if (onHitGroundFieldRecipe != null)
            {
                onHitGroundFieldRecipe.OnHit = new IModifier[]
                {
                    new GroundField(
                        GroundFieldRadius,
                        GroundFieldLifetime,
                        GroundFieldDamage,
                        intervalSeconds: GroundFieldInterval,
                        presentationId: "disk")
                };
            }
            if (expireGroundFieldRecipe != null)
            {
                expireGroundFieldRecipe.OnExpire = new IModifier[]
                {
                    new GroundField(
                        GroundFieldRadius,
                        GroundFieldLifetime,
                        GroundFieldDamage,
                        intervalSeconds: GroundFieldInterval,
                        presentationId: "disk")
                };
            }
            if (statusGroundFieldRecipe != null)
            {
                statusGroundFieldRecipe.OnHit = new IModifier[]
                {
                    new GroundField(
                        GroundFieldRadius,
                        GroundFieldLifetime,
                        GroundFieldDamage,
                        new DummyStatusEffect(),
                        GroundFieldInterval,
                        presentationId: "disk")
                };
            }
        }

        static Recipe CreateRecipe(
            string id,
            float speed,
            float lifetime,
            float radius = 0.25f,
            string presentationId = "sphere")
        {
            return new Recipe
            {
                Id = id,
                Speed = speed,
                Radius = radius,
                LifetimeSeconds = lifetime,
                PresentationId = presentationId
            };
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

        IGroundFieldViewFactory CreateGroundFieldViewFactory()
        {
            if (groundFieldViewPrefab == null)
                return null;

            return new PrefabGroundFieldViewFactory(new Dictionary<string, GameObject>
            {
                ["disk"] = groundFieldViewPrefab
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
            if (spawnPoint == null)
                spawnPoint = GetComponentInChildren<SandboxSpawnPoint>(true);
            dummies = GetComponentsInChildren<SandboxDummyTarget>(true);
            dummyTarget = dummies.Length > 0 ? dummies[0] : null;
            if (worldWall == null)
                worldWall = transform.Find(WallName);
            if (worldWall2 == null)
                worldWall2 = transform.Find(Wall2Name);

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

        void OnStatusApplied(StatusInfo info)
        {
            for (int i = 0; i < dummies.Length; i++)
            {
                SandboxDummyTarget dummy = dummies[i];
                if (dummy != null && dummy.TargetId == info.TargetId)
                    dummy.Apply(info.Effect);
            }
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
            created |= EnsureDummy(root, DummySplashName, DummySplashPosition, 5, DummySplashColor);
            created |= EnsureDummy(root, DummyMuzzleName, DummyMuzzlePosition, 6, DummyMuzzleColor);
            created |= EnsureDummy(root, DummyBasicName, DummyBasicPosition, 7, DummyBasicColor, ProtectionLevel.Basic);
            created |= EnsureShield(root, StrongShieldName, StrongShieldPosition, StrongShieldScale, StrongShieldColor);
            created |= EnsureWall(root, WallName, WallPosition, WallScale);
            created |= EnsureWall(root, Wall2Name, Wall2Position, Wall2Scale);

            return created;
        }

        static bool EnsureWall(Transform root, string name, Vector3 position, Vector3 scale)
        {
            Transform wall = root.Find(name);
            bool created = false;
            if (wall == null)
            {
                created = true;
                GameObject wallGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wallGo.name = name;
                wall = wallGo.transform;
                wall.SetParent(root, false);
            }

            wall.localPosition = position;
            wall.localScale = scale;
            Tint(wall.gameObject, new Color(0.45f, 0.45f, 0.5f));
            return created;
        }

        static bool EnsureDummy(
            Transform root,
            string name,
            Vector3 position,
            int targetId,
            Color color,
            ProtectionLevel protection = ProtectionLevel.None)
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
            target?.Configure(targetId, 10f, protection);
            Tint(existing.gameObject, color);
            return created;
        }

        static bool EnsureShield(Transform root, string name, Vector3 position, Vector3 scale, Color color)
        {
            Transform existing = root.Find(name);
            bool created = false;
            if (existing == null)
            {
                created = true;
                GameObject shield = GameObject.CreatePrimitive(PrimitiveType.Cube);
                shield.name = name;
                shield.transform.SetParent(root, false);
                shield.AddComponent<SandboxProjectileProtection>();
                existing = shield.transform;
            }

            existing.localPosition = position;
            existing.localScale = scale;
            var protection = existing.GetComponent<SandboxProjectileProtection>();
            protection?.Configure(ProtectionLevel.Strong);
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
            if (worldWall2 != null)
                Tint(worldWall2.gameObject, new Color(0.45f, 0.45f, 0.5f));
            Transform shield = transform.Find(StrongShieldName);
            if (shield != null)
                Tint(shield.gameObject, StrongShieldColor);
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
