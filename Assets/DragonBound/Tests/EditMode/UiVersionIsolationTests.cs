using System.Collections.Generic;
using DragonBound.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DragonBound.Tests.EditMode
{
    public sealed class UiVersionIsolationTests
    {
        [Test]
        public void RegistryResolvesLogicalKeysWithoutProjectPaths()
        {
            var registry = ScriptableObject.CreateInstance<UiAssetRegistry>();
            var texture = new Texture2D(1, 1);
            try
            {
                var entry = new UiAssetRegistry.Entry();
                entry.Configure("Main/Signin/Today", new Object[] { texture });
                registry.Configure("V2", new List<UiAssetRegistry.Entry> { entry });

                Assert.AreSame(texture, registry.Load<Texture2D>("Main/Signin/Today"));
                CollectionAssert.AreEqual(new[] { texture }, registry.LoadAll<Texture2D>(string.Empty));
            }
            finally
            {
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(registry);
            }
        }

        [Test]
        public void RegistryReturnsPrefabRootWhenNestedGameObjectIsListedFirst()
        {
            var root = new GameObject("ProfileImg");
            var child = new GameObject("ProfileFire");
            child.transform.SetParent(root.transform, false);
            var registry = ScriptableObject.CreateInstance<UiAssetRegistry>();
            try
            {
                var entry = new UiAssetRegistry.Entry();
                entry.Configure("prefabs/ProfileImg", new Object[] { child, root });
                registry.Configure("V2", new List<UiAssetRegistry.Entry> { entry });

                Assert.AreSame(root, registry.Load<GameObject>("prefabs/ProfileImg"));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(registry);
            }
        }

        [Test]
        public void ExplicitBindingSurvivesACompletelyDifferentLayout()
        {
            var root = new GameObject("V2Root");
            var deeplyMovedTarget = new GameObject("AnyDesignerChosenName").transform;
            deeplyMovedTarget.SetParent(root.transform, false);
            try
            {
                var map = root.AddComponent<UiNodeBindingMap>();
                map.Configure(
                    new[] { "Main/Signin/CloseButton" },
                    new[] { deeplyMovedTarget });

                Assert.AreSame(deeplyMovedTarget, root.transform.FindUi("Main/Signin/CloseButton"));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void V1RegistryLoadsRepresentativeMovedAssetsIndependentlyOfActiveScene()
        {
            var registry = AssetDatabase.LoadAssetAtPath<UiAssetRegistry>(
                UiVariantProjectPaths.V1Root + "/Config/UiAssetRegistryV1.asset");

            Assert.IsNotNull(registry);
            Assert.IsNotNull(registry.Load<Sprite>("Main/Signin/Today"));
            Assert.IsNotNull(registry.Load<GameObject>("prefabs/Hero"));
            Assert.IsNotNull(registry.Load<RuntimeAnimatorController>("Animation/BossW06"));
            Assert.AreEqual("V1", registry.VariantId);
        }

        [Test]
        public void SharedClientConfigurationLoadsOutsideVariantUiRegistries()
        {
            Assert.IsNotNull(Resources.Load<ScriptableObject>("Configuration/ClientServiceConfig"));
            Assert.IsNotNull(Resources.Load<TextAsset>("Configuration/Stages/stage-001"));
        }
    }
}
