using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.XR;

namespace CampusTour
{
    public class HotspotInteractionHandler : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private LayerMask hotspotLayerMask = ~0;

        [Header("VR Controller Laser Pointers")]
        [SerializeField] private VRControllerLaser leftLaser;
        [SerializeField] private VRControllerLaser rightLaser;

        [Header("Gaze Reticle Fallback (Head-aim)")]
        [SerializeField] private bool enableGazeReticleInVR = true;
        [SerializeField] private GameObject gazeReticle;
        [SerializeField] private float gazeDistance = 8f;

        private Vector2 pointerDownPos;
        private float pointerDownTime;
        private bool isPointerDown;
        private const float MaxTapDistance = 15f;
        private const float MaxTapDuration = 0.35f;

        private Hotspot currentGazedHotspot;
        private UnityEngine.UI.Button currentGazedButton;

        private void Awake()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            SetupVRInteractors();
        }

        private void SetupVRInteractors()
        {
            Transform camParent = targetCamera != null ? targetCamera.transform.parent : transform;
            if (camParent == null) camParent = transform;

            if (rightLaser == null)
            {
                GameObject rightObj = new GameObject("RightController_Laser");
                rightObj.transform.SetParent(camParent, false);
                rightLaser = rightObj.AddComponent<VRControllerLaser>();
                rightLaser.SetHand(false);
            }

            if (leftLaser == null)
            {
                GameObject leftObj = new GameObject("LeftController_Laser");
                leftObj.transform.SetParent(camParent, false);
                leftLaser = leftObj.AddComponent<VRControllerLaser>();
                leftLaser.SetHand(true);
            }

            if (gazeReticle == null && enableGazeReticleInVR)
            {
                gazeReticle = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                gazeReticle.name = "GazeReticle";
                gazeReticle.transform.localScale = Vector3.one * 0.05f;

                Collider col = gazeReticle.GetComponent<Collider>();
                if (col != null) Destroy(col);

                Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit") 
                                  ?? Shader.Find("Sprites/Default") 
                                  ?? Shader.Find("Unlit/Color");
                Material mat = new Material(unlitShader);
                mat.color = new Color(1f, 1f, 1f, 0.75f);
                gazeReticle.GetComponent<Renderer>().material = mat;

                if (targetCamera != null)
                {
                    gazeReticle.transform.SetParent(targetCamera.transform, false);
                    gazeReticle.transform.localPosition = new Vector3(0f, 0f, 2f);
                }
                gazeReticle.SetActive(false);
            }
        }

        private void Update()
        {
            bool vrActive = CheckIfVRActive();

            if (vrActive)
            {
                bool usingRemotes = (rightLaser != null && rightLaser.IsActiveAndTracking) || 
                                    (leftLaser != null && leftLaser.IsActiveAndTracking);

                if (!usingRemotes && enableGazeReticleInVR)
                {
                    HandleGazeInteraction();
                }
                else if (gazeReticle != null && gazeReticle.activeSelf)
                {
                    gazeReticle.SetActive(false);
                }

                CheckGenericVRTrigger();
            }
            else
            {
                HandleMouseInteraction();
                HandleTouchInteraction();
            }
        }

        private bool CheckIfVRActive()
        {
            return UnityEngine.XR.XRSettings.isDeviceActive || 
                   (InputSystem.GetDevice<XRHMD>() != null);
        }

        private void HandleGazeInteraction()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            if (targetCamera == null) return;

            Ray gazeRay = new Ray(targetCamera.transform.position, targetCamera.transform.forward);

            if (gazeReticle != null)
            {
                gazeReticle.SetActive(true);
            }

            if (Physics.Raycast(gazeRay, out RaycastHit hit, gazeDistance * 2f, hotspotLayerMask))
            {
                currentGazedHotspot = hit.collider.GetComponentInParent<Hotspot>();
                currentGazedButton = hit.collider.GetComponentInParent<UnityEngine.UI.Button>();
                if (gazeReticle != null)
                {
                    gazeReticle.transform.position = hit.point - gazeRay.direction * 0.1f;
                    gazeReticle.transform.localScale = Vector3.one * 0.08f;
                }
            }
            else
            {
                currentGazedHotspot = null;
                currentGazedButton = null;
                if (gazeReticle != null)
                {
                    gazeReticle.transform.position = targetCamera.transform.position + targetCamera.transform.forward * 2f;
                    gazeReticle.transform.localScale = Vector3.one * 0.04f;
                }
            }
        }

        private void CheckGenericVRTrigger()
        {
            foreach (var device in InputSystem.devices)
            {
                if (device is XRController controller)
                {
                    var trigger = controller.GetChildControl<ButtonControl>("triggerPressed")
                               ?? controller.GetChildControl<ButtonControl>("trigger")
                               ?? controller.GetChildControl<ButtonControl>("primaryButton");

                    if (trigger != null && trigger.wasPressedThisFrame)
                    {
                        if (currentGazedButton != null)
                        {
                            currentGazedButton.onClick.Invoke();
                        }
                        else if (currentGazedHotspot != null)
                        {
                            currentGazedHotspot.OnClicked();
                        }
                    }
                }
            }
        }

        private void HandleMouseInteraction()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            if (mouse.leftButton.wasPressedThisFrame)
            {
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                {
                    return;
                }

                pointerDownPos = mouse.position.ReadValue();
                pointerDownTime = Time.time;
                isPointerDown = true;
            }
            else if (mouse.leftButton.wasReleasedThisFrame && isPointerDown)
            {
                isPointerDown = false;
                Vector2 releasePos = mouse.position.ReadValue();
                float dist = Vector2.Distance(pointerDownPos, releasePos);
                float duration = Time.time - pointerDownTime;

                if (dist <= MaxTapDistance && duration <= MaxTapDuration)
                {
                    PerformRaycast(releasePos);
                }
            }
        }

        private void HandleTouchInteraction()
        {
            Touchscreen touch = Touchscreen.current;
            if (touch == null) return;

            var touches = touch.touches;
            if (touches.Count > 0)
            {
                var primaryTouch = touches[0];
                if (primaryTouch.press.wasPressedThisFrame)
                {
                    int fingerId = primaryTouch.touchId.ReadValue();
                    if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(fingerId))
                    {
                        return;
                    }

                    pointerDownPos = primaryTouch.position.ReadValue();
                    pointerDownTime = Time.time;
                    isPointerDown = true;
                }
                else if (primaryTouch.press.wasReleasedThisFrame && isPointerDown)
                {
                    isPointerDown = false;
                    Vector2 releasePos = primaryTouch.position.ReadValue();
                    float dist = Vector2.Distance(pointerDownPos, releasePos);
                    float duration = Time.time - pointerDownTime;

                    if (dist <= MaxTapDistance && duration <= MaxTapDuration)
                    {
                        PerformRaycast(releasePos);
                    }
                }
            }
        }

        private void PerformRaycast(Vector2 screenPoint)
        {
            if (targetCamera == null) targetCamera = Camera.main;
            if (targetCamera == null) return;

            Ray ray = targetCamera.ScreenPointToRay(screenPoint);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f, hotspotLayerMask))
            {
                Hotspot spot = hit.collider.GetComponentInParent<Hotspot>();
                if (spot != null)
                {
                    spot.OnClicked();
                }
            }
        }
    }
}
