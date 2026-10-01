using System.Collections.Generic;
using Naiwa.Growth;
using NUnit.Framework;

namespace Naiwa.Tests
{
    public class GrowthServiceLiteTests
    {
        static GrowthService Create(List<FormId> requests)
        {
            var g = new GrowthService(3000, 25000);
            g.EvolutionRequested += f => requests.Add(f);
            return g;
        }

        [Test]
        public void Growth2999_StaysEgg()
        {
            var req = new List<FormId>();
            var g = Create(req);
            g.Add(2999);
            Assert.AreEqual(FormId.Egg, g.Form);
            Assert.AreEqual(FormId.Egg, g.TargetForm);
            Assert.IsFalse(g.HasPendingEvolution);
            Assert.IsEmpty(req);
        }

        [Test]
        public void Growth3000_RequestsSmall()
        {
            var req = new List<FormId>();
            var g = Create(req);
            g.Add(2999);
            g.Add(1);
            CollectionAssert.AreEqual(new[] { FormId.Small }, req);
            Assert.IsTrue(g.HasPendingEvolution);

            g.CommitEvolution();
            Assert.AreEqual(FormId.Small, g.Form);
            Assert.IsFalse(g.HasPendingEvolution);
            CollectionAssert.AreEqual(new[] { FormId.Small }, req);
        }

        [Test]
        public void Add30000FromZero_RequestsSmallThenBig()
        {
            var req = new List<FormId>();
            var g = Create(req);
            g.Add(30000);
            CollectionAssert.AreEqual(new[] { FormId.Small }, req);
            Assert.AreEqual(FormId.Egg, g.Form, "不能跳过奶蛋→小奶蛙");

            g.CommitEvolution();
            CollectionAssert.AreEqual(new[] { FormId.Small, FormId.Big }, req);
            Assert.AreEqual(FormId.Small, g.Form);

            g.CommitEvolution();
            Assert.AreEqual(FormId.Big, g.Form);
            Assert.IsFalse(g.HasPendingEvolution);
            Assert.AreEqual(2, req.Count);
        }

        [Test]
        public void AfterBig_AddingNeverRequests()
        {
            var req = new List<FormId>();
            var g = Create(req);
            g.Add(25000);
            g.CommitEvolution();
            g.CommitEvolution();
            req.Clear();

            g.Add(100000);
            Assert.IsEmpty(req);
            Assert.AreEqual(FormId.Big, g.Form);
            Assert.AreEqual(125000, g.Growth);
            Assert.IsNull(g.NextThreshold);
        }

        [Test]
        public void RepeatedAddsWhilePending_RequestOnlyOnce()
        {
            var req = new List<FormId>();
            var g = Create(req);
            g.Add(3000);
            g.Add(10);
            g.Add(10);
            Assert.AreEqual(1, req.Count);
        }
    }
}
