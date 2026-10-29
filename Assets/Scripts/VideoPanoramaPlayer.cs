using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Video;

namespace CampusTour
{
    /// <summary>
    /// Handles optional 360-degree video playback using Unity VideoPlayer and a high-resolution RenderTexture.
    /// Safely handles StreamingAssets URL construction, error catching, and resource cleanup.
    /// </summary>
    [RequireComponent(typeof(VideoPlayer), typeof(AudioSource))]
    public class VideoPanoramaPlayer : MonoBehaviour
    {
        [Header("Configuration")]
        public int renderWidth = 3840;
        public int renderHeight = 1920;

        [Header("References")]
        public PanoramaRenderer panoramaRenderer;

        private VideoPlayer videoPlayer;
        private AudioSource audioSource;
        private RenderTexture videoRenderTexture;

        public bool IsPlaying => videoPlayer != null && videoPlayer.isPlaying;
        public bool IsPrepared => videoPlayer != null && videoPlayer.isPrepared;
        public bool IsMuted => audioSource != null && audioSource.mute;

        public event Action<bool> OnPlayStateChanged;
        public event Action<string> OnVideoError;

        private void Awake()
        {
            videoPlayer = GetComponent<VideoPlayer>();
            audioSource = GetComponent<AudioSource>();

            videoPlayer.playOnAwake = false;
            videoPlayer.isLooping = true;
            videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
            videoPlayer.SetTargetAudioSource(0, audioSource);

            videoPlayer.errorReceived += HandleVideoError;
            videoPlayer.prepareCompleted += HandlePrepareCompleted;
        }

        private void OnDestroy()
        {
            if (videoPlayer != null)
            {
                videoPlayer.errorReceived -= HandleVideoError;
                videoPlayer.prepareCompleted -= HandlePrepareCompleted;
            }
            ReleaseResources();
        }

        public void PlayVideo(string videoFilename, Action<bool> onReady = null)
        {
            if (string.IsNullOrEmpty(videoFilename))
            {
                OnVideoError?.Invoke("No video filename specified.");
                onReady?.Invoke(false);
                return;
            }

            EnsureRenderTexture();

            string fullPath = Path.Combine(Application.streamingAssetsPath, "Videos", videoFilename).Replace("\\", "/");
            Debug.Log($"[VideoPanoramaPlayer] Preparing 360° video from: {fullPath}");

            videoPlayer.source = VideoSource.Url;
            videoPlayer.url = fullPath;
            videoPlayer.targetTexture = videoRenderTexture;

            if (panoramaRenderer != null)
            {
                panoramaRenderer.SetRenderTexture(videoRenderTexture);
            }

            StartCoroutine(PrepareRoutine(onReady));
        }

        private IEnumerator PrepareRoutine(Action<bool> onReady)
        {
            videoPlayer.Prepare();

            float timeout = 10f;
            float timer = 0f;

            while (!videoPlayer.isPrepared && timer < timeout)
            {
                timer += Time.deltaTime;
                yield return null;
            }

            if (videoPlayer.isPrepared)
            {
                videoPlayer.Play();
                OnPlayStateChanged?.Invoke(true);
                onReady?.Invoke(true);
            }
            else
            {
                string msg = $"Video preparation timed out: {videoPlayer.url}";
                Debug.LogWarning($"[VideoPanoramaPlayer] {msg}");
                OnVideoError?.Invoke(msg);
                onReady?.Invoke(false);
            }
        }

        public void Pause()
        {
            if (videoPlayer != null && videoPlayer.isPlaying)
            {
                videoPlayer.Pause();
                OnPlayStateChanged?.Invoke(false);
            }
        }

        public void Resume()
        {
            if (videoPlayer != null && !videoPlayer.isPlaying)
            {
                videoPlayer.Play();
                OnPlayStateChanged?.Invoke(true);
            }
        }

        public void TogglePlayPause()
        {
            if (IsPlaying) Pause();
            else Resume();
        }

        public void ToggleMute()
        {
            if (audioSource != null)
            {
                audioSource.mute = !audioSource.mute;
            }
        }

        public void StopAndRelease()
        {
            if (videoPlayer != null)
            {
                videoPlayer.Stop();
            }
            ReleaseResources();
        }

        private void EnsureRenderTexture()
        {
            if (videoRenderTexture == null)
            {
                videoRenderTexture = new RenderTexture(renderWidth, renderHeight, 0, RenderTextureFormat.ARGB32)
                {
                    name = "RT_PanoramaVideo",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };
                videoRenderTexture.Create();
            }
        }

        private void ReleaseResources()
        {
            if (videoRenderTexture != null)
            {
                if (videoPlayer != null && videoPlayer.targetTexture == videoRenderTexture)
                {
                    videoPlayer.targetTexture = null;
                }
                videoRenderTexture.Release();
                Destroy(videoRenderTexture);
                videoRenderTexture = null;
            }
        }

        private void HandleVideoError(VideoPlayer source, string message)
        {
            Debug.LogError($"[VideoPanoramaPlayer] Video error: {message}");
            OnVideoError?.Invoke(message);
        }

        private void HandlePrepareCompleted(VideoPlayer source)
        {
            Debug.Log("[VideoPanoramaPlayer] Video prepared successfully.");
        }
    }
}
