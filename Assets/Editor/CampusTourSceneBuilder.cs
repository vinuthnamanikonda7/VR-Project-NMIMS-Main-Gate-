#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using UnityEngine.Video;

namespace CampusTour.Editor
{
    public static class CampusTourSceneBuilder
    {
        private const string SceneDirectory = "Assets/Scenes";
        private const string ScenePath = "Assets/Scenes/CampusTour.unity";
        private const string MaterialsDirectory = "Assets/Materials";
        private const string SkyboxMaterialPath = "Assets/Materials/Skybox_Panoramic.mat";
        private const string RenderTexturePath = "Assets/Materials/Skybox_RenderTexture.renderTexture";

        [MenuItem("Tools/Campus VR Tour/Build Scene")]
        public static void BuildTourScene()
        {
            // 1. Ensure required directories
            EnsureDirectoryExists(SceneDirectory);
            EnsureDirectoryExists(MaterialsDirectory);
            EnsureDirectoryExists("Assets/StreamingAssets/Videos");

            // 2. Setup Skybox Material and RenderTexture
            Material skyboxMat = SetupSkyboxMaterial();
            RenderTexture renderTexture = SetupRenderTexture();
            Texture2D loc1Tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/Media/Location1.jpg")
                ?? AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/StreamingAssets/Media/Location1.jpg");
            if (loc1Tex != null)
            {
                skyboxMat.mainTexture = loc1Tex;
            }
            else
            {
                skyboxMat.mainTexture = renderTexture;
            }
            EditorUtility.SetDirty(skyboxMat);

            // 3. Create or open scene
            var newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 4. Set RenderSettings Skybox
            RenderSettings.skybox = skyboxMat;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;

            // 5. Main Camera & Rig
            GameObject cameraObj = new GameObject("Main Camera");
            cameraObj.tag = "MainCamera";
            cameraObj.transform.position = Vector3.zero;
            cameraObj.transform.rotation = Quaternion.identity;

            Camera cam = cameraObj.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.fieldOfView = 65f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 1000f;

            // TrackedPoseDriver for full 360-degree VR Headset tracking
            Type poseDriverType = Type.GetType("UnityEngine.InputSystem.XR.TrackedPoseDriver, Unity.InputSystem");
            if (poseDriverType != null)
            {
                cameraObj.AddComponent(poseDriverType);
            }

            // Add URP UniversalAdditionalCameraData if available via reflection to support both URP and Built-in
            Type urpCamType = Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
            if (urpCamType != null)
            {
                var urpCamData = cameraObj.AddComponent(urpCamType);
                var prop = urpCamType.GetProperty("renderPostProcessing");
                if (prop != null)
                {
                    prop.SetValue(urpCamData, true);
                }
            }

            // AudioListener on Camera
            cameraObj.AddComponent<AudioListener>();

            // Look Around & Raycast handlers
            LookAroundController lookAround = cameraObj.AddComponent<LookAroundController>();
            HotspotInteractionHandler interactionHandler = cameraObj.AddComponent<HotspotInteractionHandler>();

            // 6. Tour Manager & Video Controller root
            GameObject tourManagerObj = new GameObject("CampusTourManager");
            CampusTourManager tourManager = tourManagerObj.AddComponent<CampusTourManager>();
            Video360Controller videoController = tourManagerObj.AddComponent<Video360Controller>();
            VRModeController vrController = tourManagerObj.AddComponent<VRModeController>();

            videoController.SetSkyboxMaterial(skyboxMat);
            videoController.SetRenderTexture(renderTexture);

            // AudioSource for 360 audio
            AudioSource audioSource = tourManagerObj.GetComponent<AudioSource>();
            if (audioSource == null) audioSource = tourManagerObj.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;

            // Hotspots Container
            GameObject hotspotsContainerObj = new GameObject("HotspotsContainer");
            hotspotsContainerObj.transform.position = Vector3.zero;

            // 7. Event System with new InputSystemUIInputModule
            GameObject eventSystemObj = new GameObject("EventSystem");
            eventSystemObj.AddComponent<EventSystem>();
            eventSystemObj.AddComponent<InputSystemUIInputModule>();

            // 8. Build Canvas & UI Elements
            CampusTourUI uiController = BuildUI(videoController, vrController, cam);

            // 9. Assign Manager References
            tourManager.SetReferences(videoController, lookAround, uiController, hotspotsContainerObj.transform, cam);
            tourManager.InitializeDefaultLocationsIfEmpty();

            // 10. Save Scene & Add to Build Settings
            EditorSceneManager.SaveScene(newScene, ScenePath);
            AddSceneToBuildSettings(ScenePath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[CampusTour] Scene built successfully at {ScenePath} and registered in Build Settings!");
            EditorUtility.DisplayDialog("Campus VR Tour", "Campus Tour scene successfully built and ready at:\n" + ScenePath, "OK");
        }

        private static Material SetupSkyboxMaterial()
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(SkyboxMaterialPath);
            if (mat == null)
            {
                Shader skyShader = Shader.Find("Skybox/Panoramic");
                if (skyShader == null) skyShader = Shader.Find("Skybox/6 Sided");

                mat = new Material(skyShader);
                mat.SetFloat("_Mapping", 1); // Equirectangular (Latitude Longitude Layout)
                mat.SetFloat("_ImageType", 0); // 360 degrees
                mat.SetFloat("_MirrorOnBack", 0);
                AssetDatabase.CreateAsset(mat, SkyboxMaterialPath);
            }

            Texture2D loc1Tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/Media/Location1.jpg")
                ?? AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/StreamingAssets/Media/Location1.jpg");
            if (loc1Tex != null)
            {
                mat.mainTexture = loc1Tex;
                EditorUtility.SetDirty(mat);
            }
            return mat;
        }

