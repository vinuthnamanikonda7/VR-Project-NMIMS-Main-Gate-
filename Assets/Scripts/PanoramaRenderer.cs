using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace CampusTour
{
    /// <summary>
    /// Renders 360-degree equirectangular panoramas on an inward-facing spherical mesh.
    /// Manages high-speed direct file loading on Desktop/Editor and UnityWebRequest on Android,
    /// texture memory management, and camera centering.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class PanoramaRenderer : MonoBehaviour
    {
        [Header("Configuration")]
        [Tooltip("Target camera that the panoramic sphere remains centered on.")]
        public Transform targetCamera;

        [Tooltip("Radius of the panoramic sphere in meters.")]
        public float sphereRadius = 50f;

        [Tooltip("Sphere longitude segments (higher = smoother curvature).")]
        [Range(24, 128)]
        public int longitudeSegments = 64;

        [Tooltip("Sphere latitude segments.")]
        [Range(16, 64)]
        public int latitudeSegments = 32;

        [Header("Material & Shader")]
        [Tooltip("Material used for panoramic rendering. If null, a runtime material is created.")]
        public Material panoramaMaterial;

        [Header("Diagnostics")]
        [SerializeField] private string currentLoadedFile = "";
        [SerializeField] private int activeRequestId = 0;

        private MeshRenderer meshRenderer;
        private MeshFilter meshFilter;
        private Mesh sphereMesh;
        private Texture2D currentTexture;

        public bool IsLoading { get; private set; }
        public string LastError { get; private set; }

        private void Awake()
        {
            meshRenderer = GetComponent<MeshRenderer>();
            meshFilter = GetComponent<MeshFilter>();

            if (targetCamera == null && Camera.main != null)
            {
                targetCamera = Camera.main.transform;
            }

            BuildInwardSphereMesh();
            EnsureMaterial();
        }

        private void LateUpdate()
        {
            if (targetCamera == null && Camera.main != null)
            {
                targetCamera = Camera.main.transform;
            }

            if (targetCamera != null)
            {
                transform.position = targetCamera.position;
            }
        }

        private void OnDestroy()
        {
            ReleaseTexture();
            if (sphereMesh != null)
            {
                Destroy(sphereMesh);
            }
        }

        /// <summary>
        /// Generates a smooth, procedural UV-mapped sphere with inward-facing triangles
        /// and un-mirrored equirectangular coordinates.
        /// </summary>
        public void BuildInwardSphereMesh()
        {
            if (sphereMesh != null)
            {
                Destroy(sphereMesh);
            }

            sphereMesh = new Mesh();
            sphereMesh.name = "InwardPanoramaSphere";

            int lon = longitudeSegments;
            int lat = latitudeSegments;

            Vector3[] vertices = new Vector3[(lon + 1) * (lat + 1)];
            Vector2[] uvs = new Vector2[vertices.Length];
            Vector3[] normals = new Vector3[vertices.Length];
            int[] triangles = new int[lon * lat * 6];

            float radius = sphereRadius;

            int vertIndex = 0;
            for (int y = 0; y <= lat; y++)
            {
                float v = (float)y / lat;
                float latAngle = (v - 0.5f) * Mathf.PI; // -PI/2 to PI/2
                float sinLat = Mathf.Sin(latAngle);
                float cosLat = Mathf.Cos(latAngle);

                for (int x = 0; x <= lon; x++)
                {
                    float u = (float)x / lon;
                    float lonAngle = u * Mathf.PI * 2f; // 0 to 2PI

                    float vx = cosLat * Mathf.Sin(lonAngle) * radius;
                    float vy = sinLat * radius;
                    float vz = cosLat * Mathf.Cos(lonAngle) * radius;

                    vertices[vertIndex] = new Vector3(vx, vy, vz);
                    // Mirror horizontal UV (1 - u) so viewing from inside is not flipped horizontally
                    uvs[vertIndex] = new Vector2(1f - u, v);
                    normals[vertIndex] = -new Vector3(vx, vy, vz).normalized;

                    vertIndex++;
                }
            }

            int triIndex = 0;
            for (int y = 0; y < lat; y++)
            {
                for (int x = 0; x < lon; x++)
                {
                    int current = y * (lon + 1) + x;
                    int next = current + lon + 1;

                    // Inward facing winding order (Clockwise when viewed from center)
                    triangles[triIndex++] = current;
                    triangles[triIndex++] = next;
                    triangles[triIndex++] = current + 1;

                    triangles[triIndex++] = next;
                    triangles[triIndex++] = next + 1;
                    triangles[triIndex++] = current + 1;
                }
            }

            sphereMesh.vertices = vertices;
            sphereMesh.uv = uvs;
            sphereMesh.normals = normals;
            sphereMesh.triangles = triangles;
            // Prevent frustum culling from ever hiding the sphere
            sphereMesh.bounds = new Bounds(Vector3.zero, Vector3.one * 2000f);

            if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();
            meshFilter.sharedMesh = sphereMesh;
        }

        private void EnsureMaterial()
        {
            if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();

            if (panoramaMaterial == null)
            {
                Shader shader = Shader.Find("CampusTour/Panorama360");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) shader = Shader.Find("Unlit/Texture");

                if (shader != null)
                {
                    panoramaMaterial = new Material(shader) { name = "M_CampusPanorama" };
                }
                else
                {
                    Debug.LogError("[PanoramaRenderer] Failed to find a compatible unlit shader!");
                }
            }

            if (panoramaMaterial != null)
            {
                panoramaMaterial.color = Color.white;
                if (panoramaMaterial.HasProperty("_BaseColor"))
                {
                    panoramaMaterial.SetColor("_BaseColor", Color.white);
                }
                if (panoramaMaterial.HasProperty("_Cull"))
                {
                    panoramaMaterial.SetFloat("_Cull", 0f); // 0 = Cull Off (Double-sided)
                }
            }

            if (meshRenderer != null && panoramaMaterial != null)
            {
                meshRenderer.sharedMaterial = panoramaMaterial;
                meshRenderer.enabled = true;
            }
        }

        /// <summary>
        /// Loads a 360-degree image asynchronously from Assets/StreamingAssets/Images/.
        /// </summary>
        public void LoadPanorama(string filename, float yawOffset, Action<bool> onComplete = null)
        {
            activeRequestId++;
            int requestId = activeRequestId;

            transform.rotation = Quaternion.Euler(0f, yawOffset, 0f);

            StartCoroutine(LoadPanoramaRoutine(filename, requestId, onComplete));
        }

        private IEnumerator LoadPanoramaRoutine(string filename, int requestId, Action<bool> onComplete)
        {
            IsLoading = true;
            LastError = null;

            if (string.IsNullOrEmpty(filename))
            {
                LastError = "No filename specified for location.";
                IsLoading = false;
                onComplete?.Invoke(false);
                yield break;
            }

            string relativePath = "Images/" + filename;
            string fullDiskPath = Path.Combine(Application.streamingAssetsPath, relativePath).Replace("\\", "/");
            Texture2D loadedTex = null;

            // Strategy 1: Direct File System Read (Fastest, zero URI escaping issues on Windows/Editor/Mac)
            if (Application.platform != RuntimePlatform.Android && File.Exists(fullDiskPath))
            {
                try
                {
                    byte[] fileBytes = File.ReadAllBytes(fullDiskPath);
                    loadedTex = new Texture2D(2, 2, TextureFormat.RGB24, false);
                    if (loadedTex.LoadImage(fileBytes))
                    {
                        Debug.Log($"[PanoramaRenderer] Loaded {filename} directly via File I/O: {loadedTex.width}x{loadedTex.height} px");
                    }
                    else
                    {
                        Destroy(loadedTex);
                        loadedTex = null;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[PanoramaRenderer] Direct file read fallback: {ex.Message}");
                    loadedTex = null;
                }
            }

            // Strategy 2: Android APK StreamingAssets via UnityWebRequest buffer + Texture2D.LoadImage
            if (loadedTex == null)
            {
                string requestUri = fullDiskPath;
                if (!requestUri.Contains("://"))
                {
                    requestUri = "file:///" + requestUri;
                }

                Debug.Log($"[PanoramaRenderer] Loading 360° image via WebRequest buffer from: {requestUri}");

                using (UnityWebRequest uwr = UnityWebRequest.Get(requestUri))
                {
                    yield return uwr.SendWebRequest();

                    if (requestId != activeRequestId)
                    {
                        yield break;
                    }

                    if (uwr.result == UnityWebRequest.Result.Success && uwr.downloadHandler != null)
                    {
                        byte[] rawBytes = uwr.downloadHandler.data;
                        if (rawBytes != null && rawBytes.Length > 0)
                        {
                            loadedTex = new Texture2D(2, 2, TextureFormat.RGB24, false);
                            if (loadedTex.LoadImage(rawBytes))
                            {
                                Debug.Log($"[PanoramaRenderer] Successfully decoded {filename}: {loadedTex.width}x{loadedTex.height} px ({rawBytes.Length} bytes)");
                            }
                            else
                            {
                                Destroy(loadedTex);
                                loadedTex = null;
                            }
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[PanoramaRenderer] WebRequest buffer load failed for {filename}: {uwr.error}");
                    }
                }
            }

            // Strategy 3: Fallback attempt via UnityWebRequestTexture
            if (loadedTex == null)
            {
                string requestUri = fullDiskPath;
                if (!requestUri.Contains("://"))
                {
                    requestUri = "file:///" + requestUri;
                }

                Debug.Log($"[PanoramaRenderer] Trying fallback UnityWebRequestTexture from: {requestUri}");

                using (UnityWebRequest uwr = UnityWebRequestTexture.GetTexture(requestUri))
                {
                    yield return uwr.SendWebRequest();

                    if (requestId != activeRequestId)
                    {
                        yield break;
                    }

                    if (uwr.result == UnityWebRequest.Result.Success)
                    {
                        loadedTex = DownloadHandlerTexture.GetContent(uwr);
                        Debug.Log($"[PanoramaRenderer] Loaded {filename} via DownloadHandlerTexture: {loadedTex.width}x{loadedTex.height}");
                    }
                    else
                    {
                        LastError = $"360° image not found: {filename} ({uwr.error})";
                        Debug.LogWarning($"[PanoramaRenderer] {LastError}");
                    }
                }
            }

            if (requestId != activeRequestId)
            {
                if (loadedTex != null) Destroy(loadedTex);
                yield break;
            }

            if (loadedTex != null)
            {
                loadedTex.wrapMode = TextureWrapMode.Clamp;
                loadedTex.filterMode = FilterMode.Bilinear;

                ApplyTexture(loadedTex, filename);
                IsLoading = false;
                onComplete?.Invoke(true);
            }
            else
            {
                if (string.IsNullOrEmpty(LastError))
                {
                    LastError = $"Failed to load 360° image: {filename}. Please verify file exists in Assets/StreamingAssets/Images/.";
                }
                Debug.LogError($"[PanoramaRenderer] {LastError}");
                IsLoading = false;
                onComplete?.Invoke(false);
            }
        }

        public void ApplyTexture(Texture2D newTexture, string filename = "")
        {
            ReleaseTexture();

            currentTexture = newTexture;
            currentLoadedFile = filename;

            EnsureMaterial();
            if (panoramaMaterial != null && currentTexture != null)
            {
                panoramaMaterial.color = Color.white;
                if (panoramaMaterial.HasProperty("_BaseColor"))
                {
                    panoramaMaterial.SetColor("_BaseColor", Color.white);
                }
                if (panoramaMaterial.HasProperty("_BaseMap"))
                {
                    panoramaMaterial.SetTexture("_BaseMap", currentTexture);
                }
                if (panoramaMaterial.HasProperty("_MainTex"))
                {
                    panoramaMaterial.SetTexture("_MainTex", currentTexture);
                }
                if (panoramaMaterial.HasProperty("_Cull"))
                {
                    panoramaMaterial.SetFloat("_Cull", 0f); // Double-sided
                }
            }

            if (meshRenderer != null)
            {
                meshRenderer.enabled = true;
            }
        }

        public void SetRenderTexture(RenderTexture rt)
        {
            ReleaseTexture();
            EnsureMaterial();
            if (panoramaMaterial != null)
            {
                if (panoramaMaterial.HasProperty("_BaseMap"))
                {
                    panoramaMaterial.SetTexture("_BaseMap", rt);
                }
                if (panoramaMaterial.HasProperty("_MainTex"))
                {
                    panoramaMaterial.SetTexture("_MainTex", rt);
                }
            }
        }

        public void SetVisible(bool visible)
        {
            if (meshRenderer != null)
            {
                meshRenderer.enabled = visible;
            }
        }

        private void ReleaseTexture()
        {
            if (currentTexture != null)
            {
                Destroy(currentTexture);
                currentTexture = null;
                currentLoadedFile = "";
            }
        }
    }
}
