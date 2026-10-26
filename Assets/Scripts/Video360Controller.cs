using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Video;

namespace CampusTour
{
    [RequireComponent(typeof(VideoPlayer))]
    [RequireComponent(typeof(AudioSource))]
    public class Video360Controller : MonoBehaviour
    {
        [Header("Render Texture & Material")]
        [SerializeField] private RenderTexture renderTexture;
        [SerializeField] private Material skyboxMaterial;

        [Header("Components")]
        [SerializeField] private VideoPlayer videoPlayer;
        [SerializeField] private AudioSource audioSource;

        private Texture2D loadedImageTexture;
        private bool isLoadedTextureAnAsset = false;
        private bool isDisplayingImage = false;

        public bool IsPrepared => isDisplayingImage || (videoPlayer != null && videoPlayer.isPrepared);
        public bool IsPlaying => !isDisplayingImage && videoPlayer != null && videoPlayer.isPlaying;
        public double CurrentTime => videoPlayer != null ? videoPlayer.time : 0.0;
        public double Duration => videoPlayer != null && videoPlayer.frameCount > 0 && videoPlayer.frameRate > 0 
            ? (double)videoPlayer.frameCount / videoPlayer.frameRate 
            : 0.0;
        public float Progress => (Duration > 0.0) ? Mathf.Clamp01((float)(CurrentTime / Duration)) : 0f;

        public event System.Action<string> OnVideoError;
        public event System.Action OnVideoPrepared;
        public event System.Action<bool> OnPlayStateChanged;

        private void Awake()
        {
            if (videoPlayer == null) videoPlayer = GetComponent<VideoPlayer>();
            if (audioSource == null) audioSource = GetComponent<AudioSource>();

            SetupVideoComponents();
        }

        private void SetupVideoComponents()
        {
            if (renderTexture == null)
            {
                renderTexture = new RenderTexture(3840, 1920, 0, RenderTextureFormat.ARGB32)
                {
                    name = "Video360_RenderTexture"
                };
                renderTexture.Create();
            }

            videoPlayer.playOnAwake = false;
            videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            videoPlayer.targetTexture = renderTexture;
            videoPlayer.isLooping = true;
            videoPlayer.source = VideoSource.Url;

            // Audio configuration
            videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
            videoPlayer.EnableAudioTrack(0, true);
            videoPlayer.SetTargetAudioSource(0, audioSource);

            videoPlayer.prepareCompleted += SourceVideoPrepared;
            videoPlayer.errorReceived += SourceVideoError;

            // Pre-load Location 1 from Resources if available so there is NEVER a black frame on startup
            if (skyboxMaterial != null)
            {
                Texture2D initialTex = Resources.Load<Texture2D>("Media/Location1") ?? Resources.Load<Texture2D>("Location1");
                if (initialTex != null)
                {
                    skyboxMaterial.mainTexture = initialTex;
                    loadedImageTexture = initialTex;
                    isLoadedTextureAnAsset = true;
                    isDisplayingImage = true;
                }
                else if (skyboxMaterial.mainTexture == null)
                {
                    skyboxMaterial.mainTexture = renderTexture;
                }
                RenderSettings.skybox = skyboxMaterial;
            }
        }

        public void PlayMedia(string fileName)
        {
            StopAllCoroutines();

            string ext = Path.GetExtension(fileName).ToLowerInvariant();
            bool isImage = ext == ".jpg" || ext == ".jpeg" || ext == ".png" || string.IsNullOrEmpty(ext);

            if (isImage)
            {
                StartCoroutine(Load360ImageRoutine(fileName));
            }
            else
            {
                // Locate video file in StreamingAssets
                string pathInMedia = Path.Combine(Application.streamingAssetsPath, "Media", fileName);
                string pathInVideos = Path.Combine(Application.streamingAssetsPath, "Videos", fileName);
                string pathDirect = Path.Combine(Application.streamingAssetsPath, fileName);

                string resolvedPath = null;
                if (File.Exists(pathInMedia)) resolvedPath = pathInMedia;
                else if (File.Exists(pathInVideos)) resolvedPath = pathInVideos;
                else if (File.Exists(pathDirect)) resolvedPath = pathDirect;

                string actualPath = resolvedPath ?? pathInMedia;
                PlayVideoFile(actualPath);
            }
        }

