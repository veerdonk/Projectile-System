using System;
using Veerdonk.ProjectileSystem;
using NUnit.Framework;

namespace Sandbox.Editor.Tests
{
    public sealed class SampleModifierCatalogTests
    {
        [Test]
        public void OnHitCatalog_ContainsPierce_NotHoming()
        {
            Type[] onHit = SampleModifierCatalog.ModifiersFor(RecipeStage.OnHit);
            Assert.IsTrue(Contains(onHit, typeof(Pierce)));
            Assert.IsFalse(Contains(onHit, typeof(Homing)));
        }

        [Test]
        public void OnSpawnCatalog_DoesNotContainPierce()
        {
            Type[] onSpawn = SampleModifierCatalog.ModifiersFor(RecipeStage.OnSpawn);
            Assert.IsFalse(Contains(onSpawn, typeof(Pierce)));
            Assert.IsTrue(Contains(onSpawn, typeof(Interval)));
            Assert.IsTrue(Contains(onSpawn, typeof(Multicast)));
        }

        [Test]
        public void OnTickCatalog_ContainsHoming_NotPierce()
        {
            Type[] onTick = SampleModifierCatalog.ModifiersFor(RecipeStage.OnTick);
            Assert.IsTrue(Contains(onTick, typeof(Homing)));
            Assert.IsFalse(Contains(onTick, typeof(Pierce)));
        }

        [Test]
        public void Create_UsesPhaseDefaults()
        {
            var child = new Recipe { Id = "straight", Speed = 20f, LifetimeSeconds = 5f };
            var pierce = (Pierce)SampleModifierCatalog.Create(typeof(Pierce), child);
            var bounce = (Bounce)SampleModifierCatalog.Create(typeof(Bounce), child);
            var interval = (Interval)SampleModifierCatalog.Create(typeof(Interval), child);
            var homing = (Homing)SampleModifierCatalog.Create(typeof(Homing), child);
            var speed = (Speed)SampleModifierCatalog.Create(typeof(Speed), child);
            var multiply = (Multiply)SampleModifierCatalog.Create(typeof(Multiply), child);
            var multicast = (Multicast)SampleModifierCatalog.Create(typeof(Multicast), child);
            var area = (AreaEffect)SampleModifierCatalog.Create(typeof(AreaEffect), child);
            var field = (GroundField)SampleModifierCatalog.Create(typeof(GroundField), child);
            var apply = (ApplyStatus)SampleModifierCatalog.Create(typeof(ApplyStatus), child);

            Assert.AreEqual(1, pierce.Count);
            Assert.AreEqual(1, bounce.Count);
            Assert.AreEqual(0.5f, interval.Seconds);
            Assert.AreEqual(100f, homing.TurnRateDegreesPerSecond);
            Assert.AreEqual(1f, speed.Scale);
            Assert.AreEqual(2, multiply.Count);
            Assert.AreEqual(2f, multiply.Spread);
            Assert.IsInstanceOf<SquarePattern>(multiply.Pattern);
            Assert.AreSame(child, multiply.ChildRecipe);
            Assert.AreEqual(2, multicast.Count);
            Assert.AreSame(child, multicast.ExtraRecipe);
            Assert.AreEqual(3f, area.Damage);
            Assert.AreEqual(2.25f, field.Radius);
            Assert.AreEqual(2f, field.LifetimeSeconds);
            Assert.AreEqual(3f, field.Damage);
            Assert.AreEqual(0.5f, field.IntervalSeconds);
            Assert.IsInstanceOf<DummyStatusEffect>(apply.Effect);
        }

        [Test]
        public void Create_AreaEffect_SetsWorkingEffectRadiusWhenZero()
        {
            var working = new Recipe { EffectRadius = 0f };
            SampleModifierCatalog.Create(typeof(AreaEffect), null, working);
            Assert.AreEqual(3f, working.EffectRadius);
        }

        [Test]
        public void AppendPierce_ToEmptyOnHit_LeavesAssetUnrelated()
        {
            var recipe = new Recipe();
            SampleModifierCatalog.Append(
                recipe,
                RecipeStage.OnHit,
                SampleModifierCatalog.Create(typeof(Pierce), null));

            Assert.AreEqual(1, recipe.OnHit.Length);
            var pierce = recipe.OnHit[0] as Pierce;
            Assert.IsNotNull(pierce);
            Assert.AreEqual(1, pierce.Count);
            Assert.AreEqual(0, recipe.OnTick.Length);
        }

        static bool Contains(Type[] types, Type match)
        {
            for (int i = 0; i < types.Length; i++)
            {
                if (types[i] == match)
                    return true;
            }

            return false;
        }
    }
}