        private static RenderTexture SetupRenderTexture()
        {
            RenderTexture rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(RenderTexturePath);
            if (rt == null)
            {
                rt = new RenderTexture(3840, 1920, 0, RenderTextureFormat.ARGB32)
                {
                    name = "Skybox_RenderTexture",
                    dimension = UnityEngine.Rendering.TextureDimension.Tex2D,
                    antiAliasing = 1,
                    useMipMap = false
                };
                AssetDatabase.CreateAsset(rt, RenderTexturePath);
            }
            return rt;
        }

        private static CampusTourUI BuildUI(Video360Controller videoController, VRModeController vrController, Camera mainCam)
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // UI Canvas in World Space so that it renders 100% reliably inside VR Headsets
            GameObject canvasObj = new GameObject("CampusTour_Canvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            Canvas canvas = canvasObj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = mainCam;
            canvas.sortingOrder = 100;

            Type trackedRaycasterType = Type.GetType("UnityEngine.InputSystem.UI.TrackedDeviceGraphicRaycaster, Unity.InputSystem");
            if (trackedRaycasterType != null)
            {
                canvasObj.AddComponent(trackedRaycasterType);
            }

            // Parent to camera so the HUD is always comfortably positioned in the user's field of view
            canvasObj.transform.SetParent(mainCam.transform, false);
            canvasObj.transform.localPosition = new Vector3(0f, 0f, 2.2f);
            canvasObj.transform.localRotation = Quaternion.identity;
            canvasObj.transform.localScale = Vector3.one * 0.0018f;

            RectTransform canvasRt = canvasObj.GetComponent<RectTransform>();
            canvasRt.sizeDelta = new Vector2(1920, 1080);

            CampusTourUI ui = canvasObj.AddComponent<CampusTourUI>();

            // --- Top Banner ---
            GameObject bannerObj = CreateUIElement("TopBanner", canvasObj.transform, 
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), 
                new Vector2(0, -45), new Vector2(1100, 70), new Color(0.08f, 0.1f, 0.14f, 0.88f));

            GameObject titleTextObj = CreateTextElement("LocationTitle", bannerObj.transform, font, 26, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            RectTransform titleRt = titleTextObj.GetComponent<RectTransform>();
            titleRt.anchorMin = Vector2.zero;
            titleRt.anchorMax = Vector2.one;
            titleRt.sizeDelta = Vector2.zero;
            titleRt.anchoredPosition = Vector2.zero;
            Text locTitleText = titleTextObj.GetComponent<Text>();
            locTitleText.text = "Campus VR Tour";

            // Menu toggle button on banner left
            GameObject menuBtnObj = CreateButtonElement("MenuToggleButton", bannerObj.transform, font, "LOCATIONS", 16, 
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(25, 0), new Vector2(140, 44),
                new Color(0.2f, 0.4f, 0.7f, 0.9f));
            Button menuBtn = menuBtnObj.GetComponent<Button>();

            // --- Menu Panel (Solid Red Box in Middle of Left, positioned comfortably inside VR vision) ---
            GameObject menuPanelObj = CreateUIElement("MenuPanel", canvasObj.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-480, 0), new Vector2(240, 410), new Color(0.85f, 0.12f, 0.12f, 0.98f));

            VerticalLayoutGroup vlg = menuPanelObj.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 10;
            vlg.padding = new RectOffset(12, 12, 14, 14);
            vlg.childControlHeight = true;
            vlg.childControlWidth = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = true;

            // --- Info Popup Modal ---
            GameObject infoPopupObj = CreateUIElement("InfoPopupModal", canvasObj.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(600, 320), new Color(0.12f, 0.15f, 0.22f, 0.98f));

