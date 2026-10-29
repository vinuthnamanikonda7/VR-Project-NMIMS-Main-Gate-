using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

namespace CampusTour
{
    /// <summary>
    /// Coordinates the complete campus tour UI: headers, navigation bar, location picker dialog,
    /// fade transitions, loading spinner, and VR status indicator.
    /// Menu is locked firmly in the middle-left of the view.
    /// Full joystick navigation & click-to-select support with zero auto-dwell triggers.
    /// </summary>
    public class CampusTourUI : MonoBehaviour
    {
        public static CampusTourUI Instance { get; private set; }

        [Header("Header Elements")]
        public Text appTitleText;
        public Text vrStatusText;
        public Text locationBannerText;

        [Header("Navigation Bar Controls")]
        public Button prevButton;
        public Button nextButton;
        public Button menuToggleButton;
        public Button infoButton;
        public Button resetViewButton;
        public Button hideUIToggleButton;

        [Header("Video Controls Panel")]
        public GameObject videoControlsPanel;
        public Button videoPlayPauseButton;
        public Text videoPlayPauseText;
        public Button videoMuteButton;
        public Text videoMuteText;

        [Header("Location Selection Menu")]
        public GameObject locationMenuPanel;
        public Transform locationListContainer;
        public Button locationItemPrefab;
        public Button closeMenuButton;

        [Header("Information Popup Modal")]
        public GameObject infoModalPanel;
        public Text infoModalTitle;
        public Text infoModalBody;
        public Button closeInfoModalButton;

        [Header("Loading & Transition")]
        public GameObject loadingPanel;
        public Text loadingText;
        public CanvasGroup fadeOverlay;

        [Header("Error Message Modal")]
        public GameObject errorModalPanel;
        public Text errorModalText;
        public Button closeErrorModalButton;

        [Header("Root HUD")]
        public CanvasGroup hudCanvasGroup;

        [Header("VR Adaptation Settings")]
        [Tooltip("Whether to place UI in 3D World Space for VR headsets like Meta Quest.")]
        public bool adaptForVR = true;

        [Tooltip("Distance in meters in front of the viewer to lock the VR Canvas.")]
        public float vrDistance = 2.2f;

        [Tooltip("World scale multiplier (1.0 = native 1:1 scale ratio).")]
        public float vrWorldScale = 1.0f;

        private Canvas targetCanvas;
        private Camera activeCamera;
        private bool isVRCanvasActive = false;
        public bool IsVRCanvasActive => isVRCanvasActive;
        private List<Button> spawnedLocationButtons = new List<Button>();
        private int menuHighlightedIndex = 0;
        private bool isUIHidden = false;

        public bool IsMenuOpen => locationMenuPanel != null && locationMenuPanel.activeSelf;
        public int MenuHighlightedIndex => menuHighlightedIndex;

        public event Action OnPrevClicked;
        public event Action OnNextClicked;
        public event Action<int> OnLocationSelected;
        public event Action OnResetViewClicked;
        public event Action OnPlayPauseVideoClicked;
        public event Action OnMuteVideoClicked;

        private void Awake()
        {
            Instance = this;
            targetCanvas = GetComponent<Canvas>();

            // Completely remove any on-screen Menu, Info, and modal close buttons
            if (menuToggleButton != null)
            {
                menuToggleButton.gameObject.SetActive(false);
                Destroy(menuToggleButton.gameObject);
                menuToggleButton = null;
            }
            if (infoButton != null)
            {
                infoButton.gameObject.SetActive(false);
                Destroy(infoButton.gameObject);
                infoButton = null;
            }
            if (closeMenuButton != null)
            {
                closeMenuButton.gameObject.SetActive(false);
                Destroy(closeMenuButton.gameObject);
                closeMenuButton = null;
            }
            if (closeInfoModalButton != null)
            {
                closeInfoModalButton.gameObject.SetActive(false);
                Destroy(closeInfoModalButton.gameObject);
                closeInfoModalButton = null;
            }

            // Cleanly remove any residual Menu or Info buttons from saved scene hierarchy
            Button[] allBtns = GetComponentsInChildren<Button>(true);
            foreach (var b in allBtns)
            {
                if (b == null) continue;
                string bName = b.name.ToLower();
                if (bName.Contains("menubtn") || bName.Contains("infobtn") || 
                    bName.Contains("menubutton") || bName.Contains("infobutton") || 
                    bName.Contains("menutoggle") || bName.Contains("closemenu") || 
                    bName.Contains("closeinfo"))
                {
                    b.gameObject.SetActive(false);
                    Destroy(b.gameObject);
                }
            }

            SetupButtonListeners();
            SetLoading(false);
            SetError(null);
            SetInfoModalVisible(false);
            SetMenuVisible(false);
            SetVideoControlsVisible(false);

            if (fadeOverlay != null)
            {
                fadeOverlay.alpha = 0f;
                fadeOverlay.blocksRaycasts = false;
                fadeOverlay.gameObject.SetActive(false);
            }
        }

