using System.Collections;
using UnityEngine;

namespace CampusTour
{
    public class VRModeController : MonoBehaviour
    {
        [Header("Auto-Start VR on Launch")]
        [SerializeField] private bool autoInitializeVR = true;

        private bool isVREnabled = false;
        public bool IsVREnabled => isVREnabled;

        private void Start()
        {
            if (autoInitializeVR)
            {
                StartCoroutine(EnableXRCoroutine());
            }
        }

        public void ToggleVRMode()
        {
            if (isVREnabled)
            {
                StartCoroutine(DisableXRCoroutine());
            }
            else
            {
                StartCoroutine(EnableXRCoroutine());
            }
        }

        private IEnumerator EnableXRCoroutine()
        {
#if UNITY_XR_MANAGEMENT
            var generalSettings = UnityEngine.XR.Management.XRGeneralSettings.Instance;
            if (generalSettings != null && generalSettings.Manager != null)
            {
                if (!generalSettings.Manager.isInitializationComplete)
                {
                    yield return generalSettings.Manager.InitializeLoader();
                }

                if (generalSettings.Manager.activeLoader != null)
                {
                    generalSettings.Manager.StartSubsystems();
                    isVREnabled = true;
                    Debug.Log("[CampusTour] XR Subsystems started successfully for VR Headset.");
                }
                else
                {
                    Debug.LogWarning("[CampusTour] No active XR Loader found for current platform.");
                }
            }
#else
            Debug.Log("[CampusTour] XR Management not active. Running standard camera mode.");
            yield return null;
#endif
        }

        private IEnumerator DisableXRCoroutine()
        {
#if UNITY_XR_MANAGEMENT
            var generalSettings = UnityEngine.XR.Management.XRGeneralSettings.Instance;
            if (generalSettings != null && generalSettings.Manager != null && generalSettings.Manager.isInitializationComplete)
            {
                generalSettings.Manager.StopSubsystems();
                generalSettings.Manager.DeinitializeLoader();
                isVREnabled = false;
                Debug.Log("[CampusTour] XR Subsystems stopped.");
            }
#endif
            yield return null;
        }
    }
}
