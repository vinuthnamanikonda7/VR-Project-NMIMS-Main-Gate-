using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace CampusTour
{
    /// <summary>
    /// Handles look-around controls across Editor (Mouse Drag), Mobile (Touch Drag),
    /// and VR (Hands-off / Hardware Head Tracking driven).
    /// Built strictly on the Unity New Input System.
    /// </summary>
    public class CampusTourController : MonoBehaviour
    {
        [Header("Camera & Target")]
        [Tooltip("Camera transform whose orientation will be controlled.")]
        public Transform cameraTransform;

        [Header("Rotation Settings")]
        [Tooltip("Mouse look sensitivity for desktop / editor.")]
        public float mouseSensitivity = 0.15f;

        [Tooltip("Touch look sensitivity for mobile devices.")]
        public float touchSensitivity = 0.12f;

        [Tooltip("Maximum pitch angle in degrees (prevent over-rotation).")]
        [Range(30f, 89f)]
        public float maxPitchAngle = 85f;

        [Tooltip("Damping factor for smooth look movement.")]
        public float smoothDamping = 12f;

        [Header("Interaction Settings")]
        [Tooltip("Maximum drag distance in pixels to still be considered a tap on a hotspot.")]
        public float tapThresholdPixels = 12f;

        [Tooltip("Layer mask for raycasting interactive hotspots.")]
        public LayerMask hotspotLayerMask = ~0;

        [Header("State")]
        [SerializeField] private bool isVRActive = false;
        [SerializeField] private float targetYaw = 0f;
        [SerializeField] private float targetPitch = 0f;

        private float currentYaw = 0f;
        private float currentPitch = 0f;

        private bool isDragging = false;
        private Vector2 dragStartPos;
        private CampusHotspot currentHoveredHotspot;

        private void Start()
        {
            if (cameraTransform == null && Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }

            if (cameraTransform != null)
            {
                Vector3 euler = cameraTransform.localEulerAngles;
                targetYaw = euler.y;
                targetPitch = (euler.x > 180f) ? euler.x - 360f : euler.x;
                currentYaw = targetYaw;
                currentPitch = targetPitch;
            }
        }

        private void Update()
        {
            // If VR is active, hardware head tracking (TrackedPoseDriver) controls orientation directly
            if (isVRActive)
            {
                HandleVRHoverRaycast();
                return;
            }

            HandleInput();
            ApplySmoothRotation();
            HandleHotspotHover();
        }

        public void SetVRActive(bool active)
        {
            isVRActive = active;
        }

        public void ResetView()
        {
            targetYaw = 0f;
            targetPitch = 0f;
            currentYaw = 0f;
            currentPitch = 0f;

            if (cameraTransform != null && !isVRActive)
            {
                cameraTransform.localRotation = Quaternion.identity;
            }
        }

        public void SetYaw(float yaw)
        {
            targetYaw = yaw;
            currentYaw = yaw;
        }

        private void HandleInput()
        {
            // 1. Mobile Touch Input
            if (Touchscreen.current != null && Touchscreen.current.touches.Count > 0)
            {
                var touch = Touchscreen.current.primaryTouch;
                int touchId = touch.touchId.ReadValue();

                if (touch.press.wasPressedThisFrame)
                {
                    Vector2 pos = touch.position.ReadValue();
                    if (!IsPointerOverUI(touchId))
                    {
                        isDragging = true;
                        dragStartPos = pos;
                    }
                }
                else if (touch.press.isPressed && isDragging)
                {
                    Vector2 delta = touch.delta.ReadValue();
                    targetYaw += delta.x * touchSensitivity;
                    targetPitch -= delta.y * touchSensitivity;
                    targetPitch = Mathf.Clamp(targetPitch, -maxPitchAngle, maxPitchAngle);
                }
                else if (touch.press.wasReleasedThisFrame && isDragging)
                {
                    Vector2 endPos = touch.position.ReadValue();
                    if (Vector2.Distance(dragStartPos, endPos) <= tapThresholdPixels)
                    {
                        TryRaycastHotspot(endPos);
                    }
                    isDragging = false;
                }
                return;
            }

            // 2. Desktop Mouse Input
            if (Mouse.current != null)
            {
                Vector2 mousePos = Mouse.current.position.ReadValue();

                if (Mouse.current.leftButton.wasPressedThisFrame)
                {
                    if (!IsPointerOverUI())
                    {
                        isDragging = true;
                        dragStartPos = mousePos;
                    }
                }
                else if (Mouse.current.leftButton.isPressed && isDragging)
                {
                    Vector2 delta = Mouse.current.delta.ReadValue();
                    targetYaw += delta.x * mouseSensitivity;
                    targetPitch -= delta.y * mouseSensitivity;
                    targetPitch = Mathf.Clamp(targetPitch, -maxPitchAngle, maxPitchAngle);
                }
                else if (Mouse.current.leftButton.wasReleasedThisFrame && isDragging)
                {
                    if (Vector2.Distance(dragStartPos, mousePos) <= tapThresholdPixels)
                    {
                        TryRaycastHotspot(mousePos);
                    }
                    isDragging = false;
                }
            }
        }

        private void ApplySmoothRotation()
        {
            if (cameraTransform == null) return;

            currentYaw = Mathf.LerpAngle(currentYaw, targetYaw, Time.deltaTime * smoothDamping);
            currentPitch = Mathf.Lerp(currentPitch, targetPitch, Time.deltaTime * smoothDamping);

            cameraTransform.localRotation = Quaternion.Euler(currentPitch, currentYaw, 0f);
        }

        private void HandleHotspotHover()
        {
            if (isDragging || Mouse.current == null) return;

            Vector2 mousePos = Mouse.current.position.ReadValue();
            if (IsPointerOverUI())
            {
                ClearHover();
                return;
            }

            Camera cam = cameraTransform != null ? cameraTransform.GetComponent<Camera>() : Camera.main;
            if (cam == null) return;

            Ray ray = cam.ScreenPointToRay(mousePos);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f, hotspotLayerMask))
            {
                CampusHotspot hotspot = hit.collider.GetComponentInParent<CampusHotspot>();
                if (hotspot != null)
                {
                    if (currentHoveredHotspot != hotspot)
                    {
                        ClearHover();
                        currentHoveredHotspot = hotspot;
                        currentHoveredHotspot.SetHovered(true);
                    }
                    return;
                }
            }

            ClearHover();
        }

        private void HandleVRHoverRaycast()
        {
            Camera cam = cameraTransform != null ? cameraTransform.GetComponent<Camera>() : Camera.main;
            if (cam == null) return;

            // Center-of-eye gaze ray for VR
            Ray ray = new Ray(cam.transform.position, cam.transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f, hotspotLayerMask))
            {
                CampusHotspot hotspot = hit.collider.GetComponentInParent<CampusHotspot>();
                if (hotspot != null)
                {
                    if (currentHoveredHotspot != hotspot)
                    {
                        ClearHover();
                        currentHoveredHotspot = hotspot;
                        currentHoveredHotspot.SetHovered(true);
                    }
                    return;
                }
            }
            ClearHover();
        }

        private void TryRaycastHotspot(Vector2 screenPos)
        {
            Camera cam = cameraTransform != null ? cameraTransform.GetComponent<Camera>() : Camera.main;
            if (cam == null) return;

            Ray ray = cam.ScreenPointToRay(screenPos);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f, hotspotLayerMask))
            {
                CampusHotspot hotspot = hit.collider.GetComponentInParent<CampusHotspot>();
                if (hotspot != null)
                {
                    hotspot.TriggerInteraction();
                }
            }
        }

        public void TriggerCenterGazeInteraction()
        {
            if (currentHoveredHotspot != null)
            {
                currentHoveredHotspot.TriggerInteraction();
            }
        }

        private void ClearHover()
        {
            if (currentHoveredHotspot != null)
            {
                currentHoveredHotspot.SetHovered(false);
                currentHoveredHotspot = null;
            }
        }

        private bool IsPointerOverUI(int touchId = -1)
        {
            if (EventSystem.current == null) return false;

            if (touchId >= 0)
            {
                return EventSystem.current.IsPointerOverGameObject(touchId);
            }
            return EventSystem.current.IsPointerOverGameObject();
        }
    }
}