        private void Start()
        {
            activeCamera = Camera.main;

            // Check if running on Meta Quest or if XR is active
            bool isXR = XRSettings.isDeviceActive || Application.platform == RuntimePlatform.Android;
            if (adaptForVR && isXR)
            {
                ConfigureForVR();
            }
            else
            {
                EnsureCollidersOnButtons();
            }
        }

        /// <summary>
        /// Converts the canvas to a 3D World Space UI locked solidly to the camera in front of the viewer.
        /// The menu is locked firmly in the middle-left without running away or drifting.
        /// </summary>
        public void ConfigureForVR()
        {
            if (targetCanvas == null) targetCanvas = GetComponent<Canvas>();
            if (targetCanvas == null) return;

            if (activeCamera == null) activeCamera = Camera.main;

            targetCanvas.renderMode = RenderMode.WorldSpace;
            targetCanvas.worldCamera = activeCamera;

            // Firmly lock Canvas to the Camera with zero drift
            if (activeCamera != null)
            {
                transform.SetParent(activeCamera.transform, false);
                transform.localPosition = new Vector3(0f, 0f, vrDistance);
                transform.localRotation = Quaternion.identity;
                float baseUnitScale = 0.0018f * vrWorldScale;
                transform.localScale = Vector3.one * baseUnitScale;
            }

            RectTransform rt = GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.sizeDelta = new Vector2(1920, 1080);
            }

            isVRCanvasActive = true;
            EnsureCollidersOnButtons();
            Debug.Log("[CampusTourUI] Canvas successfully locked to camera in WorldSpace VR mode (Menu pinned to middle-left).");
        }

        /// <summary>
        /// Allows the Quest controller Joystick to navigate up/down through the 6 location items.
        /// </summary>
        public void NavigateMenuSelection(int direction)
        {
            if (!IsMenuOpen || spawnedLocationButtons == null || spawnedLocationButtons.Count == 0) return;

            menuHighlightedIndex += direction;
            if (menuHighlightedIndex < 0) menuHighlightedIndex = spawnedLocationButtons.Count - 1;
            if (menuHighlightedIndex >= spawnedLocationButtons.Count) menuHighlightedIndex = 0;

            HighlightCurrentLocationInMenu(menuHighlightedIndex);
        }

        /// <summary>
        /// Confirms selection of the currently highlighted menu item via Trigger, 'A' button, or Stick Click.
        /// </summary>
        public void ConfirmMenuSelection()
        {
            if (!IsMenuOpen || spawnedLocationButtons == null || spawnedLocationButtons.Count == 0) return;

            if (menuHighlightedIndex >= 0 && menuHighlightedIndex < spawnedLocationButtons.Count)
            {
                Debug.Log($"[CampusTourUI] Joystick confirmed selection: Location {menuHighlightedIndex + 1}");
                spawnedLocationButtons[menuHighlightedIndex].onClick.Invoke();
            }
        }

