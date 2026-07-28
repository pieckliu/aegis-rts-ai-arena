using UnityEngine;

[CreateAssetMenu(fileName = "RtsGameConfig", menuName = "Aegis RTS/Game Config")]
public sealed class RtsGameConfig : ScriptableObject
{
    [Header("Map")]
    public int MapSize = 48;
    public float CellSize = 1f;

    [Header("Buildings")]
    public float BaseRadius = 0.45f;
    public float BuildRadius = 7f;
    public float BuildingRadius = 0.42f;
    public float InfantryTrainingTime = 3f;
    public int MaxFactoryQueueSize = 5;
    public int PlayerBaseHitPoints = 500;
    public int FactoryHitPoints = 300;
    public int EnemyBaseHitPoints = 400;

    [Header("Player Infantry")]
    public int InfantryAttackDamage = 20;
    public float InfantryAttackRange = 1.2f;
    public float InfantryAttackCooldown = 1f;
    public int PlayerInfantryHitPoints = 100;

    [Header("Enemy AI")]
    public int EnemyInfantryHitPoints = 80;
    public float UnitAggroRange = 4f;
    public float EnemySpawnInterval = 15f;
    public int EnemyInfantryAttackDamage = 10;
    public float EnemyInfantryAttackRange = 1.2f;
    public float EnemyInfantryAttackCooldown = 1.2f;

    [Header("Economy")]
    public int StartingResources = 500;
    public int FactoryCost = 150;
    public int InfantryCost = 50;
    public int PassiveResourceIncome = 10;
    public float PassiveResourceInterval = 5f;

    [Header("Movement and Camera")]
    public float InfantryRadius = 0.42f;
    public float UnitCollisionPadding = 0.04f;
    public float UnitMoveSpeed = 5f;
    public float CameraMoveSpeed = 14f;
    public float CameraZoomSpeed = 3f;
    public float MinCameraSize = 6f;
    public float InitialCameraSize = 6f;
    public float MaxCameraSize = 26f;
    [Range(0f, 1f)] public float InitialCameraInwardBias = 0.25f;
    public float DragSelectThreshold = 10f;

    [Header("Visibility")]
    public float BuildingSightRange = 6f;
    public float UnitSightRange = 5f;
    public float EnemyLastKnownDuration = 8f;

    public bool IsValid()
    {
        return MapSize > 0 &&
            CellSize > 0f &&
            InfantryTrainingTime > 0f &&
            MaxFactoryQueueSize > 0 &&
            PlayerBaseHitPoints > 0 &&
            EnemyBaseHitPoints > 0 &&
            InfantryAttackDamage >= 0 &&
            InfantryAttackRange > 0f &&
            InfantryAttackCooldown > 0f &&
            StartingResources >= 0 &&
            FactoryCost >= 0 &&
            InfantryCost >= 0 &&
            PassiveResourceInterval > 0f &&
            UnitCollisionPadding >= 0f &&
            UnitMoveSpeed > 0f &&
            MinCameraSize > 0f &&
            InitialCameraSize >= MinCameraSize &&
            InitialCameraSize <= MaxCameraSize &&
            MaxCameraSize >= MinCameraSize &&
            InitialCameraInwardBias >= 0f &&
            InitialCameraInwardBias <= 1f &&
            BuildingSightRange > 0f &&
            UnitSightRange > 0f &&
            EnemyLastKnownDuration > 0f;
    }
}