        private IEnumerator Load360ImageRoutine(string fileName)
        {
            if (videoPlayer != null && videoPlayer.isPlaying)
            {
                videoPlayer.Stop();
            }

            string baseName = Path.GetFileNameWithoutExtension(fileName);

            // Method 1: Try Resources first (instant, 100% reliable on Android APK, zero filesystem issues)
            Texture2D resTex = Resources.Load<Texture2D>("Media/" + baseName) ?? Resources.Load<Texture2D>(baseName);
            if (resTex != null)
            {
                ApplySkyboxTexture(resTex, isAsset: true);
                yield break;
            }

            // Method 2: On Standalone/Editor, try reading direct file bytes
            string pathInMedia = Path.Combine(Application.streamingAssetsPath, "Media", fileName);
            string pathInVideos = Path.Combine(Application.streamingAssetsPath, "Videos", fileName);
            string pathDirect = Path.Combine(Application.streamingAssetsPath, fileName);

            string localPath = null;
            if (File.Exists(pathInMedia)) localPath = pathInMedia;
            else if (File.Exists(pathInVideos)) localPath = pathInVideos;
            else if (File.Exists(pathDirect)) localPath = pathDirect;

            if (!string.IsNullOrEmpty(localPath))
            {
                byte[] fileData = null;
                try
                {
                    fileData = File.ReadAllBytes(localPath);
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[CampusTour] File.ReadAllBytes failed: {ex.Message}");
                }

                if (fileData != null && fileData.Length > 0)
                {
                    Texture2D tex = new Texture2D(2, 2, TextureFormat.RGB24, false);
                    if (tex.LoadImage(fileData))
                    {
                        ApplySkyboxTexture(tex, isAsset: false);
                        yield break;
                    }
                }
            }

            // Method 3: UnityWebRequest for Android APK StreamingAssets (jar:file://)
            string url;
            if (Application.platform == RuntimePlatform.Android)
            {
                url = Application.streamingAssetsPath + "/Media/" + fileName;
            }
            else
            {
                url = "file://" + pathInMedia.Replace("\\", "/");
            }

            using (UnityWebRequest uwr = UnityWebRequest.Get(url))
            {
                yield return uwr.SendWebRequest();
                if (uwr.result == UnityWebRequest.Result.Success && uwr.downloadHandler != null)
                {
                    byte[] data = uwr.downloadHandler.data;
                    if (data != null && data.Length > 0)
                    {
                        Texture2D tex = new Texture2D(2, 2, TextureFormat.RGB24, false);
                        if (tex.LoadImage(data))
                        {
                            ApplySkyboxTexture(tex, isAsset: false);
                            yield break;
                        }
                    }
                }
            }

            // Fallback for Android root StreamingAssets
            if (Application.platform == RuntimePlatform.Android)
            {
                string fallbackUrl = Application.streamingAssetsPath + "/" + fileName;
                using (UnityWebRequest uwrFallback = UnityWebRequest.Get(fallbackUrl))
                {
                    yield return uwrFallback.SendWebRequest();
                    if (uwrFallback.result == UnityWebRequest.Result.Success && uwrFallback.downloadHandler != null)
                    {
                        byte[] data = uwrFallback.downloadHandler.data;
                        if (data != null && data.Length > 0)
                        {
                            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGB24, false);
                            if (tex.LoadImage(data))
                            {
                                ApplySkyboxTexture(tex, isAsset: false);
                                yield break;
                            }
                        }
                    }
                }
            }

            Debug.LogError($"[CampusTour] Could not load 360 image: {fileName}");
            OnVideoError?.Invoke($"Unable to load 360 image: {fileName}");
        }

        private void ApplySkyboxTexture(Texture2D tex, bool isAsset)
        {
            if (loadedImageTexture != null && !isLoadedTextureAnAsset)
            {
                Destroy(loadedImageTexture);
            }

            loadedImageTexture = tex;
            isLoadedTextureAnAsset = isAsset;
            isDisplayingImage = true;

            if (skyboxMaterial != null)
            {
                skyboxMaterial.mainTexture = loadedImageTexture;
                RenderSettings.skybox = skyboxMaterial;
                DynamicGI.UpdateEnvironment();
            }

            OnVideoPrepared?.Invoke();
            OnPlayStateChanged?.Invoke(false);
        }

        private void PlayVideoFile(string fullPath)
        {
            isDisplayingImage = false;

            if (loadedImageTexture != null && !isLoadedTextureAnAsset)
            {
                Destroy(loadedImageTexture);
                loadedImageTexture = null;
            }

            if (skyboxMaterial != null)
            {
                skyboxMaterial.mainTexture = renderTexture;
                RenderSettings.skybox = skyboxMaterial;
            }

            videoPlayer.url = fullPath;
            videoPlayer.Prepare();
        }

        private void SourceVideoPrepared(VideoPlayer source)
        {
            videoPlayer.Play();
            OnVideoPrepared?.Invoke();
            OnPlayStateChanged?.Invoke(true);
        }

        private void SourceVideoError(VideoPlayer source, string message)
        {
            Debug.LogError($"[CampusTour] Video Player Error: {message}");
            OnVideoError?.Invoke($"Video playback error: {message}");
            OnPlayStateChanged?.Invoke(false);
        }

        public void TogglePlayPause()
        {
            if (isDisplayingImage) return;
            if (videoPlayer == null || !videoPlayer.isPrepared) return;

            if (videoPlayer.isPlaying)
            {
                videoPlayer.Pause();
                OnPlayStateChanged?.Invoke(false);
            }
            else
            {
                videoPlayer.Play();
                OnPlayStateChanged?.Invoke(true);
            }
        }

        public void SetMute(bool isMuted)
        {
            if (audioSource != null)
            {
                audioSource.mute = isMuted;
            }
        }

        public bool IsMuted()
        {
            return audioSource != null && audioSource.mute;
        }

        public void Seek(float normalizedTime)
        {
            if (isDisplayingImage) return;
            if (videoPlayer == null || !videoPlayer.isPrepared || Duration <= 0.0) return;

            float clamped = Mathf.Clamp01(normalizedTime);
            videoPlayer.time = clamped * Duration;
        }

        public void SetSkyboxMaterial(Material mat)
        {
            skyboxMaterial = mat;
            if (skyboxMaterial != null)
            {
                skyboxMaterial.mainTexture = isDisplayingImage && loadedImageTexture != null ? (Texture)loadedImageTexture : renderTexture;
                RenderSettings.skybox = skyboxMaterial;
            }
        }

        public void SetRenderTexture(RenderTexture rt)
        {
            renderTexture = rt;
            if (videoPlayer != null)
            {
                videoPlayer.targetTexture = renderTexture;
            }
        }

        private void OnDestroy()
        {
            if (loadedImageTexture != null && !isLoadedTextureAnAsset)
            {
                Destroy(loadedImageTexture);
            }
        }
    }
}
