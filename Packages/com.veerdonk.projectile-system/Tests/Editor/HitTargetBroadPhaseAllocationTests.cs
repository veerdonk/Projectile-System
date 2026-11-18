using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is =UnityEngine.TestTools.Constraints.Is;

namespace Veerdonk.ProjectileSystem.Editor.Tests
{
    public sealed class HitTargetBroadPhaseAllocationTests
    {
        [Test]
        public void SteadyStateMovesAndQueries_DoNotAllocate()
        {
            var catalog = new HitTargetCatalog();
            for (int i = 0; i < 400; i++)
                catalog.SetCapsule(new TargetId(i), new Vector3((i % 20) * 2f + 1.5f, 0f, (i / 20) * 2f + 0.5f), 0.5f, 1.8f, 1.2f);

            var results = new List<HitInfo>(1024);
            var disk = new GroundDisk(new Vector3(10f, 0f, 10f), Vector3.up, 4f, 0f, Vector3.forward);
            GroundDisk sector = GroundDisk.Shaped(
                new Vector3(20f, 0f, 20f),
                GroundDisk.SanitizeNormal(new Vector3(0.2f, 1f, 0.1f)),
                Vector3.right,
                new GroundFieldSize(GroundFieldShape.Sector, 0f, 6f, 0f, 90f),
                0.5f);

            void Step(int frame)
            {
                float shift = (frame & 1) == 0 ? 1f : 0f;
                for (int i = 0; i < 400; i++)
                    catalog.SetCapsule(new TargetId(i), new Vector3((i % 20) * 2f + 1.5f + shift, 0f, (i / 20) * 2f + 0.5f), 0.5f, 1.8f, 1.2f);

                for (int k = 0; k < 20; k++)
                {
                    var shot = new ProjectileId(k + 1);
                    catalog.TrySweep(shot, new Vector3(k * 2f + 1.2f, 1f, 1f), Vector3.forward, 0.8f, 0.1f, out _, out _, out _);
                    catalog.TrySweep(shot, new Vector3(-5f, 1f, k * 2f), new Vector3(1f, 0f, 1f), 300f, 0.1f, out _, out _, out _);
                }

                results.Clear();
                catalog.AppendSphere(new ProjectileId(1), new Vector3(15f, 1f, 15f), 3f, results);
                catalog.AppendDisk(new ProjectileId(1), disk, results);
                catalog.AppendDisk(new ProjectileId(1), sector, results);
            }

            for (int frame = 0; frame < 4; frame++)
                Step(frame);

            Assert.That(() =>
            {
                for (int frame = 0; frame < 8; frame++)
                    Step(frame);
            }, Is.Not.AllocatingGCMemory());
        }
    }
}
