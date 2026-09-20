using Davidvdveerdonk.ComVeerdonkProjectileSystem;
using UnityEngine;

namespace Sandbox
{
    public static class SampleYard
    {
        public const string GroundName = "Ground";
        public const string TurretName = "Turret";
        public const string BodyName = "Body";
        public const string BarrelName = "Barrel";
        public const string MuzzleName = "Muzzle";
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

        public static readonly Vector3 CameraPosition = new Vector3(0f, 14f, -12f);
        public static readonly Vector3 CameraLookAt = new Vector3(0f, 1f, 10f);
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
        static readonly Color Dummy1Color = new Color(0.82f, 0.22f, 0.18f);
        static readonly Color Dummy2Color = new Color(0.85f, 0.45f, 0.15f);
        static readonly Color Dummy3Color = new Color(0.55f, 0.12f, 0.12f);
        static readonly Color DummyBounceColor = new Color(0.55f, 0.2f, 0.7f);
        static readonly Color DummySplashColor = new Color(0.2f, 0.55f, 0.85f);
        static readonly Color DummyMuzzleColor = new Color(0.15f, 0.7f, 0.55f);
        static readonly Color DummyBasicColor = new Color(0.35f, 0.6f, 0.75f);
        static readonly Color StrongShieldColor = new Color(0.9f, 0.75f, 0.2f);
        static readonly Color TurretColor = new Color(0.42f, 0.45f, 0.5f);
        static readonly Color BarrelColor = new Color(0.28f, 0.3f, 0.34f);

        public static bool BuildIfNeeded(Transform root)
        {
            bool created = false;

            if (root.Find(GroundName) == null)
            {
                created = true;
                GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ground.name = GroundName;
                ground.transform.SetParent(root, false);
                ground.transform.localPosition = new Vector3(0f, -0.1f, 14f);
                ground.transform.localScale = new Vector3(28f, 0.2f, 44f);
                Tint(ground, new Color(0.28f, 0.32f, 0.28f));
            }

            created |= EnsureTurret(root);
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

        public static Transform FindTurret(Transform root)
        {
            return root.Find(TurretName);
        }

        public static SandboxSpawnPoint FindMuzzle(Transform root)
        {
            Transform turret = FindTurret(root);
            Transform muzzle = turret != null ? turret.Find(MuzzleName) : null;
            return muzzle != null ? muzzle.GetComponent<SandboxSpawnPoint>() : null;
        }

        public static void ResetTurretYaw(Transform root)
        {
            Transform turret = FindTurret(root);
            if (turret != null)
                turret.localRotation = Quaternion.identity;
        }

        public static void AimCamera()
        {
            Camera cam = Camera.main;
            if (cam == null)
                return;

            cam.transform.position = CameraPosition;
            cam.transform.LookAt(CameraLookAt);
        }

        public static void ApplyColors(Transform root)
        {
            Transform ground = root.Find(GroundName);
            if (ground != null)
                Tint(ground.gameObject, new Color(0.28f, 0.32f, 0.28f));
            Transform turret = FindTurret(root);
            if (turret == null)
                return;

            Transform body = turret.Find(BodyName);
            if (body != null)
                Tint(body.gameObject, TurretColor);
            Transform barrel = turret.Find(BarrelName);
            if (barrel != null)
                Tint(barrel.gameObject, BarrelColor);
        }

        static bool EnsureTurret(Transform root)
        {
            Transform existing = root.Find(TurretName);
            if (existing != null)
                return false;

            var turret = new GameObject(TurretName);
            turret.transform.SetParent(root, false);
            turret.transform.localPosition = Vector3.zero;
            turret.transform.localRotation = Quaternion.identity;

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            body.name = BodyName;
            body.transform.SetParent(turret.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.7f, 0f);
            body.transform.localScale = Vector3.one * 1.2f;
            StripCollider(body);
            Tint(body, TurretColor);

            GameObject barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            barrel.name = BarrelName;
            barrel.transform.SetParent(turret.transform, false);
            barrel.transform.localScale = new Vector3(0.18f, 0.7f, 0.18f);
            barrel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            barrel.transform.localPosition = new Vector3(0f, 0.7f, 1.3f);
            StripCollider(barrel);
            Tint(barrel, BarrelColor);

            var muzzle = new GameObject(MuzzleName);
            muzzle.transform.SetParent(turret.transform, false);
            muzzle.transform.localPosition = new Vector3(0f, 0.7f, 2f);
            muzzle.transform.localRotation = Quaternion.identity;
            muzzle.AddComponent<SandboxSpawnPoint>();
            return true;
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

        static void StripCollider(GameObject go)
        {
            Collider[] colliders = go.GetComponents<Collider>();
            for (int i = 0; i < colliders.Length; i++)
            {
                if (Application.isPlaying)
                    Object.Destroy(colliders[i]);
                else
                    Object.DestroyImmediate(colliders[i]);
            }
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