        /// <summary>
        /// Automatically adds BoxColliders to all buttons so Meta Quest laser raycast can interact with them.
        /// </summary>
        public void EnsureCollidersOnButtons()
        {
            Button[] buttons = GetComponentsInChildren<Button>(true);
            foreach (var btn in buttons)
            {
                RectTransform bRect = btn.GetComponent<RectTransform>();
                BoxCollider col = btn.GetComponent<BoxCollider>();
                if (col == null)
                {
                    col = btn.gameObject.AddComponent<BoxCollider>();
                }

                if (bRect != null)
                {
                    float w = Mathf.Max(bRect.rect.width, bRect.sizeDelta.x, 150f);
                    float h = Mathf.Max(bRect.rect.height, bRect.sizeDelta.y, 50f);
                    col.size = new Vector3(w, h, 25f);
                    col.center = Vector3.zero;
                }
            }
        }

        private void SetupButtonListeners()
        {
            if (prevButton != null) prevButton.onClick.AddListener(() => OnPrevClicked?.Invoke());
            if (nextButton != null) nextButton.onClick.AddListener(() => OnNextClicked?.Invoke());
            if (resetViewButton != null) resetViewButton.onClick.AddListener(() => OnResetViewClicked?.Invoke());

            if (menuToggleButton != null) menuToggleButton.onClick.AddListener(ToggleMenu);
            if (closeMenuButton != null) closeMenuButton.onClick.AddListener(() => SetMenuVisible(false));

            if (infoButton != null) infoButton.onClick.AddListener(OpenCurrentLocationInfo);
            if (closeInfoModalButton != null) closeInfoModalButton.onClick.AddListener(() => SetInfoModalVisible(false));

            if (closeErrorModalButton != null) closeErrorModalButton.onClick.AddListener(() => SetError(null));

            if (hideUIToggleButton != null) hideUIToggleButton.onClick.AddListener(ToggleHUDVisibility);

            if (videoPlayPauseButton != null) videoPlayPauseButton.onClick.AddListener(() => OnPlayPauseVideoClicked?.Invoke());
            if (videoMuteButton != null) videoMuteButton.onClick.AddListener(() => OnMuteVideoClicked?.Invoke());
        }

        public void PopulateLocationMenu(List<LocationData> locations, int currentIndex)
        {
            // Clear previous items
            foreach (var btn in spawnedLocationButtons)
            {
                if (btn != null) Destroy(btn.gameObject);
            }
            spawnedLocationButtons.Clear();

            if (locationListContainer == null || locationItemPrefab == null) return;

            for (int i = 0; i < locations.Count; i++)
            {
                int index = i;
                Button item = Instantiate(locationItemPrefab, locationListContainer);
                item.gameObject.SetActive(true);

                Text label = item.GetComponentInChildren<Text>();
                if (label != null)
                {
                    label.text = $"{i + 1}. {locations[i].locationName}";
                }

                item.onClick.AddListener(() =>
                {
                    SetMenuVisible(false);
                    OnLocationSelected?.Invoke(index);
                });

                spawnedLocationButtons.Add(item);
            }

            menuHighlightedIndex = currentIndex;
            HighlightCurrentLocationInMenu(currentIndex);
            EnsureCollidersOnButtons();
        }

        public void HighlightCurrentLocationInMenu(int currentIndex)
        {
            for (int i = 0; i < spawnedLocationButtons.Count; i++)
            {
                var colors = spawnedLocationButtons[i].colors;
                if (i == currentIndex)
                {
                    colors.normalColor = new Color(0.18f, 0.52f, 0.95f, 1.0f); // Selected bright blue
                }
                else
                {
                    colors.normalColor = new Color(0.14f, 0.16f, 0.22f, 0.90f); // Dark translucent
                }
                spawnedLocationButtons[i].colors = colors;
            }
        }

        public void UpdateLocationDisplay(int index, int total, string locationName, string description)
        {
            if (locationBannerText != null)
            {
                locationBannerText.text = $"[{index + 1}/{total}] {locationName}";
            }

            if (infoModalTitle != null)
            {
                infoModalTitle.text = locationName;
            }

            if (infoModalBody != null)
            {
                infoModalBody.text = description;
            }

            menuHighlightedIndex = index;
            HighlightCurrentLocationInMenu(index);
        }

        public void SetVRStatus(string status)
        {
            if (vrStatusText != null)
            {
                vrStatusText.text = status;
            }
        }

