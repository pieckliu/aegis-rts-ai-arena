using UnityEngine;

public static class ArenaGameRules
{
    public static bool CanAfford(int resources, int cost)
    {
        return cost >= 0 && resources >= cost;
    }

    public static int Spend(int resources, int cost)
    {
        return CanAfford(resources, cost) ? resources - cost : resources;
    }

    public static int ApplyDamage(int hitPoints, int damage)
    {
        return Mathf.Max(0, hitPoints - Mathf.Max(0, damage));
    }

    public static int ApplyIncome(int resources, int income)
    {
        return resources + Mathf.Max(0, income);
    }

    public static bool CanQueue(int queueCount, int maxQueueSize, int resources, int cost)
    {
        return queueCount >= 0 &&
            maxQueueSize > 0 &&
            queueCount < maxQueueSize &&
            CanAfford(resources, cost);
    }
}
