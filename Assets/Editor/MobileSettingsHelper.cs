#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;

namespace CampusTour.Editor
{
    public static class MobileSettingsHelper
    {
        [MenuItem("Tools/Campus VR Tour/Apply Android Mobile Settings")]
        public static void ApplyAndroidSettings()
        {
            // Switch Build Target to Android if needed
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                bool proceed = EditorUtility.DisplayDialog(
                    "Switch Target to Android",
                    "The current platform is " + EditorUserBuildSettings.activeBuildTarget + ". Switch active build target to Android?",
                    "Yes", "No");

                if (proceed)
                {
                    EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
                }
            }

            // Scripting Backend: IL2CPP
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);

            // Target Architectures: ARM64
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

            // Minimum SDK version 24 (Android 7.0 Nougat)
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;

            // Target SDK version: 34 (Android 14)
            PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)34;

            // Screen Orientation: Landscape Left (suitable for 360 and VR)
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;

            // Internet & Audio Access
            PlayerSettings.Android.forceInternetPermission = true;

            // Ensure Skybox/Panoramic shader is in Always Included Shaders (prevents black skybox in APK)
            EnsureSkyboxShaderIncluded();

            // Ensure Default Render Pipeline is set
            EnsureDefaultRenderPipeline();

            AssetDatabase.SaveAssets();

            Debug.Log("[CampusTour] Successfully applied Android Mobile Settings: IL2CPP, ARM64, Min SDK 24, Skybox Shader Included, Landscape orientation.");
            EditorUtility.DisplayDialog("Android Settings Applied", "Android Mobile Settings configured:\n- Scripting Backend: IL2CPP\n- Architecture: ARM64\n- Min API: 24 (Android 7.0)\n- Skybox/Panoramic Shader: Included in Build\n- Orientation: Landscape Left / Right", "OK");
        }

        private static void EnsureSkyboxShaderIncluded()
        {
            Shader skyboxShader = Shader.Find("Skybox/Panoramic");
            if (skyboxShader == null) return;

            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (assets == null || assets.Length == 0) return;

            SerializedObject graphicsSettingsObj = new SerializedObject(assets[0]);
            SerializedProperty alwaysIncludedShaders = graphicsSettingsObj.FindProperty("m_AlwaysIncludedShaders");
            if (alwaysIncludedShaders == null) return;

            bool alreadyIncluded = false;
            for (int i = 0; i < alwaysIncludedShaders.arraySize; i++)
            {
                SerializedProperty item = alwaysIncludedShaders.GetArrayElementAtIndex(i);
                if (item.objectReferenceValue == skyboxShader)
                {
                    alreadyIncluded = true;
                    break;
                }
            }

            if (!alreadyIncluded)
            {
                int newIndex = alwaysIncludedShaders.arraySize;
                alwaysIncludedShaders.InsertArrayElementAtIndex(newIndex);
                alwaysIncludedShaders.GetArrayElementAtIndex(newIndex).objectReferenceValue = skyboxShader;
                graphicsSettingsObj.ApplyModifiedProperties();
                Debug.Log("[CampusTour] Added Skybox/Panoramic shader to Always Included Shaders.");
            }
        }

        private static void EnsureDefaultRenderPipeline()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (assets == null || assets.Length == 0) return;

            SerializedObject graphicsSettingsObj = new SerializedObject(assets[0]);
            SerializedProperty srpProperty = graphicsSettingsObj.FindProperty("m_CustomRenderPipeline");
            if (srpProperty != null && srpProperty.objectReferenceValue == null)
            {
                RenderPipelineAsset mobileUrpAsset = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset");
                if (mobileUrpAsset == null)
                {
                    string[] guids = AssetDatabase.FindAssets("t:RenderPipelineAsset");
                    if (guids.Length > 0)
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                        mobileUrpAsset = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(path);
                    }
                }

                if (mobileUrpAsset != null)
                {
                    srpProperty.objectReferenceValue = mobileUrpAsset;
                    graphicsSettingsObj.ApplyModifiedProperties();
                    Debug.Log($"[CampusTour] Assigned Default Render Pipeline Asset: {mobileUrpAsset.name}");
                }
            }
        }
    }
}
#endif
