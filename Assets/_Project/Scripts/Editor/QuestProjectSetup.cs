using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.Interactions;

namespace JPHACKS2026.Editor
{
    /// <summary>
    /// Meta Quest 向けのビルド設定と OpenXR (Meta XR) の設定をまとめて行う。
    /// Android は実機ビルド用、Standalone は Quest Link で Play モード確認する用。
    /// </summary>
    public static class QuestProjectSetup
    {
        const string XRSettingsPath = "Assets/XR/XRGeneralSettingsPerBuildTarget.asset";
        const string OpenXRLoaderTypeName = "UnityEngine.XR.OpenXR.OpenXRLoader";
        const string MetaXRFeatureSetId = "com.meta.openxr.featureset.metaxr";

        static readonly BuildTargetGroup[] XRTargetGroups = { BuildTargetGroup.Android, BuildTargetGroup.Standalone };

        [MenuItem("JPHACKS2026/Setup/Configure Meta Quest Project")]
        public static void Configure()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

            ConfigurePlayerSettings();

            foreach (var group in XRTargetGroups)
            {
                AssignOpenXRLoader(group);
                EnableOpenXRFeatures(group);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[QuestProjectSetup] Meta Quest 向けの設定が完了しました。");
        }

        static void ConfigurePlayerSettings()
        {
            var android = NamedBuildTarget.Android;
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel32;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
            PlayerSettings.SetMobileMTRendering(android, true);
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;
        }

        static void AssignOpenXRLoader(BuildTargetGroup group)
        {
            var perBuildTarget = GetOrCreateXRSettings();
            if (!perBuildTarget.HasManagerSettingsForBuildTarget(group))
                perBuildTarget.CreateDefaultManagerSettingsForBuildTarget(group);

            var settings = perBuildTarget.SettingsForBuildTarget(group);
            settings.InitManagerOnStart = true;
            if (!XRPackageMetadataStore.IsLoaderAssigned(OpenXRLoaderTypeName, group))
                XRPackageMetadataStore.AssignLoader(settings.Manager, OpenXRLoaderTypeName, group);
            EditorUtility.SetDirty(settings);
        }

        static XRGeneralSettingsPerBuildTarget GetOrCreateXRSettings()
        {
            if (EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out XRGeneralSettingsPerBuildTarget existing) && existing != null)
                return existing;

            Directory.CreateDirectory(Path.GetDirectoryName(XRSettingsPath));
            var created = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
            AssetDatabase.CreateAsset(created, XRSettingsPath);
            EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, created, true);
            return created;
        }

        static void EnableOpenXRFeatures(BuildTargetGroup group)
        {
            var featureSet = OpenXRFeatureSetManager.GetFeatureSetWithId(group, MetaXRFeatureSetId);
            if (featureSet == null)
            {
                Debug.LogError($"[QuestProjectSetup] Meta XR の OpenXR Feature Group が見つかりません ({group})");
                return;
            }
            featureSet.isEnabled = true;
            OpenXRFeatureSetManager.SetFeaturesFromEnabledFeatureSets(group);

            // コントローラー入力のインタラクションプロファイル（Quest 2/Pro と Quest 3/3S）
            var openXRSettings = OpenXRSettings.GetSettingsForBuildTargetGroup(group);
            openXRSettings.GetFeature<OculusTouchControllerProfile>().enabled = true;
            openXRSettings.GetFeature<MetaQuestTouchPlusControllerProfile>().enabled = true;
            EditorUtility.SetDirty(openXRSettings);
        }
    }
}
