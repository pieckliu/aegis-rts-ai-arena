using NUnit.Framework;
using UnityEngine;

public sealed class RtsGameConfigTests
{
    [Test]
    public void DefaultConfigurationAsset_LoadsFromResources()
    {
        RtsGameConfig config = Resources.Load<RtsGameConfig>("RtsGameConfig");

        Assert.IsNotNull(config);
        Assert.IsTrue(config.IsValid());
    }

    [Test]
    public void DefaultConfiguration_IsValid()
    {
        RtsGameConfig config = ScriptableObject.CreateInstance<RtsGameConfig>();

        Assert.IsTrue(config.IsValid());

        Object.DestroyImmediate(config);
    }

    [Test]
    public void Configuration_RejectsInvalidMapSize()
    {
        RtsGameConfig config = ScriptableObject.CreateInstance<RtsGameConfig>();
        config.MapSize = 0;

        Assert.IsFalse(config.IsValid());

        Object.DestroyImmediate(config);
    }

    [Test]
    public void Configuration_RejectsInvertedCameraRange()
    {
        RtsGameConfig config = ScriptableObject.CreateInstance<RtsGameConfig>();
        config.MinCameraSize = 12f;
        config.MaxCameraSize = 6f;

        Assert.IsFalse(config.IsValid());

        Object.DestroyImmediate(config);
    }

    [Test]
    public void Configuration_RejectsInitialCameraOutsideRange()
    {
        RtsGameConfig config = ScriptableObject.CreateInstance<RtsGameConfig>();
        config.InitialCameraSize = config.MaxCameraSize + 1f;

        Assert.IsFalse(config.IsValid());

        Object.DestroyImmediate(config);
    }
}
