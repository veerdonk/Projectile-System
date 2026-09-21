using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Veerdonk.ProjectileSystem.Editor.Tests
{
    public sealed class ViewFactoryTests
    {
        [Test]
        public void UnknownPresentationId_FallsBackToPrimitiveSphere()
        {
            var factory = new PrefabViewFactory(new Dictionary<string, GameObject>());
            var recipe = new Recipe { PresentationId = "missing", Radius = 0.5f };
            ProjectileView view = factory.Create(recipe, null);
            try
            {
                Assert.IsTrue(view.ScaleToRadius);
                Assert.AreEqual(1f, view.transform.localScale.x, 1e-4f);
            }
            finally
            {
                if (view != null)
                    Object.DestroyImmediate(view.gameObject);
            }
        }

        [Test]
        public void KnownPresentationId_KeepsAuthoredPrefabScale()
        {
            GameObject prefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            prefab.transform.localScale = new Vector3(2f, 2f, 2f);
            prefab.SetActive(false);
            var factory = new PrefabViewFactory(new Dictionary<string, GameObject>
            {
                ["cube"] = prefab
            });
            var recipe = new Recipe { PresentationId = "cube", Radius = 0.25f };
            ProjectileView view = factory.Create(recipe, null);
            try
            {
                Assert.IsFalse(view.ScaleToRadius);
                Assert.AreEqual(new Vector3(2f, 2f, 2f), view.transform.localScale);
            }
            finally
            {
                if (view != null)
                    Object.DestroyImmediate(view.gameObject);
                Object.DestroyImmediate(prefab);
            }
        }
    }
}