        public void SetLoading(bool loading, string message = "Loading 360° panorama...")
        {
            if (loadingPanel != null)
            {
                loadingPanel.SetActive(loading);
            }
            if (loadingText != null)
            {
                loadingText.text = message;
            }
        }

        public void SetError(string errorMsg)
        {
            if (errorModalPanel != null)
            {
                bool hasError = !string.IsNullOrEmpty(errorMsg);
                errorModalPanel.SetActive(hasError);
                if (hasError && errorModalText != null)
                {
                    errorModalText.text = errorMsg;
                }
            }
        }

        public void SetMenuVisible(bool visible)
        {
            if (locationMenuPanel != null)
            {
                locationMenuPanel.SetActive(visible);
            }

            if (visible)
            {
                if (CampusTourManager.Instance != null)
                {
                    menuHighlightedIndex = CampusTourManager.Instance.currentLocationIndex;
                }
                HighlightCurrentLocationInMenu(menuHighlightedIndex);
            }
        }

        public void ToggleMenu()
        {
            if (locationMenuPanel != null)
            {
                bool nextState = !locationMenuPanel.activeSelf;
                if (nextState && infoModalPanel != null && infoModalPanel.activeSelf)
                {
                    SetInfoModalVisible(false); // Mutual exclusion: close info if opening menu
                }
                SetMenuVisible(nextState);
            }
        }

        public void ToggleInfoModal()
        {
            if (infoModalPanel != null && infoModalPanel.activeSelf)
            {
                SetInfoModalVisible(false);
            }
            else
            {
                if (locationMenuPanel != null && locationMenuPanel.activeSelf)
                {
                    SetMenuVisible(false); // Mutual exclusion: close menu if opening info
                }
                OpenCurrentLocationInfo();
            }
        }

        public void OpenCurrentLocationInfo()
        {
            if (CampusTourManager.Instance != null && CampusTourManager.Instance.locations != null && CampusTourManager.Instance.locations.Count > 0)
            {
                int idx = CampusTourManager.Instance.currentLocationIndex;
                if (idx >= 0 && idx < CampusTourManager.Instance.locations.Count)
                {
                    var loc = CampusTourManager.Instance.locations[idx];
                    if (infoModalTitle != null)
                    {
                        infoModalTitle.text = loc.locationName;
                    }
                    if (infoModalBody != null)
                    {
                        infoModalBody.text = loc.description;
                    }
                }
            }

            SetInfoModalVisible(true);
            EnsureCollidersOnButtons();
            Debug.Log("[CampusTourUI] Opened Info Modal for current location: " + (infoModalTitle != null ? infoModalTitle.text : ""));
        }

        public void SetInfoModalVisible(bool visible)
        {
            if (infoModalPanel != null)
            {
                infoModalPanel.SetActive(visible);
            }
        }

        public void SetVideoControlsVisible(bool visible)
        {
            if (videoControlsPanel != null)
            {
                videoControlsPanel.SetActive(visible);
            }
        }

        public void UpdateVideoState(bool isPlaying, bool isMuted)
        {
            if (videoPlayPauseText != null)
            {
                videoPlayPauseText.text = isPlaying ? "⏸ Pause" : "▶ Play";
            }
            if (videoMuteText != null)
            {
                videoMuteText.text = isMuted ? "🔊 Unmute" : "🔇 Mute";
            }
        }

        public void ToggleHUDVisibility()
        {
            isUIHidden = !isUIHidden;
            if (hudCanvasGroup != null)
            {
                hudCanvasGroup.alpha = isUIHidden ? 0.0f : 1.0f;
                hudCanvasGroup.blocksRaycasts = !isUIHidden;
            }
        }

        /// <summary>
        /// Instant transition between locations with zero black screen.
        /// </summary>
        public IEnumerator FadeRoutine(float targetAlpha, float duration = 0.5f)
        {
            if (fadeOverlay != null)
            {
                fadeOverlay.alpha = 0f;
                fadeOverlay.blocksRaycasts = false;
                fadeOverlay.gameObject.SetActive(false);
            }
            yield break;
        }
    }
}