            GameObject infoTitleObj = CreateTextElement("InfoTitle", infoPopupObj.transform, font, 24, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.3f, 0.85f, 1f));
            RectTransform itRt = infoTitleObj.GetComponent<RectTransform>();
            itRt.anchorMin = new Vector2(0, 1);
            itRt.anchorMax = new Vector2(1, 1);
            itRt.pivot = new Vector2(0.5f, 1f);
            itRt.anchoredPosition = new Vector2(25, -20);
            itRt.sizeDelta = new Vector2(-50, 40);

            GameObject infoDescObj = CreateTextElement("InfoDescription", infoPopupObj.transform, font, 18, FontStyle.Normal, TextAnchor.UpperLeft, Color.white);
            RectTransform idRt = infoDescObj.GetComponent<RectTransform>();
            idRt.anchorMin = Vector2.zero;
            idRt.anchorMax = Vector2.one;
            idRt.offsetMin = new Vector2(25, 75);
            idRt.offsetMax = new Vector2(-25, -70);

            GameObject infoCloseBtnObj = CreateButtonElement("CloseButton", infoPopupObj.transform, font, "CLOSE", 16,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 35), new Vector2(140, 42),
                new Color(0.7f, 0.25f, 0.25f, 0.95f));
            Button infoCloseBtn = infoCloseBtnObj.GetComponent<Button>();

            // --- Error Panel Modal ---
            GameObject errorPanelObj = CreateUIElement("ErrorPanelModal", canvasObj.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(650, 260), new Color(0.2f, 0.08f, 0.08f, 0.98f));

            GameObject errHeaderObj = CreateTextElement("ErrorHeader", errorPanelObj.transform, font, 22, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(1f, 0.4f, 0.4f));
            RectTransform ehRt = errHeaderObj.GetComponent<RectTransform>();
            ehRt.anchorMin = new Vector2(0, 1);
            ehRt.anchorMax = new Vector2(1, 1);
            ehRt.sizeDelta = new Vector2(0, 45);
            ehRt.anchoredPosition = new Vector2(0, -10);
            errHeaderObj.GetComponent<Text>().text = "Notice";

            GameObject errTextObj = CreateTextElement("ErrorMessage", errorPanelObj.transform, font, 16, FontStyle.Normal, TextAnchor.MiddleCenter, Color.white);
            RectTransform etRt = errTextObj.GetComponent<RectTransform>();
            etRt.anchorMin = Vector2.zero;
            etRt.anchorMax = Vector2.one;
            etRt.offsetMin = new Vector2(20, 65);
            etRt.offsetMax = new Vector2(-20, -55);

            GameObject errCloseBtnObj = CreateButtonElement("ErrorCloseButton", errorPanelObj.transform, font, "OK", 16,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 35), new Vector2(120, 40),
                new Color(0.4f, 0.4f, 0.45f, 0.95f));
            Button errCloseBtn = errCloseBtnObj.GetComponent<Button>();

            // --- Loading Indicator ---
            GameObject loadingObj = CreateUIElement("LoadingIndicator", canvasObj.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(220, 70), new Color(0f, 0f, 0f, 0.75f));
            GameObject loadingTextObj = CreateTextElement("LoadingText", loadingObj.transform, font, 18, FontStyle.Bold, TextAnchor.MiddleCenter, Color.yellow);
            loadingTextObj.GetComponent<Text>().text = "Loading 360 Video...";
            RectTransform ltRt = loadingTextObj.GetComponent<RectTransform>();
            ltRt.anchorMin = Vector2.zero;
            ltRt.anchorMax = Vector2.one;
            ltRt.sizeDelta = Vector2.zero;

            // --- Fade To Black Overlay Image ---
            GameObject fadeObj = new GameObject("FadeOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fadeObj.transform.SetParent(canvasObj.transform, false);
            RectTransform fadeRt = fadeObj.GetComponent<RectTransform>();
            fadeRt.anchorMin = Vector2.zero;
            fadeRt.anchorMax = Vector2.one;
            fadeRt.sizeDelta = Vector2.zero;
            Image fadeImg = fadeObj.GetComponent<Image>();
            fadeImg.color = new Color(0, 0, 0, 0);
            fadeImg.raycastTarget = false;

            // Wire up UI fields via serialized object
            SerializedObject so = new SerializedObject(ui);
            so.FindProperty("locationNameText").objectReferenceValue = locTitleText;
            so.FindProperty("infoPopupPanel").objectReferenceValue = infoPopupObj;
            so.FindProperty("infoTitleText").objectReferenceValue = infoTitleObj.GetComponent<Text>();
            so.FindProperty("infoDescriptionText").objectReferenceValue = infoDescObj.GetComponent<Text>();
            so.FindProperty("infoCloseButton").objectReferenceValue = infoCloseBtn;

            so.FindProperty("menuToggleButton").objectReferenceValue = menuBtn;
            so.FindProperty("menuPanel").objectReferenceValue = menuPanelObj;
            so.FindProperty("menuListContent").objectReferenceValue = menuPanelObj.transform;

            so.FindProperty("loadingIndicator").objectReferenceValue = loadingObj;
            so.FindProperty("errorPanel").objectReferenceValue = errorPanelObj;
            so.FindProperty("errorMessageText").objectReferenceValue = errTextObj.GetComponent<Text>();
            so.FindProperty("errorCloseButton").objectReferenceValue = errCloseBtn;
            so.FindProperty("fadeOverlayImage").objectReferenceValue = fadeImg;
            so.ApplyModifiedProperties();

            return ui;
        }

        private static GameObject CreateUIElement(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 sizeDelta, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;

            Image img = go.GetComponent<Image>();
            img.color = color;
            return go;
        }

        private static GameObject CreateTextElement(string name, Transform parent, Font font, int fontSize, FontStyle fontStyle, TextAnchor alignment, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);

            Text txt = go.GetComponent<Text>();
            txt.font = font;
            txt.fontSize = fontSize;
            txt.fontStyle = fontStyle;
            txt.alignment = alignment;
            txt.color = color;
            txt.supportRichText = true;
            return go;
        }

        private static GameObject CreateButtonElement(string name, Transform parent, Font font, string text, int fontSize, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 sizeDelta, Color btnColor)
        {
            GameObject btnObj = CreateUIElement(name, parent, anchorMin, anchorMax, pivot, anchoredPos, sizeDelta, btnColor);
            Button btn = btnObj.AddComponent<Button>();

            ColorBlock cb = btn.colors;
            cb.highlightedColor = btnColor * 1.25f;
            cb.pressedColor = btnColor * 0.8f;
            btn.colors = cb;

            GameObject textObj = CreateTextElement("Label", btnObj.transform, font, fontSize, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            RectTransform trt = textObj.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.sizeDelta = Vector2.zero;
            trt.anchoredPosition = Vector2.zero;
            textObj.GetComponent<Text>().text = text;

            BoxCollider col = btnObj.AddComponent<BoxCollider>();
            col.size = new Vector3(sizeDelta.x > 0 ? sizeDelta.x : 120f, sizeDelta.y > 0 ? sizeDelta.y : 44f, 5f);

            return btnObj;
        }

        private static GameObject CreateSliderElement(string name, Transform parent)
        {
            GameObject sliderObj = new GameObject(name, typeof(RectTransform), typeof(Slider));
            sliderObj.transform.SetParent(parent, false);

            Slider slider = sliderObj.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;

            // Background
            GameObject bgObj = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            bgObj.transform.SetParent(sliderObj.transform, false);
            RectTransform bgRt = bgObj.GetComponent<RectTransform>();
            bgRt.anchorMin = new Vector2(0f, 0.25f);
            bgRt.anchorMax = new Vector2(1f, 0.75f);
            bgRt.sizeDelta = Vector2.zero;
            bgObj.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.25f, 0.8f);

            // Fill Area
            GameObject fillAreaObj = new GameObject("Fill Area", typeof(RectTransform));
            fillAreaObj.transform.SetParent(sliderObj.transform, false);
            RectTransform faRt = fillAreaObj.GetComponent<RectTransform>();
            faRt.anchorMin = new Vector2(0f, 0.25f);
            faRt.anchorMax = new Vector2(1f, 0.75f);
            faRt.sizeDelta = Vector2.zero;

            GameObject fillObj = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fillObj.transform.SetParent(fillAreaObj.transform, false);
            RectTransform fRt = fillObj.GetComponent<RectTransform>();
            fRt.sizeDelta = Vector2.zero;
            fillObj.GetComponent<Image>().color = new Color(0.2f, 0.75f, 1f, 1f);

            slider.fillRect = fRt;
            slider.targetGraphic = fillObj.GetComponent<Image>();
            slider.direction = Slider.Direction.LeftToRight;

            return sliderObj;
        }

        private static void EnsureDirectoryExists(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        private static void AddSceneToBuildSettings(string scenePath)
        {
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            for (int i = 0; i < scenes.Count; i++)
            {
                if (scenes[i].path == scenePath)
                {
                    scenes[i].enabled = true;
                    EditorBuildSettings.scenes = scenes.ToArray();
                    return;
                }
            }

            scenes.Insert(0, new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
#endif
