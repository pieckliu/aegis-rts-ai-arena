using NUnit.Framework;

public sealed class ArenaGameRulesTests
{
    [Test]
    public void Spend_DeductsCost_WhenAffordable()
    {
        Assert.AreEqual(350, ArenaGameRules.Spend(500, 150));
    }

    [Test]
    public void Spend_DoesNotChangeResources_WhenUnaffordable()
    {
        Assert.AreEqual(40, ArenaGameRules.Spend(40, 50));
    }

    [Test]
    public void ApplyDamage_ClampsAtZero()
    {
        Assert.AreEqual(0, ArenaGameRules.ApplyDamage(10, 20));
    }

    [Test]
    public void ApplyIncome_IgnoresNegativeIncome()
    {
        Assert.AreEqual(100, ArenaGameRules.ApplyIncome(100, -10));
    }

    [Test]
    public void CanQueue_RejectsFullQueue()
    {
        Assert.IsFalse(ArenaGameRules.CanQueue(5, 5, 500, 50));
    }

    [Test]
    public void CanQueue_AcceptsAvailableSlotAndResources()
    {
        Assert.IsTrue(ArenaGameRules.CanQueue(2, 5, 100, 50));
    }
}
