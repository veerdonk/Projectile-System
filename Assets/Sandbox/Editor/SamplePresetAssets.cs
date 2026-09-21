using Veerdonk.ProjectileSystem;
using UnityEditor;
using UnityEngine;

namespace Sandbox.Editor
{
    static class SamplePresetAssets
    {
        public const string Folder = "Assets/Sandbox/Recipes";

        public static SampleLab.Preset[] CreateOrUpdate()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets/Sandbox", "Recipes");

            RecipeAsset multiplyChild = Upsert("MultiplyChild", asset =>
            {
                ConfigureBase(asset, "multiply-child", 8f, 2f, 0.2f);
                ClearStages(asset);
            });
            RecipeAsset multicastExtra = Upsert("MulticastExtra", asset =>
            {
                ConfigureBase(asset, "multicast-extra", 18f, 5f, 0.25f);
                ClearStages(asset);
            });

            var presets = new[]
            {
                Preset("Straight", Upsert("Straight", asset =>
                {
                    ConfigureBase(asset, "straight", 20f, 5f);
                    ClearStages(asset);
                })),
                Preset("Pierce", Upsert("Pierce", asset =>
                {
                    ConfigureBase(asset, "pierce", 20f, 5f);
                    ClearStages(asset);
                    asset.OnHit = new IModifier[] { new Pierce(2) };
                })),
                Preset("Bounce", Upsert("Bounce", asset =>
                {
                    ConfigureBase(asset, "bounce", 20f, 5f);
                    ClearStages(asset);
                    asset.OnHit = new IModifier[] { new Bounce(1) };
                })),
                Preset("Multiply on hit (Square)", Upsert("MultiplyOnHit", asset =>
                {
                    ConfigureBase(asset, "multiply-hit", 20f, 5f);
                    ClearStages(asset);
                    asset.OnHit = new IModifier[] { new Multiply(3, multiplyChild, 2f, new SquarePattern()) };
                })),
                Preset("Interval multiply", Upsert("IntervalMultiply", asset =>
                {
                    ConfigureBase(asset, "interval-multiply", 10f, 5f);
                    ClearStages(asset);
                    asset.OnSpawn = new IModifier[] { new Interval(0.5f) };
                    asset.OnInterval = new IModifier[] { new Multiply(2, multiplyChild, 2f, new SquarePattern()) };
                })),
                Preset("Multicast", Upsert("Multicast", asset =>
                {
                    ConfigureBase(asset, "multicast", 18f, 5f);
                    ClearStages(asset);
                    asset.OnSpawn = new IModifier[] { new Multicast(3, multicastExtra) };
                })),
                Preset("Homing", Upsert("Homing", asset =>
                {
                    ConfigureBase(asset, "homing", 20f, 5f);
                    ClearStages(asset);
                    asset.OnTick = new IModifier[] { new Homing(100f) };
                })),
                Preset("Pierce+Homing", Upsert("PierceHoming", asset =>
                {
                    ConfigureBase(asset, "pierce-homing", 20f, 5f);
                    ClearStages(asset);
                    asset.OnHit = new IModifier[] { new Pierce(1) };
                    asset.OnTick = new IModifier[] { new Homing(300f) };
                })),
                Preset("Bounce+Multiply", Upsert("BounceMultiply", asset =>
                {
                    ConfigureBase(asset, "bounce-multiply", 20f, 5f);
                    ClearStages(asset);
                    asset.OnHit = new IModifier[]
                    {
                        new Bounce(2),
                        new Multiply(2, multiplyChild, 2f, new SquarePattern())
                    };
                })),
                Preset("Speed on travel", Upsert("SpeedTravel", asset =>
                {
                    ConfigureBase(asset, "speed-travel", 20f, 5f);
                    ClearStages(asset);
                    asset.OnTravel = new IModifier[] { new Speed(2f) };
                })),
                Preset("ApplyStatus", Upsert("ApplyStatus", asset =>
                {
                    ConfigureBase(asset, "apply-status", 20f, 5f);
                    ClearStages(asset);
                    asset.OnHit = new IModifier[] { new ApplyStatus(new DummyStatusEffect()) };
                })),
                Preset("Size", Upsert("Size", asset =>
                {
                    ConfigureBase(asset, "size-travel", 20f, 5f);
                    ClearStages(asset);
                    asset.OnTravel = new IModifier[] { new Size(2f) };
                })),
                Preset("OnHit AreaEffect", Upsert("AreaOnHit", asset =>
                {
                    ConfigureBase(asset, "area-onhit", 20f, 5f, effectRadius: 3f);
                    ClearStages(asset);
                    asset.OnHit = new IModifier[] { new AreaEffect(3f) };
                })),
                Preset("Area-only OnHit", Upsert("AreaOnly", asset =>
                {
                    ConfigureBase(asset, "area-only", 20f, 5f, effectRadius: 3f);
                    asset.Damage = 5f;
                    ClearStages(asset);
                    asset.OnHit = new IModifier[] { new AreaEffect(3f, includeDirectHit: false) };
                })),
                Preset("OnTravel AreaEffect", Upsert("AreaTravel", asset =>
                {
                    ConfigureBase(asset, "area-travel", 20f, 5f, effectRadius: 3f);
                    ClearStages(asset);
                    asset.OnTravel = new IModifier[] { new AreaEffect(3f) };
                })),
                Preset("Interval aura", Upsert("AreaAura", asset =>
                {
                    ConfigureBase(asset, "area-aura", 20f, 5f, effectRadius: 3f);
                    ClearStages(asset);
                    asset.OnSpawn = new IModifier[] { new Interval(0.5f) };
                    asset.OnInterval = new IModifier[] { new AreaEffect(3f) };
                })),
                Preset("Cannonball", Upsert("Cannonball", asset =>
                {
                    ConfigureBase(asset, "cannonball", 32f, 5f, gravity: 80f);
                    ClearStages(asset);
                }), useBallisticAim: true),
                Preset("Cannonball Gravity(2)", Upsert("CannonballGravity", asset =>
                {
                    ConfigureBase(asset, "cannonball-gravity", 20f, 5f, gravity: 80f);
                    ClearStages(asset);
                    asset.OnTravel = new IModifier[] { new Gravity(2f) };
                })),
                Preset("OnHit GroundField", Upsert("GroundOnHit", asset =>
                {
                    ConfigureBase(asset, "ground-onhit", 20f, 5f);
                    ClearStages(asset);
                    asset.OnHit = new IModifier[] { new GroundField(2.25f, 2f, 3f, intervalSeconds: 0.5f) };
                })),
                Preset("OnExpire GroundField", Upsert("GroundExpire", asset =>
                {
                    ConfigureBase(asset, "ground-expire", 4f, 1f);
                    ClearStages(asset);
                    asset.OnExpire = new IModifier[] { new GroundField(2.25f, 2f, 3f, intervalSeconds: 0.5f) };
                })),
                Preset("GroundField status", Upsert("GroundStatus", asset =>
                {
                    ConfigureBase(asset, "ground-status", 20f, 5f);
                    ClearStages(asset);
                    asset.OnHit = new IModifier[]
                    {
                        new GroundField(2.25f, 2f, 3f, new DummyStatusEffect(), 0.5f)
                    };
                }))
            };

