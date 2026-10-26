using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;

namespace CampusTour
{
    public class LookAroundController : MonoBehaviour
    {
        [Header("Sensitivity Settings")]
        [SerializeField] private float mouseSensitivity = 0.15f;
        [SerializeField] private float touchSensitivity = 0.12f;
        [SerializeField] private float smoothSpeed = 25f;

        [Header("Pitch Limits")]
        [SerializeField] private float minPitch = -85f;
        [SerializeField] private float maxPitch = 85f;

        [Header("Gyroscope Settings")]
        [SerializeField] private bool useGyroscopeIfAvailable = true;

        private float currentYaw;
        private float currentPitch;
        private float targetYaw;
        private float targetPitch;

        private bool isDraggingMouse;
        private Vector2 previousMousePosition;
        private Vector2 previousTouchPosition;
        private bool hasInitialTouch;

        private bool gyroActive;

        private void Start()
        {
            Vector3 euler = transform.localEulerAngles;
            currentYaw = euler.y;
            currentPitch = euler.x;
            if (currentPitch > 180f) currentPitch -= 360f;

            targetYaw = currentYaw;
            targetPitch = currentPitch;

            // Check if running in VR headset
            if (IsXRHeadsetActive())
            {
                // In VR headset, headset hardware handles 360 rotation directly!
                return;
            }

            // Initialize Gyroscope / AttitudeSensor on mobile
            if (useGyroscopeIfAvailable && (Application.isMobilePlatform || SystemInfo.supportsGyroscope))
            {
                if (AttitudeSensor.current != null)
                {
                    InputSystem.EnableDevice(AttitudeSensor.current);
                    gyroActive = true;
                }
                else if (UnityEngine.InputSystem.Gyroscope.current != null)
                {
                    InputSystem.EnableDevice(UnityEngine.InputSystem.Gyroscope.current);
                    gyroActive = true;
                }
            }
        }

        private bool IsXRHeadsetActive()
        {
            if (UnityEngine.XR.XRSettings.isDeviceActive) return true;
            if (InputSystem.GetDevice<XRHMD>() != null) return true;
            return false;
        }

        private void Update()
        {
            // If in a VR headset, do not override camera rotation with mouse/touch/gyro
            if (IsXRHeadsetActive())
            {
                return;
            }

            bool handledTouch = HandleTouchInput();
            bool handledMouse = false;

            if (!handledTouch)
            {
                handledMouse = HandleMouseInput();
            }

            if (gyroActive && !handledTouch && !handledMouse)
            {
                HandleGyroInput();
            }
            else
            {
                // Smooth interpolation for mouse/touch look
                currentYaw = Mathf.Lerp(currentYaw, targetYaw, Time.deltaTime * smoothSpeed);
                currentPitch = Mathf.Lerp(currentPitch, targetPitch, Time.deltaTime * smoothSpeed);
                transform.localRotation = Quaternion.Euler(currentPitch, currentYaw, 0f);
            }
        }

        private bool HandleMouseInput()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) return false;

            if (mouse.leftButton.wasPressedThisFrame)
            {
                isDraggingMouse = true;
                previousMousePosition = mouse.position.ReadValue();
            }
            else if (mouse.leftButton.wasReleasedThisFrame)
            {
                isDraggingMouse = false;
            }

            if (isDraggingMouse && mouse.leftButton.isPressed)
            {
                Vector2 currentPos = mouse.position.ReadValue();
                Vector2 delta = currentPos - previousMousePosition;
                previousMousePosition = currentPos;

                targetYaw += delta.x * mouseSensitivity;
                targetPitch -= delta.y * mouseSensitivity;
                targetPitch = Mathf.Clamp(targetPitch, minPitch, maxPitch);
                return true;
            }

            return false;
        }

        private bool HandleTouchInput()
        {
            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen == null) return false;

            var touches = touchscreen.touches;
            if (touches.Count > 0 && touches[0].isInProgress)
            {
                Vector2 touchPos = touches[0].position.ReadValue();

                if (!hasInitialTouch)
                {
                    hasInitialTouch = true;
                    previousTouchPosition = touchPos;
                    return true;
                }

                Vector2 delta = touchPos - previousTouchPosition;
                previousTouchPosition = touchPos;

                targetYaw += delta.x * touchSensitivity;
                targetPitch -= delta.y * touchSensitivity;
                targetPitch = Mathf.Clamp(targetPitch, minPitch, maxPitch);
                return true;
            }
            else
            {
                hasInitialTouch = false;
            }

            return false;
        }

        private void HandleGyroInput()
        {
            if (AttitudeSensor.current != null)
            {
                Quaternion attitude = AttitudeSensor.current.attitude.ReadValue();
                Quaternion converted = new Quaternion(attitude.x, attitude.y, -attitude.z, -attitude.w);
                transform.localRotation = Quaternion.Euler(90f, 0f, 0f) * converted;
            }
        }

        public void ResetOrientation()
        {
            targetYaw = 0f;
            targetPitch = 0f;
            currentYaw = 0f;
            currentPitch = 0f;
            transform.localRotation = Quaternion.identity;
        }
    }
}
