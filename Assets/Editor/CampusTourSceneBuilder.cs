using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

namespace CampusTour.Editor
{
    /// <summary>
    /// Automated scene builder for the SVKM NMIMS Campus VR Tour.
    /// Creates or updates Assets/Scenes/CampusTour.unity with complete panoramic camera rig,
    /// UI hierarchy, New Input System EventSystem, hotspot prefabs, and tour management.
    /// </summary>
    public static class CampusTourSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/CampusTour.unity";
        private const string ScenesFolder = "Assets/Scenes";
        private const string PrefabsFolder = "Assets/Prefabs";
        private const string HotspotPrefabPath = "Assets/Prefabs/CampusHotspot.prefab";
        private const string MaterialPath = "Assets/Materials/M_CampusPanorama.mat";

        [MenuItem("Tools/Campus VR Tour/Build Scene", false, 10)]
        public static void BuildTourScene()
        {
            Debug.Log("[CampusTourSceneBuilder] Starting Campus VR Tour Scene Generation...");

            // 1. Ensure required directory structure exists
            EnsureFolders();

            // 2. Create or load the scene
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 3. Load font
            Font legacyFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (legacyFont == null)
            {
                legacyFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            // 4. Create Material for Panorama
            Material panoMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (panoMaterial == null)
            {
                Shader shader = Shader.Find("CampusTour/Panorama360");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) shader = Shader.Find("Unlit/Texture");

                if (shader != null)
                {
                    panoMaterial = new Material(shader) { name = "M_CampusPanorama" };
                    AssetDatabase.CreateAsset(panoMaterial, MaterialPath);
                }
            }

            // 5. Build Hotspot Prefab
            GameObject hotspotPrefab = BuildOrLoadHotspotPrefab(legacyFont);

            // 6. Create Camera Rig
            GameObject cameraObj = new GameObject("Main Camera");
            cameraObj.tag = "MainCamera";
            cameraObj.transform.position = Vector3.zero;
            cameraObj.transform.rotation = Quaternion.identity;

            Camera cam = cameraObj.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 1000f;
            cam.fieldOfView = 75f;

            // Ensure URP Camera Data
            var urpCamData = cameraObj.GetComponent<UniversalAdditionalCameraData>();
            if (urpCamData == null)
            {
                urpCamData = cameraObj.AddComponent<UniversalAdditionalCameraData>();
            }
            urpCamData.renderPostProcessing = false;

            // Add AudioListener
            cameraObj.AddComponent<AudioListener>();

            // Add CampusTourController
            CampusTourController controller = cameraObj.AddComponent<CampusTourController>();
            controller.cameraTransform = cameraObj.transform;

            // Add VRModeManager & Gaze Reticle
            VRModeManager vrManager = cameraObj.AddComponent<VRModeManager>();
            vrManager.mainCamera = cam;
            vrManager.tourController = controller;

            // Pure joystick button controlling — all gaze reticle / square cursors completely removed
            vrManager.gazeReticle = null;
            vrManager.gazeProgressRing = null;

            // Try attach TrackedPoseDriver for OpenXR / Quest 6DOF tracking
            AttachTrackedPoseDriver(cameraObj);

            // 7. Create Panorama Renderer Sphere
            GameObject panoObj = new GameObject("PanoramaSphere");
            panoObj.transform.position = Vector3.zero;
            panoObj.transform.rotation = Quaternion.identity;
            PanoramaRenderer panoRenderer = panoObj.AddComponent<PanoramaRenderer>();
            panoRenderer.targetCamera = cameraObj.transform;
            panoRenderer.panoramaMaterial = panoMaterial;
            panoObj.GetComponent<MeshRenderer>().sharedMaterial = panoMaterial;
            panoRenderer.BuildInwardSphereMesh();

            // 8. Create Video Player System
            GameObject videoObj = new GameObject("VideoPanoramaPlayer");
            videoObj.transform.position = Vector3.zero;
            VideoPanoramaPlayer vidPlayer = videoObj.AddComponent<VideoPanoramaPlayer>();
            vidPlayer.panoramaRenderer = panoRenderer;

            // 9. Create UI Canvas Hierarchy
            GameObject canvasObj = new GameObject("CampusTourCanvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();

            CampusTourUI tourUI = BuildUIHierarchy(canvasObj, legacyFont);

            // 10. Create EventSystem with New Input System Module
            GameObject eventSystemObj = new GameObject("EventSystem");
            eventSystemObj.AddComponent<EventSystem>();
            eventSystemObj.AddComponent<InputSystemUIInputModule>();

            // 11. Create CampusTourManager & Quest Joystick Controller
            GameObject managerObj = new GameObject("CampusTourManager");
            CampusTourManager manager = managerObj.AddComponent<CampusTourManager>();
            managerObj.AddComponent<QuestJoystickController>();
            manager.panoramaRenderer = panoRenderer;
            manager.tourController = controller;
            manager.vrManager = vrManager;
            manager.tourUI = tourUI;
            manager.videoPlayer = vidPlayer;
            manager.cameraTransform = cameraObj.transform;
            if (hotspotPrefab != null)
            {
                manager.hotspotPrefab = hotspotPrefab.GetComponent<CampusHotspot>();
            }

            GameObject hotspotContainer = new GameObject("HotspotsContainer");
            hotspotContainer.transform.position = Vector3.zero;
            manager.hotspotContainer = hotspotContainer.transform;

            manager.InitializeDefaultLocations();

            // 12. Save Scene
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[CampusTourSceneBuilder] Scene saved to {ScenePath}");

            // 13. Add Scene to Build Settings without removing existing scenes
            AddSceneToBuildSettings(ScenePath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[CampusTourSceneBuilder] Scene build completed successfully! Ready for Editor testing & Meta Quest export.");
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder(ScenesFolder))
            {
                AssetDatabase.CreateFolder("Assets", "Scenes");
            }
            if (!AssetDatabase.IsValidFolder(PrefabsFolder))
            {
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            }
            if (!AssetDatabase.IsValidFolder("Assets/Materials"))
            {
                AssetDatabase.CreateFolder("Assets", "Materials");
            }
            if (!AssetDatabase.IsValidFolder("Assets/StreamingAssets"))
            {
                AssetDatabase.CreateFolder("Assets", "StreamingAssets");
            }
            if (!AssetDatabase.IsValidFolder("Assets/StreamingAssets/Images"))
            {
                AssetDatabase.CreateFolder("Assets/StreamingAssets", "Images");
            }
            if (!AssetDatabase.IsValidFolder("Assets/StreamingAssets/Videos"))
            {
                AssetDatabase.CreateFolder("Assets/StreamingAssets", "Videos");
            }
        }

        private static GameObject BuildOrLoadHotspotPrefab(Font font)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(HotspotPrefabPath);
            if (existing != null) return existing;

            GameObject root = new GameObject("CampusHotspot");
            SphereCollider col = root.AddComponent<SphereCollider>();
            col.radius = 0.9f;

            CampusHotspot hotspot = root.AddComponent<CampusHotspot>();

            // Visual Root
            GameObject visual = new GameObject("VisualRoot");
            visual.transform.SetParent(root.transform, false);
            hotspot.visualRoot = visual.transform;

            // World-space canvas for crisp billboard UI text & icons
            Canvas wCanvas = visual.AddComponent<Canvas>();
            wCanvas.renderMode = RenderMode.WorldSpace;
            RectTransform wRect = visual.GetComponent<RectTransform>();
            wRect.sizeDelta = new Vector2(200, 200);
            visual.transform.localScale = Vector3.one * 0.012f;

            // Icon Background
            GameObject bgObj = new GameObject("IconBG");
            bgObj.transform.SetParent(visual.transform, false);
            Image bgImg = bgObj.AddComponent<Image>();
            bgImg.color = new Color(0.12f, 0.45f, 0.90f, 0.92f);
            RectTransform bgRect = bgObj.GetComponent<RectTransform>();
            bgRect.sizeDelta = new Vector2(80, 80);
            hotspot.iconBackground = bgImg;

            // Icon Text
            GameObject iconObj = new GameObject("IconText");
            iconObj.transform.SetParent(bgObj.transform, false);
            Text iconTxt = iconObj.AddComponent<Text>();
            iconTxt.font = font;
            iconTxt.fontSize = 50;
            iconTxt.alignment = TextAnchor.MiddleCenter;
            iconTxt.color = Color.white;
            iconTxt.text = "➜";
            RectTransform iconRect = iconObj.GetComponent<RectTransform>();
            iconRect.sizeDelta = new Vector2(80, 80);
            hotspot.iconText = iconTxt;

            // Label Below Icon
            GameObject labelObj = new GameObject("LabelText");
            labelObj.transform.SetParent(visual.transform, false);
            labelObj.transform.localPosition = new Vector3(0, -60, 0);
            Text labelTxt = labelObj.AddComponent<Text>();
            labelTxt.font = font;
            labelTxt.fontSize = 24;
            labelTxt.fontStyle = FontStyle.Bold;
            labelTxt.alignment = TextAnchor.MiddleCenter;
            labelTxt.color = Color.white;
            labelTxt.text = "Go to location";
            RectTransform labelRect = labelObj.GetComponent<RectTransform>();
            labelRect.sizeDelta = new Vector2(260, 45);
            hotspot.labelDisplay = labelTxt;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, HotspotPrefabPath);
            GameObject.DestroyImmediate(root);
            return prefab;
        }



        private static void AttachTrackedPoseDriver(GameObject cameraObj)
        {
            // Unity 6 Input System TrackedPoseDriver support
            Type tpdType = Type.GetType("UnityEngine.InputSystem.XR.TrackedPoseDriver, Unity.InputSystem");
            if (tpdType != null)
            {
                if (cameraObj.GetComponent(tpdType) == null)
                {
                    cameraObj.AddComponent(tpdType);
                    Debug.Log("[CampusTourSceneBuilder] Attached InputSystem TrackedPoseDriver to Camera.");
                }
            }
        }

                        private static CampusTourUI BuildUIHierarchy(GameObject canvasObj, Font font)
        {
            CampusTourUI ui = canvasObj.AddComponent<CampusTourUI>();

            // Root HUD CanvasGroup
            GameObject hudObj = CreateUIElement("HUD_Root", canvasObj.transform);
            RectTransform hudRect = hudObj.GetComponent<RectTransform>();
            SetFullStretch(hudRect);
            CanvasGroup hudGroup = hudObj.AddComponent<CanvasGroup>();
            ui.hudCanvasGroup = hudGroup;

            // 1. Top Location Banner (Clean title at top-center)
            GameObject bannerObj = CreateUIElement("LocationBanner", hudObj.transform);
            Image bannerBg = bannerObj.AddComponent<Image>();
            bannerBg.color = new Color(0.08f, 0.10f, 0.16f, 0.90f);
            RectTransform bannerRect = bannerObj.GetComponent<RectTransform>();
            bannerRect.anchorMin = new Vector2(0.5f, 1f);
            bannerRect.anchorMax = new Vector2(0.5f, 1f);
            bannerRect.pivot = new Vector2(0.5f, 1f);
            bannerRect.anchoredPosition = new Vector2(0f, -30f);
            bannerRect.sizeDelta = new Vector2(740f, 52f);

            Text bannerTxt = CreateText("BannerText", bannerObj.transform, font, 20, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            bannerTxt.text = "Location 1 - Left Side Outside the Main Gate";
            RectTransform bannerTxtRect = bannerTxt.GetComponent<RectTransform>();
            SetFullStretch(bannerTxtRect);
            ui.locationBannerText = bannerTxt;

            // 2. On-screen Menu & Info buttons removed — 100% controlled via Quest Joysticks & physical buttons:
            // Right Controller 'B' = Open/Close Menu | Left Controller 'Y' = Open/Close Info | 'A' & 'X' = Select
            ui.menuToggleButton = null;
            ui.infoButton = null;

            // 3. Location Selection Menu Modal (Docked at Middle-Left)
            GameObject menuModal = CreateUIElement("LocationMenuModal", canvasObj.transform);
            Image menuBg = menuModal.AddComponent<Image>();
            menuBg.color = new Color(0.06f, 0.08f, 0.14f, 0.95f);
            RectTransform menuRect = menuModal.GetComponent<RectTransform>();
            menuRect.anchorMin = new Vector2(0f, 0.5f);
            menuRect.anchorMax = new Vector2(0f, 0.5f);
            menuRect.pivot = new Vector2(0f, 0.5f);
            menuRect.anchoredPosition = new Vector2(50f, 0f);
            menuRect.sizeDelta = new Vector2(520f, 520f);

            Text menuTitle = CreateText("MenuTitle", menuModal.transform, font, 22, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            menuTitle.text = "Select Campus Viewpoint";
            RectTransform mTitleRect = menuTitle.GetComponent<RectTransform>();
            mTitleRect.anchorMin = new Vector2(0f, 1f);
            mTitleRect.anchorMax = new Vector2(1f, 1f);
            mTitleRect.pivot = new Vector2(0.5f, 1f);
            mTitleRect.anchoredPosition = new Vector2(0f, -18f);
            mTitleRect.sizeDelta = new Vector2(0f, 40f);

            // Container for location items
            GameObject listContainer = CreateUIElement("LocationListContainer", menuModal.transform);
            RectTransform listRect = listContainer.GetComponent<RectTransform>();
            listRect.anchorMin = new Vector2(0.05f, 0.16f);
            listRect.anchorMax = new Vector2(0.95f, 0.88f);
            listRect.offsetMin = Vector2.zero;
            listRect.offsetMax = Vector2.zero;

            VerticalLayoutGroup vlg = listContainer.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 10;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth = true;

            Button itemTemplate = CreateStyledButton("LocationItemTemplate", listContainer.transform, font, "1. Location Name", new Vector2(460f, 46f));
            itemTemplate.gameObject.SetActive(false);
            ui.locationItemPrefab = itemTemplate;
            ui.locationListContainer = listContainer.transform;

            ui.closeMenuButton = null;
            Text menuHint = CreateText("MenuCloseHint", menuModal.transform, font, 15, FontStyle.Italic, new Color(0.7f, 0.75f, 0.85f), TextAnchor.MiddleCenter);
            menuHint.text = "Press (B) on Right Controller to Close";
            RectTransform menuHintRect = menuHint.GetComponent<RectTransform>();
            menuHintRect.anchorMin = new Vector2(0f, 0f);
            menuHintRect.anchorMax = new Vector2(1f, 0f);
            menuHintRect.pivot = new Vector2(0.5f, 0f);
            menuHintRect.anchoredPosition = new Vector2(0f, 16f);
            menuHintRect.sizeDelta = new Vector2(0f, 32f);

            ui.locationMenuPanel = menuModal;

            // 4. Viewpoint Info Modal (Centered popup for viewpoint details)
            GameObject infoModal = CreateUIElement("InfoModal", canvasObj.transform);
            Image infoBg = infoModal.AddComponent<Image>();
            infoBg.color = new Color(0.06f, 0.08f, 0.14f, 0.95f);
            RectTransform infoRect = infoModal.GetComponent<RectTransform>();
            infoRect.anchorMin = new Vector2(0.5f, 0.5f);
            infoRect.anchorMax = new Vector2(0.5f, 0.5f);
            infoRect.pivot = new Vector2(0.5f, 0.5f);
            infoRect.sizeDelta = new Vector2(660f, 400f);

            Text infoTitle = CreateText("InfoTitle", infoModal.transform, font, 22, FontStyle.Bold, new Color(0.2f, 0.6f, 1f), TextAnchor.MiddleCenter);
            infoTitle.text = "Location Name";
            RectTransform iTitleRect = infoTitle.GetComponent<RectTransform>();
            iTitleRect.anchorMin = new Vector2(0.05f, 1f);
            iTitleRect.anchorMax = new Vector2(0.95f, 1f);
            iTitleRect.pivot = new Vector2(0.5f, 1f);
            iTitleRect.anchoredPosition = new Vector2(0f, -22f);
            iTitleRect.sizeDelta = new Vector2(0f, 44f);
            ui.infoModalTitle = infoTitle;

            Text infoBody = CreateText("InfoBody", infoModal.transform, font, 18, FontStyle.Normal, Color.white, TextAnchor.UpperLeft);
            infoBody.text = "Detailed campus viewpoint description.";
            RectTransform iBodyRect = infoBody.GetComponent<RectTransform>();
            iBodyRect.anchorMin = new Vector2(0.08f, 0.22f);
            iBodyRect.anchorMax = new Vector2(0.92f, 0.82f);
            iBodyRect.offsetMin = Vector2.zero;
            iBodyRect.offsetMax = Vector2.zero;
            ui.infoModalBody = infoBody;

            ui.closeInfoModalButton = null;
            Text infoHint = CreateText("InfoCloseHint", infoModal.transform, font, 15, FontStyle.Italic, new Color(0.7f, 0.75f, 0.85f), TextAnchor.MiddleCenter);
            infoHint.text = "Press (Y) on Left Controller to Close";
            RectTransform infoHintRect = infoHint.GetComponent<RectTransform>();
            infoHintRect.anchorMin = new Vector2(0f, 0f);
            infoHintRect.anchorMax = new Vector2(1f, 0f);
            infoHintRect.pivot = new Vector2(0.5f, 0f);
            infoHintRect.anchoredPosition = new Vector2(0f, 16f);
            infoHintRect.sizeDelta = new Vector2(0f, 32f);

            ui.infoModalPanel = infoModal;

            // 5. Fullscreen Fade Overlay (Disabled to prevent black screens)
            GameObject fadeObj = CreateUIElement("FadeOverlay", canvasObj.transform);
            Image fadeImg = fadeObj.AddComponent<Image>();
            fadeImg.color = new Color(0f, 0f, 0f, 0f);
            RectTransform fadeRect = fadeObj.GetComponent<RectTransform>();
            SetFullStretch(fadeRect);
            CanvasGroup fadeGroup = fadeObj.AddComponent<CanvasGroup>();
            fadeGroup.alpha = 0f;
            fadeGroup.blocksRaycasts = false;
            fadeObj.SetActive(false);
            ui.fadeOverlay = fadeGroup;

            return ui;
        }

        private static GameObject CreateUIElement(string name, Transform parent)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            return go;
        }

        private static void SetFullStretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Text CreateText(string name, Transform parent, Font font, int size, FontStyle style, Color color, TextAnchor alignment)
        {
            GameObject go = CreateUIElement(name, parent);
            Text txt = go.AddComponent<Text>();
            txt.font = font;
            txt.fontSize = size;
            txt.fontStyle = style;
            txt.color = color;
            txt.alignment = alignment;
            return txt;
        }

        private static Button CreateStyledButton(string name, Transform parent, Font font, string label, Vector2 size)
        {
            GameObject btnObj = CreateUIElement(name, parent);
            RectTransform rect = btnObj.GetComponent<RectTransform>();
            rect.sizeDelta = size;

            Image img = btnObj.AddComponent<Image>();
            img.color = new Color(0.18f, 0.22f, 0.30f, 0.90f);

            Button btn = btnObj.AddComponent<Button>();
            ColorBlock colors = btn.colors;
            colors.normalColor = new Color(0.18f, 0.22f, 0.30f, 0.90f);
            colors.highlightedColor = new Color(0.25f, 0.50f, 0.95f, 1.0f);
            colors.pressedColor = new Color(0.12f, 0.35f, 0.75f, 1.0f);
            colors.selectedColor = new Color(0.20f, 0.45f, 0.90f, 1.0f);
            btn.colors = colors;

            GameObject txtObj = CreateUIElement("Text", btnObj.transform);
            Text txt = txtObj.AddComponent<Text>();
            txt.font = font;
            txt.fontSize = 16;
            txt.fontStyle = FontStyle.Bold;
            txt.color = Color.white;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.text = label;

            RectTransform tRect = txtObj.GetComponent<RectTransform>();
            SetFullStretch(tRect);

            return btn;
        }

        private static void AddSceneToBuildSettings(string scenePath)
        {
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

            bool alreadyExists = false;
            for (int i = 0; i < scenes.Count; i++)
            {
                if (scenes[i].path == scenePath)
                {
                    alreadyExists = true;
                    // Move to index 0 so CampusTour is the startup scene
                    var match = scenes[i];
                    match.enabled = true;
                    scenes.RemoveAt(i);
                    scenes.Insert(0, match);
                    break;
                }
            }

            if (!alreadyExists)
            {
                scenes.Insert(0, new EditorBuildSettingsScene(scenePath, true));
            }
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log($"[CampusTourSceneBuilder] Set {scenePath} as primary scene (index 0) in EditorBuildSettings.scenes.");
        }

        [MenuItem("Tools/Campus VR Tour/Build Android APK", false, 20)]
        public static void BuildAndroidAPK()
        {
            Debug.Log("[CampusTourSceneBuilder] Starting Meta Quest Android APK Build...");
            string buildFolder = Path.Combine(Directory.GetCurrentDirectory(), "Builds", "Android");
            if (!Directory.Exists(buildFolder)) Directory.CreateDirectory(buildFolder);

            string apkPath = Path.Combine(buildFolder, "SVKM_NMIMS_Tour.apk");

            // Ensure Meta Quest ARM64 IL2CPP settings
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = apkPath,
                target = BuildTarget.Android,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(options);
            Debug.Log($"[CampusTourSceneBuilder] Android Build finished with result: {report.summary.result}, errors: {report.summary.totalErrors}, size: {report.summary.totalSize} bytes, output: {apkPath}");
        }

        [MenuItem("Tools/Campus VR Tour/Build Windows Player", false, 21)]
        public static void BuildWindowsPlayer()
        {
            Debug.Log("[CampusTourSceneBuilder] Starting Windows Standalone Build...");
            string buildFolder = Path.Combine(Directory.GetCurrentDirectory(), "Builds", "Windows");
            if (!Directory.Exists(buildFolder)) Directory.CreateDirectory(buildFolder);

            string exePath = Path.Combine(buildFolder, "SVKM_NMIMS_Tour.exe");

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = exePath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(options);
            Debug.Log($"[CampusTourSceneBuilder] Windows Build finished with result: {report.summary.result}, errors: {report.summary.totalErrors}, size: {report.summary.totalSize} bytes, output: {exePath}");
        }
    }
}
