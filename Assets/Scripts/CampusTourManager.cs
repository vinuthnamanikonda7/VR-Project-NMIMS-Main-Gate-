using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CampusTour
{
    public class CampusTourManager : MonoBehaviour
    {
        public static CampusTourManager Instance { get; private set; }

        [Header("Configuration & Data")]
        [SerializeField] private List<LocationData> locations = new List<LocationData>();
        [SerializeField] private int currentLocationIndex = 0;

        [Header("Scene References")]
        [SerializeField] private Video360Controller videoController;
        [SerializeField] private LookAroundController lookAroundController;
        [SerializeField] private CampusTourUI uiController;
        [SerializeField] private Transform hotspotsContainer;
        [SerializeField] private GameObject hotspotPrefab;
        [SerializeField] private Camera mainCamera;

        [Header("Transition Settings")]
        [SerializeField] private float fadeDuration = 0.5f;

        private bool isTransitioning = false;
        private List<Hotspot> activeHotspots = new List<Hotspot>();

        public List<LocationData> Locations => locations;
        public int CurrentLocationIndex => currentLocationIndex;
        public LocationData CurrentLocation => (locations != null && currentLocationIndex >= 0 && currentLocationIndex < locations.Count) 
            ? locations[currentLocationIndex] 
            : null;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }
        }

        private void Start()
        {
            InitializeDefaultLocationsIfEmpty();

            if (videoController != null)
            {
                videoController.OnVideoError += HandleVideoError;
                videoController.OnVideoPrepared += HandleVideoPrepared;
            }

            if (uiController != null)
            {
                uiController.PopulateLocationsMenu(locations, SwitchLocation);
            }

            if (locations.Count > 0)
            {
                LoadLocation(currentLocationIndex, instant: true);
            }
        }

        private void OnDestroy()
        {
            if (videoController != null)
            {
                videoController.OnVideoError -= HandleVideoError;
                videoController.OnVideoPrepared -= HandleVideoPrepared;
            }
        }

        public void InitializeDefaultLocationsIfEmpty()
        {
            if (locations != null && locations.Count > 0) return;

            locations = new List<LocationData>
            {
                new LocationData
                {
                    name = "Location 1: Main Gate Outer Left",
                    mediaFileName = "Location1.jpg",
                    description = "This perspective presents the left-side exterior vantage point of the SVKM NMIMS Main Gate, showing the outer approach road, perimeter security structures, and visitor pathways.",
                    hotspots = new List<HotspotData>
                    {
                        new HotspotData { type = HotspotType.Navigation, title = "Go to Location 2", targetLocationIndex = 1, yaw = 35f, pitch = -4f, distance = 8f },
                        new HotspotData { type = HotspotType.Navigation, title = "Go to Location 3", targetLocationIndex = 2, yaw = 75f, pitch = -3f, distance = 8f },
                        new HotspotData { type = HotspotType.Info, title = "Location 1 Overview", description = "Standing on the left side outside the SVKM NMIMS Main Gate. This area oversees the exterior campus boundary and entry lanes leading toward the central archway.", yaw = -35f, pitch = 2f, distance = 8f }
                    }
                },
                new LocationData
                {
                    name = "Location 2: Main Gate Outer Right",
                    mediaFileName = "Location2.jpg",
                    description = "This view captures the right-side exterior of the SVKM NMIMS Main Gate, featuring the adjoining pedestrian walkway, outer perimeter landscaping, and vehicle checkout area.",
                    hotspots = new List<HotspotData>
                    {
                        new HotspotData { type = HotspotType.Navigation, title = "Go to Location 3", targetLocationIndex = 2, yaw = 55f, pitch = -3f, distance = 8f },
                        new HotspotData { type = HotspotType.Navigation, title = "Back to Location 1", targetLocationIndex = 0, yaw = -160f, pitch = -3f, distance = 8f },
                        new HotspotData { type = HotspotType.Info, title = "Location 2 Overview", description = "Standing on the right side outside the SVKM NMIMS Main Gate. This perspective highlights the right boundary wing and exterior pedestrian sidewalks.", yaw = -40f, pitch = 6f, distance = 7f }
                    }
                },
                new LocationData
                {
                    name = "Location 3: Main Gate Full Outer View",
                    mediaFileName = "Location3.jpg",
                    description = "A comprehensive panoramic perspective capturing the full exterior view of the SVKM NMIMS Main Gate in its entirety, showcasing the grand institutional architecture and main entrance portal.",
                    hotspots = new List<HotspotData>
                    {
                        new HotspotData { type = HotspotType.Navigation, title = "Enter Campus (Loc 4)", targetLocationIndex = 3, yaw = 0f, pitch = -3f, distance = 8f },
                        new HotspotData { type = HotspotType.Navigation, title = "Go to Location 2", targetLocationIndex = 1, yaw = -110f, pitch = -3f, distance = 8f },
                        new HotspotData { type = HotspotType.Info, title = "Location 3 Overview", description = "A full exterior panoramic view of the SVKM NMIMS Main Gate, presenting the landmark front elevation, grand campus signage, and central entry thoroughfare.", yaw = -30f, pitch = 8f, distance = 7f }
                    }
                },
                new LocationData
                {
                    name = "Location 4: Main Gate Inner Left",
                    mediaFileName = "Location4.jpg",
                    description = "Positioned just past the threshold, this view showcases the left side inside the SVKM NMIMS Main Gate, featuring the internal security control post and academic circulation boulevard.",
                    hotspots = new List<HotspotData>
                    {
                        new HotspotData { type = HotspotType.Navigation, title = "Go to Location 5", targetLocationIndex = 4, yaw = 75f, pitch = -4f, distance = 8f },
                        new HotspotData { type = HotspotType.Navigation, title = "Go to Location 6", targetLocationIndex = 5, yaw = 30f, pitch = -3f, distance = 8f },
                        new HotspotData { type = HotspotType.Navigation, title = "Exit to Outside (Loc 3)", targetLocationIndex = 2, yaw = 180f, pitch = -3f, distance = 8f },
                        new HotspotData { type = HotspotType.Info, title = "Location 4 Overview", description = "Standing on the left side inside the SVKM NMIMS Main Gate. This vantage point welcomes visitors into the interior campus avenue and internal security checkpoint.", yaw = -25f, pitch = 4f, distance = 7f }
                    }
                },
                new LocationData
                {
                    name = "Location 5: Main Gate Inner Right",
                    mediaFileName = "Location5.jpg",
                    description = "Looking toward the right side inside the SVKM NMIMS Main Gate, overlooking internal service pathways, landscaped verges, and adjacent campus administrative facilities.",
                    hotspots = new List<HotspotData>
                    {
                        new HotspotData { type = HotspotType.Navigation, title = "Go to Location 6", targetLocationIndex = 5, yaw = 60f, pitch = -4f, distance = 8f },
                        new HotspotData { type = HotspotType.Navigation, title = "Back to Location 4", targetLocationIndex = 3, yaw = -120f, pitch = -3f, distance = 8f },
                        new HotspotData { type = HotspotType.Info, title = "Location 5 Overview", description = "Standing on the right side inside the SVKM NMIMS Main Gate. This area provides access along the right inner concourse and campus perimeter avenues.", yaw = -30f, pitch = 6f, distance = 7f }
                    }
                },
                new LocationData
                {
                    name = "Location 6: Main Gate Full Inner View",
                    mediaFileName = "Location6.jpg",
                    description = "An expansive full internal view looking across the SVKM NMIMS Main Gate from within the campus grounds, demonstrating the complete courtyard expanse and academic quad gateway.",
                    hotspots = new List<HotspotData>
                    {
                        new HotspotData { type = HotspotType.Navigation, title = "Return to Location 1", targetLocationIndex = 0, yaw = 180f, pitch = -4f, distance = 8f },
                        new HotspotData { type = HotspotType.Navigation, title = "Back to Location 5", targetLocationIndex = 4, yaw = -135f, pitch = -3f, distance = 8f },
                        new HotspotData { type = HotspotType.Info, title = "Location 6 Overview", description = "A full internal panoramic perspective looking from inside the SVKM NMIMS Main Gate. It offers an unobstructed vista connecting the gate to inner university buildings.", yaw = -20f, pitch = 5f, distance = 7f }
                    }
                }
            };
        }

        public void SwitchLocation(int index)
        {
            if (isTransitioning) return;
            if (index < 0 || index >= locations.Count) return;

            StartCoroutine(TransitionToLocationRoutine(index));
        }

        private IEnumerator TransitionToLocationRoutine(int index)
        {
            isTransitioning = true;

            // Fade out
            if (uiController != null)
            {
                yield return uiController.FadeScreen(0f, 1f, fadeDuration);
            }

            LoadLocation(index, instant: false);

            // Wait brief moment for setup
            yield return new WaitForSeconds(0.1f);

            // Fade in
            if (uiController != null)
            {
                yield return uiController.FadeScreen(1f, 0f, fadeDuration);
            }

            isTransitioning = false;
        }

        private void LoadLocation(int index, bool instant)
        {
            if (index < 0 || index >= locations.Count) return;

            currentLocationIndex = index;
            LocationData loc = locations[index];

            if (uiController != null)
            {
                uiController.SetLocationName(loc.name);
                uiController.HideInfoPopup();
                uiController.ShowLoading(true);
            }

            // Spawn Hotspots
            SpawnHotspotsForLocation(loc);

            // Play Media (Video or 360 Image)
            if (videoController != null)
            {
                videoController.PlayMedia(loc.mediaFileName);
            }
        }

        private void SpawnHotspotsForLocation(LocationData loc)
        {
            // Clear previous hotspots
            foreach (var h in activeHotspots)
            {
                if (h != null) Destroy(h.gameObject);
            }
            activeHotspots.Clear();

            if (hotspotsContainer == null)
            {
                GameObject containerObj = new GameObject("HotspotsContainer");
                hotspotsContainer = containerObj.transform;
            }

            Transform camTransform = mainCamera != null ? mainCamera.transform : transform;

            foreach (var spotData in loc.hotspots)
            {
                GameObject spotObj;
                if (hotspotPrefab != null)
                {
                    spotObj = Instantiate(hotspotPrefab, hotspotsContainer);
                }
                else
                {
                    spotObj = CreateDefaultHotspotVisual(spotData.type);
                    spotObj.transform.SetParent(hotspotsContainer);
                }

                Hotspot hotspotComp = spotObj.GetComponent<Hotspot>();
                if (hotspotComp == null)
                {
                    hotspotComp = spotObj.AddComponent<Hotspot>();
                }

                hotspotComp.Initialize(spotData, camTransform);
                activeHotspots.Add(hotspotComp);
            }
        }

        private GameObject CreateDefaultHotspotVisual(HotspotType type)
        {
            // Clean modern 3D hotspot root
            GameObject go = new GameObject("Hotspot_" + type);
            SphereCollider sc = go.AddComponent<SphereCollider>();
            sc.radius = 0.8f;
            sc.isTrigger = false;

            // Outer ring indicator
            GameObject ringObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ringObj.name = "OuterRing";
            ringObj.transform.SetParent(go.transform, false);
            ringObj.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            ringObj.transform.localScale = new Vector3(0.9f, 0.02f, 0.9f);
            Collider ringCol = ringObj.GetComponent<Collider>();
            if (ringCol != null) Destroy(ringCol);

            // Inner icon
            GameObject innerIcon;
            if (type == HotspotType.Navigation)
            {
                // Sleek pointer cone/arrow
                innerIcon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                innerIcon.name = "PointerArrow";
                innerIcon.transform.SetParent(go.transform, false);
                innerIcon.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                innerIcon.transform.localScale = new Vector3(0.45f, 0.04f, 0.45f);
            }
            else
            {
                // Information sphere
                innerIcon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                innerIcon.name = "InfoSphere";
                innerIcon.transform.SetParent(go.transform, false);
                innerIcon.transform.localScale = Vector3.one * 0.45f;
            }
            Collider innerCol = innerIcon.GetComponent<Collider>();
            if (innerCol != null) Destroy(innerCol);

            // Shaders and materials
            Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit") 
                              ?? Shader.Find("Sprites/Default") 
                              ?? Shader.Find("Unlit/Color");

            Material ringMat = new Material(unlitShader);
            Color themeColor = (type == HotspotType.Navigation) 
                ? new Color(0.2f, 0.85f, 1f, 0.95f) 
                : new Color(1f, 0.85f, 0.2f, 0.95f);
            ringMat.color = themeColor;
            ringObj.GetComponent<Renderer>().material = ringMat;

            Material innerMat = new Material(unlitShader);
            innerMat.color = Color.white;
            innerIcon.GetComponent<Renderer>().material = innerMat;

            // Child 3D text label
            GameObject textObj = new GameObject("Label");
            textObj.transform.SetParent(go.transform, false);
            textObj.transform.localPosition = new Vector3(0f, -0.8f, 0f);
            textObj.transform.localScale = Vector3.one * 0.12f;

            TextMesh tm = textObj.AddComponent<TextMesh>();
            tm.anchor = TextAnchor.UpperCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontSize = 28;
            tm.color = Color.white;

            return go;
        }

        private void HandleVideoPrepared()
        {
            if (uiController != null)
            {
                uiController.ShowLoading(false);
            }
        }

        private void HandleVideoError(string errorMsg)
        {
            if (uiController != null)
            {
                uiController.ShowLoading(false);
                uiController.ShowErrorMessage(errorMsg);
            }
        }

        public void OnHotspotActivated(HotspotData data)
        {
            if (data == null) return;

            if (data.type == HotspotType.Navigation)
            {
                SwitchLocation(data.targetLocationIndex);
            }
            else if (data.type == HotspotType.Info)
            {
                if (uiController != null)
                {
                    uiController.ShowInfoPopup(data.title, data.description);
                }
            }
        }

        // Setters for SceneBuilder
        public void SetReferences(Video360Controller vController, LookAroundController lController, CampusTourUI ui, Transform hContainer, Camera cam)
        {
            videoController = vController;
            lookAroundController = lController;
            uiController = ui;
            hotspotsContainer = hContainer;
            mainCamera = cam;
        }
    }
}
