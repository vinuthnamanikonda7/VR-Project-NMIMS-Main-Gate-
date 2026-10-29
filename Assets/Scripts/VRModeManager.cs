using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

namespace CampusTour
{
    /// <summary>
    /// Manages VR mode detection, headset tracking coordination,
    /// enforces exact 1.0x scale rendering without 1.3x distortion,
    /// and eliminates all auto-dwell / holding timer triggers so interaction
    /// is 100% controlled by intentional joystick and click inputs.
    /// </summary>
    public class VRModeManager : MonoBehaviour
    {
        [Header("References")]
        public Camera mainCamera;
        public CampusTourController tourController;
        public Transform gazeReticle;
        public Image gazeProgressRing;

        [Header("Scale Enforcement")]
        [Tooltip("Target render and eye texture resolution scale (1.0 = native 1:1 scale).")]
        public float targetRenderScale = 1.0f;

        [Tooltip("Distance in meters in front of the camera to place the passive reticle.")]
        public float reticleDistance = 2.0f;

        [Header("Diagnostics")]
        [SerializeField] private bool isXRInitialized = false;
        [SerializeField] private string xrDeviceModel = "None";
        [SerializeField] private string xrStatusMessage = "Detecting...";

        public bool IsVRActive => isXRInitialized;
        public string XRStatusMessage => xrStatusMessage;

        public event Action<bool, string> OnVRStatusChanged;

        private void Start()
        {
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }

            EnforceTargetScale();
            StartCoroutine(CheckXRInitializationRoutine());

            // Controlling square / gaze reticle completely removed (pure joystick button control)
            if (gazeReticle != null)
            {
                gazeReticle.gameObject.SetActive(false);
                Destroy(gazeReticle.gameObject);
                gazeReticle = null;
            }

            if (gazeProgressRing != null)
            {
                gazeProgressRing.fillAmount = 0f;
                gazeProgressRing.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Explicitly resets eye texture resolution scale and URP render scale to 1.0f.
        /// </summary>
        public void EnforceTargetScale()
        {
            try
            {
                XRSettings.eyeTextureResolutionScale = targetRenderScale;
                XRSettings.renderViewportScale = targetRenderScale;

                if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline is UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset urp)
                {
                    urp.renderScale = targetRenderScale;
                }

                if (mainCamera != null)
                {
                    mainCamera.rect = new Rect(0, 0, 1, 1);
                    mainCamera.transform.localScale = Vector3.one;
                }

                Debug.Log($"[VRModeManager] Enforced exact scale: {targetRenderScale:F1}x (eyeTextureResolutionScale: {XRSettings.eyeTextureResolutionScale:F1})");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[VRModeManager] Note on scale enforcement: {ex.Message}");
            }
        }

        private IEnumerator CheckXRInitializationRoutine()
        {
            yield return new WaitForSeconds(0.3f);
            CheckXRStatus();
            EnforceTargetScale();
        }

        public void CheckXRStatus()
        {
            isXRInitialized = false;
            xrDeviceModel = "None";

            // 1. Check active OpenXR Display Subsystems
            var displaySubsystems = new List<XRDisplaySubsystem>();
            SubsystemManager.GetSubsystems(displaySubsystems);
            for (int i = 0; i < displaySubsystems.Count; i++)
            {
                if (displaySubsystems[i].running)
                {
                    isXRInitialized = true;
                    xrDeviceModel = displaySubsystems[i].subsystemDescriptor?.id ?? "OpenXR Display";
                    break;
                }
            }

            // 2. Check legacy XRSettings
            if (!isXRInitialized && XRSettings.isDeviceActive)
            {
                isXRInitialized = true;
                xrDeviceModel = XRSettings.loadedDeviceName;
            }

            // 3. Check for HeadMounted InputDevice
            if (!isXRInitialized)
            {
                var hmdList = new List<InputDevice>();
                InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.HeadMounted, hmdList);
                if (hmdList.Count > 0 && hmdList[0].isValid)
                {
                    isXRInitialized = true;
                    xrDeviceModel = hmdList[0].name;
                }
            }

            EnforceTargetScale();

            if (isXRInitialized)
            {
                xrStatusMessage = $"VR Active ({xrDeviceModel})";
                Debug.Log($"[VRModeManager] XR Subsystem Active! Device: {xrDeviceModel}");

                if (CampusTourUI.Instance != null)
                {
                    CampusTourUI.Instance.ConfigureForVR();
                }
            }
            else
            {
                xrStatusMessage = "Desktop / Touch Mode (VR Standby)";
                Debug.Log("[VRModeManager] Running in Desktop / Touch mode (XR device not active).");
            }

            if (tourController != null)
            {
                tourController.SetVRActive(isXRInitialized);
            }

            if (gazeReticle != null)
            {
                gazeReticle.gameObject.SetActive(false);
                Destroy(gazeReticle.gameObject);
                gazeReticle = null;
            }

            OnVRStatusChanged?.Invoke(isXRInitialized, xrStatusMessage);
        }

        private void LateUpdate()
        {
            if (isXRInitialized && mainCamera != null)
            {
                InputDevice hmd = InputDevices.GetDeviceAtXRNode(XRNode.CenterEye);
                if (hmd.isValid)
                {
                    if (hmd.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion hmdRot))
                    {
                        mainCamera.transform.localRotation = hmdRot;
                    }
                    if (hmd.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 hmdPos))
                    {
                        mainCamera.transform.localPosition = hmdPos;
                    }
                }
            }
        }

        private void Update()
        {
            if (gazeReticle != null && mainCamera != null)
            {
                gazeReticle.position = mainCamera.transform.position + mainCamera.transform.forward * reticleDistance;
                gazeReticle.rotation = mainCamera.transform.rotation;
            }
            // NOTE: Auto-dwell timer completely removed. Holding cursor on an element will NEVER trigger it.
        }
    }
}
