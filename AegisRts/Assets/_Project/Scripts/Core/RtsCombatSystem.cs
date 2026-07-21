using System;
using System.Collections.Generic;
using UnityEngine;

internal sealed class RtsCombatSystem
{
    private readonly RtsGameConfig config;
    private readonly IList<BuildingData> buildings;
    private readonly IList<UnitData> units;
    private readonly Action<UnitData, Vector2> moveTowards;
    private readonly RtsEntityLifecycle lifecycle;

    public RtsCombatSystem(
        RtsGameConfig gameConfig,
        IList<BuildingData> buildingList,
        IList<UnitData> unitList,
        Action<UnitData, Vector2> movement,
        RtsEntityLifecycle entityLifecycle
    )
    {
        config = gameConfig;
        buildings = buildingList;
        units = unitList;
        moveTowards = movement;
        lifecycle = entityLifecycle;
    }

    public void Tick(float deltaTime)
    {
        UnitData[] snapshot = new List<UnitData>(units).ToArray();

        foreach (UnitData unit in snapshot)
        {
            if (unit == null || !units.Contains(unit))
            {
                continue;
            }

            TryAcquireTarget(unit);

            if (unit.AttackUnitTarget != null)
            {
                AttackUnit(unit, deltaTime);
            }
            else if (unit.AttackTarget != null)
            {
                AttackBuilding(unit, deltaTime);
            }
        }
    }

    private void TryAcquireTarget(UnitData source)
    {
        UnitData nearest = null;
        float nearestDistance = float.MaxValue;

        foreach (UnitData candidate in units)
        {
            if (candidate == null || candidate.Team == source.Team)
            {
                continue;
            }

            float distance = Vector2.Distance(source.Position, candidate.Position);

            if (distance <= config.UnitAggroRange && distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = candidate;
            }
        }

        if (nearest == null)
        {
            return;
        }

        source.AttackUnitTarget = nearest;
        source.AttackTarget = null;
        source.IsMoving = false;
        source.Waypoints.Clear();
    }

    private void AttackUnit(UnitData attacker, float deltaTime)
    {
        UnitData target = attacker.AttackUnitTarget;

        if (target == null || !units.Contains(target))
        {
            attacker.AttackUnitTarget = null;
            return;
        }

        if (Vector2.Distance(attacker.Position, target.Position) > attacker.AttackRange)
        {
            moveTowards(attacker, target.Position);
            return;
        }

        attacker.AttackTimer -= deltaTime;

        if (attacker.AttackTimer > 0f)
        {
            return;
        }

        attacker.AttackTimer = attacker.AttackCooldown;
        target.HitPoints = ArenaGameRules.ApplyDamage(target.HitPoints, attacker.AttackDamage);

        if (target.HitPoints <= 0)
        {
            lifecycle.DestroyUnit(target);
            attacker.AttackUnitTarget = null;
        }
    }

    private void AttackBuilding(UnitData attacker, float deltaTime)
    {
        BuildingData target = attacker.AttackTarget;

        if (target == null || !buildings.Contains(target))
        {
            attacker.AttackTarget = null;
            return;
        }

        if (Vector2.Distance(attacker.Position, target.Position) > attacker.AttackRange)
        {
            moveTowards(attacker, target.Position);
            return;
        }

        attacker.AttackTimer -= deltaTime;

        if (attacker.AttackTimer > 0f)
        {
            return;
        }

        attacker.AttackTimer = attacker.AttackCooldown;
        target.HitPoints = ArenaGameRules.ApplyDamage(target.HitPoints, attacker.AttackDamage);

        if (target.HitPoints <= 0)
        {
            lifecycle.DestroyBuilding(target);
            attacker.AttackTarget = null;
        }
    }
}
