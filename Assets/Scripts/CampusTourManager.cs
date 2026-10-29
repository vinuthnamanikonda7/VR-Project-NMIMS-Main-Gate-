using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CampusTour
{
    /// <summary>
    /// Central manager for the SVKM NMIMS Campus VR Tour.
    /// Orchestrates location data, panorama rendering, world-space hotspots,
    /// video playback, UI updates, and fade transitions.
    /// </summary>
    public class CampusTourManager : MonoBehaviour
    {
        public static CampusTourManager Instance { get; private set; }

        [Header("Tour Data")]
        [Tooltip("List of campus viewpoints configured for the tour.")]
        public List<LocationData> locations = new List<LocationData>();

        [Tooltip("Index of the currently active location (0 to 5).")]
        public int currentLocationIndex = 0;
        public int CurrentLocationIndex => currentLocationIndex;

        [Header("System References")]
        public PanoramaRenderer panoramaRenderer;
        public CampusTourController tourController;
        public VRModeManager vrManager;
        public CampusTourUI tourUI;
        public VideoPanoramaPlayer videoPlayer;
        public Transform cameraTransform;

        [Header("Hotspot Pool")]
        public CampusHotspot hotspotPrefab;
        public Transform hotspotContainer;

        private List<CampusHotspot> activeHotspotInstances = new List<CampusHotspot>();
        private bool isTransitioning = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (locations == null || locations.Count == 0)
            {
                InitializeDefaultLocations();
            }

            if (cameraTransform == null && Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }
        }

        private void Start()
        {
            if (tourUI != null)
            {
                tourUI.OnPrevClicked += GoToPreviousLocation;
                tourUI.OnNextClicked += GoToNextLocation;
                tourUI.OnLocationSelected += (index) => LoadLocation(index);
                tourUI.OnResetViewClicked += ResetView;

                if (videoPlayer != null)
                {
                    tourUI.OnPlayPauseVideoClicked += () =>
                    {
                        videoPlayer.TogglePlayPause();
                        tourUI.UpdateVideoState(videoPlayer.IsPlaying, videoPlayer.IsMuted);
                    };
                    tourUI.OnMuteVideoClicked += () =>
                    {
                        videoPlayer.ToggleMute();
                        tourUI.UpdateVideoState(videoPlayer.IsPlaying, videoPlayer.IsMuted);
                    };
                }

                tourUI.PopulateLocationMenu(locations, currentLocationIndex);
            }

            if (vrManager != null)
            {
                vrManager.OnVRStatusChanged += (isVR, statusMsg) =>
                {
                    if (tourUI != null) tourUI.SetVRStatus(statusMsg);
                };
                if (tourUI != null) tourUI.SetVRStatus(vrManager.XRStatusMessage);
            }

            // Start by loading initial viewpoint
            LoadLocation(currentLocationIndex, instant: true);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void InitializeDefaultLocations()
        {
            locations = new List<LocationData>
            {
                // Location 1 — Left Side Outside the Main Gate
                new LocationData
                {
                    locationName = "Location 1 — Left Side Outside the Main Gate",
                    imageName = "Location1.jpg",
                    description = "Explore the left side of the SVKM NMIMS campus main gate from outside. Look around to discover the entrance surroundings and the area on the left side of the gate.",
                    mediaType = TourMediaType.Image360,
                    yawOffset = 0f,
                    hotspots = new List<HotspotData>
                    {
                        new HotspotData { type = HotspotType.Navigation, targetLocationIndex = 1, yaw = 45f, pitch = -5f, label = "Right Side Outside Gate" },
                        new HotspotData { type = HotspotType.Navigation, targetLocationIndex = 2, yaw = 10f, pitch = -2f, label = "Full View Outside Gate" },
                        new HotspotData { type = HotspotType.Information, targetLocationIndex = 0, yaw = -40f, pitch = 10f, label = "Location Info" }
                    }
                },

                // Location 2 — Right Side Outside the Main Gate
                new LocationData
                {
                    locationName = "Location 2 — Right Side Outside the Main Gate",
                    imageName = "Location2.jpg",
                    description = "Explore the right side of the SVKM NMIMS campus main gate from outside. Look around to discover the entrance surroundings and the area on the right side of the gate.",
                    mediaType = TourMediaType.Image360,
                    yawOffset = 0f,
                    hotspots = new List<HotspotData>
                    {
                        new HotspotData { type = HotspotType.Navigation, targetLocationIndex = 0, yaw = -45f, pitch = -5f, label = "Left Side Outside Gate" },
                        new HotspotData { type = HotspotType.Navigation, targetLocationIndex = 2, yaw = -10f, pitch = -2f, label = "Full View Outside Gate" },
                        new HotspotData { type = HotspotType.Information, targetLocationIndex = 1, yaw = 40f, pitch = 10f, label = "Location Info" }
                    }
                },

                // Location 3 — Full View Outside the Main Gate
                new LocationData
                {
                    locationName = "Location 3 — Full View Outside the Main Gate",
                    imageName = "Location3.jpg",
                    description = "Experience a complete 360-degree view of the SVKM NMIMS campus main gate from outside. Turn in every direction to explore the gate and its surrounding area.",
                    mediaType = TourMediaType.Image360,
                    yawOffset = 0f,
                    hotspots = new List<HotspotData>
                    {
                        new HotspotData { type = HotspotType.Navigation, targetLocationIndex = 0, yaw = -60f, pitch = -5f, label = "Left Side Outside Gate" },
                        new HotspotData { type = HotspotType.Navigation, targetLocationIndex = 1, yaw = 60f, pitch = -5f, label = "Right Side Outside Gate" },
                        new HotspotData { type = HotspotType.Navigation, targetLocationIndex = 3, yaw = 0f, pitch = -2f, label = "Enter Main Gate (Inside)" },
                        new HotspotData { type = HotspotType.Information, targetLocationIndex = 2, yaw = 180f, pitch = 5f, label = "Location Info" }
                    }
                },

                // Location 4 — Left Side Inside the Main Gate
                new LocationData
                {
                    locationName = "Location 4 — Left Side Inside the Main Gate",
                    imageName = "Location4.jpg",
                    description = "Explore the left side of the SVKM NMIMS campus immediately after entering through the main gate. Look around to discover the surroundings on the left inside the campus entrance.",
                    mediaType = TourMediaType.Image360,
                    yawOffset = 0f,
                    hotspots = new List<HotspotData>
                    {
                        new HotspotData { type = HotspotType.Navigation, targetLocationIndex = 2, yaw = 180f, pitch = -5f, label = "Exit to Outside Gate" },
                        new HotspotData { type = HotspotType.Navigation, targetLocationIndex = 4, yaw = 55f, pitch = -5f, label = "Right Side Inside Gate" },
                        new HotspotData { type = HotspotType.Navigation, targetLocationIndex = 5, yaw = 10f, pitch = -2f, label = "Full View Inside Gate" },
                        new HotspotData { type = HotspotType.Information, targetLocationIndex = 3, yaw = -50f, pitch = 8f, label = "Location Info" }
                    }
                },

                // Location 5 — Right Side Inside the Main Gate
                new LocationData
                {
                    locationName = "Location 5 — Right Side Inside the Main Gate",
                    imageName = "Location5.jpg",
                    description = "Explore the right side of the SVKM NMIMS campus immediately after entering through the main gate. Look around to discover the surroundings on the right inside the campus entrance.",
                    mediaType = TourMediaType.Image360,
                    yawOffset = 0f,
                    hotspots = new List<HotspotData>
                    {
                        new HotspotData { type = HotspotType.Navigation, targetLocationIndex = 3, yaw = -55f, pitch = -5f, label = "Left Side Inside Gate" },
                        new HotspotData { type = HotspotType.Navigation, targetLocationIndex = 5, yaw = -10f, pitch = -2f, label = "Full View Inside Gate" },
                        new HotspotData { type = HotspotType.Information, targetLocationIndex = 4, yaw = 50f, pitch = 8f, label = "Location Info" }
                    }
                },

                // Location 6 — Full View Inside the Main Gate
                new LocationData
                {
                    locationName = "Location 6 — Full View Inside the Main Gate",
                    imageName = "Location6.jpg",
                    description = "Experience a complete 360-degree view of the SVKM NMIMS campus from inside the main gate. Turn in every direction to explore the entrance area from within the campus.",
                    mediaType = TourMediaType.Image360,
                    yawOffset = 0f,
                    hotspots = new List<HotspotData>
                    {
                        new HotspotData { type = HotspotType.Navigation, targetLocationIndex = 3, yaw = -45f, pitch = -5f, label = "Left Side Inside Gate" },
                        new HotspotData { type = HotspotType.Navigation, targetLocationIndex = 4, yaw = 45f, pitch = -5f, label = "Right Side Inside Gate" },
                        new HotspotData { type = HotspotType.Information, targetLocationIndex = 5, yaw = 180f, pitch = 5f, label = "Location Info" }
                    }
                }
            };
        }

        public void GoToPreviousLocation()
        {
            if (isTransitioning) return;
            int newIndex = (currentLocationIndex - 1 + locations.Count) % locations.Count;
            LoadLocation(newIndex);
        }

        public void GoToNextLocation()
        {
            if (isTransitioning) return;
            int newIndex = (currentLocationIndex + 1) % locations.Count;
            LoadLocation(newIndex);
        }

        public void ResetView()
        {
            if (tourController != null)
            {
                tourController.ResetView();
            }
        }

        public void LoadLocation(int index, bool instant = false)
        {
            if (index < 0 || index >= locations.Count)
            {
                Debug.LogWarning($"[CampusTourManager] Invalid location index: {index}");
                return;
            }

            if (isTransitioning && !instant)
            {
                Debug.Log("[CampusTourManager] Transition in progress, ignoring request.");
                return;
            }

            StartCoroutine(LoadLocationRoutine(index, instant));
        }

        private IEnumerator LoadLocationRoutine(int index, bool instant)
        {
            isTransitioning = true;
            currentLocationIndex = index;
            LocationData data = locations[index];

            if (tourUI != null)
            {
                tourUI.SetError(null);
            }

            // Zero black screen: keep current view fully visible while loading new panorama
            bool mediaLoadSuccess = false;

            // 1. Load Image or Video directly in the background
            if (data.mediaType == TourMediaType.Video360 && !string.IsNullOrEmpty(data.videoName) && videoPlayer != null)
            {
                if (tourUI != null) tourUI.SetVideoControlsVisible(true);
                bool videoReady = false;

                videoPlayer.PlayVideo(data.videoName, (success) =>
                {
                    videoReady = true;
                    mediaLoadSuccess = success;
                });

                while (!videoReady) yield return null;
            }
            else
            {
                if (videoPlayer != null)
                {
                    videoPlayer.StopAndRelease();
                }
                if (tourUI != null) tourUI.SetVideoControlsVisible(false);

                if (panoramaRenderer != null)
                {
                    bool imageReady = false;
                    panoramaRenderer.LoadPanorama(data.imageName, data.yawOffset, (success) =>
                    {
                        imageReady = true;
                        mediaLoadSuccess = success;
                    });

                    while (!imageReady) yield return null;
                }
            }

            // Clear old hotspots and spawn new ones once the new 360 view is active
            ClearHotspots();

            if (!mediaLoadSuccess)
            {
                string err = (panoramaRenderer != null && !string.IsNullOrEmpty(panoramaRenderer.LastError))
                    ? panoramaRenderer.LastError
                    : $"360° image not found: {data.imageName}. Please ensure it is present in Assets/StreamingAssets/Images/.";

                if (tourUI != null)
                {
                    tourUI.SetError(err);
                }
            }

            // 2. Update UI Banner, Menu, and Titles
            if (tourUI != null)
            {
                tourUI.UpdateLocationDisplay(index, locations.Count, data.locationName, data.description);
                tourUI.SetLoading(false);
            }

            // 3. Spawn Hotspots for the new location
            SpawnHotspotsForLocation(data);

            isTransitioning = false;
        }

        private void SpawnHotspotsForLocation(LocationData data)
        {
            if (hotspotPrefab == null) return;

            Transform camT = cameraTransform != null ? cameraTransform : (Camera.main != null ? Camera.main.transform : null);

            foreach (var hData in data.hotspots)
            {
                CampusHotspot instance = Instantiate(hotspotPrefab, hotspotContainer != null ? hotspotContainer : transform);
                instance.gameObject.SetActive(true);
                instance.Configure(hData, camT, OnHotspotClicked);
                activeHotspotInstances.Add(instance);
            }
        }

        private void ClearHotspots()
        {
            foreach (var h in activeHotspotInstances)
            {
                if (h != null) Destroy(h.gameObject);
            }
            activeHotspotInstances.Clear();
        }

        public void OnHotspotClicked(CampusHotspot hotspot)
        {
            if (hotspot == null || isTransitioning) return;

            if (hotspot.type == HotspotType.Navigation)
            {
                LoadLocation(hotspot.targetLocationIndex);
            }
            else if (hotspot.type == HotspotType.Information)
            {
                if (tourUI != null)
                {
                    tourUI.OpenCurrentLocationInfo();
                }
            }
        }

        /// <summary>
        /// Rotates the panoramic sphere and hotspot container horizontally,
        /// allowing Meta Quest Joystick smooth-turn and snap-turn controls.
        /// </summary>
        public void AddYawOffset(float deltaYaw)
        {
            if (panoramaRenderer != null)
            {
                panoramaRenderer.transform.Rotate(0f, deltaYaw, 0f, Space.World);
            }
            if (hotspotContainer != null)
            {
                hotspotContainer.Rotate(0f, deltaYaw, 0f, Space.World);
            }
        }
    }
}