            if (AssetDatabase.LoadAssetAtPath<RecipeAsset>("Assets/Sandbox/PierceRecipe.asset") != null)
                AssetDatabase.DeleteAsset("Assets/Sandbox/PierceRecipe.asset");

            AssetDatabase.SaveAssets();
            return presets;
        }

        static SampleLab.Preset Preset(string label, RecipeAsset asset, bool useBallisticAim = false)
        {
            return new SampleLab.Preset
            {
                label = label,
                asset = asset,
                useBallisticAim = useBallisticAim
            };
        }

        static RecipeAsset Upsert(string assetName, System.Action<RecipeAsset> configure)
        {
            string path = $"{Folder}/{assetName}.asset";
            RecipeAsset asset = AssetDatabase.LoadAssetAtPath<RecipeAsset>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<RecipeAsset>();
                AssetDatabase.CreateAsset(asset, path);
            }

            configure(asset);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        static void ConfigureBase(
            RecipeAsset asset,
            string id,
            float speed,
            float lifetime,
            float radius = 0.25f,
            float gravity = 0f,
            float effectRadius = 0f)
        {
            asset.Id = id;
            asset.Speed = speed;
            asset.Radius = radius;
            asset.LifetimeSeconds = lifetime;
            asset.Gravity = gravity;
            asset.Damage = 1f;
            asset.CriticalChance = 0f;
            asset.ManaCost = 0f;
            asset.EffectRadius = effectRadius;
            asset.PresentationId = "";
            asset.MulticastGenerations = 0;
            asset.MultiplyGenerations = 0;
        }

        static void ClearStages(RecipeAsset asset)
        {
            asset.OnSpawn = System.Array.Empty<IModifier>();
            asset.OnTravel = System.Array.Empty<IModifier>();
            asset.OnTick = System.Array.Empty<IModifier>();
            asset.OnInterval = System.Array.Empty<IModifier>();
            asset.OnHit = System.Array.Empty<IModifier>();
            asset.OnExpire = System.Array.Empty<IModifier>();
        }
    }
}
