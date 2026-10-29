using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.InputSystem;
using CommonUsages = UnityEngine.XR.CommonUsages;

namespace CampusTour
{
    /// <summary>
    /// Pure Joystick & Touch Controller handler for SVKM NMIMS Campus VR Tour.
    /// All on-screen buttons and point-and-click laser pointer have been removed as requested.
    /// 100% of the tour is controlled directly through the Meta Quest Touch Controllers:
    ///
    /// Controller Button Mapping:
    /// - Right Controller 'B' Button:
    ///     * Press 'B' to OPEN the Location Selection Menu (docked at middle-left).
    ///     * Press 'B' again to CLOSE the Location Selection Menu.
    ///
    /// - Left Controller 'Y' Button:
    ///     * Press 'Y' to OPEN the Info Card for the currently opened location.
    ///     * Press 'Y' again to CLOSE the Info Card.
    ///
    /// - 'A' Button (Right) & 'X' Button (Left):
    ///     * When Menu is OPEN: SELECT the highlighted location & immediately jump to it!
    ///     * When Info is OPEN: Close the Info card.
    ///     * When Menu is CLOSED: Advance to the Next Location.
    ///
    /// - Thumbsticks (Left & Right):
    ///     * When Menu is OPEN: Thumbstick Up / Down navigates through the 6 campus locations.
    ///     * When Menu is CLOSED:
    ///         - Push Up / Forward: Next Location (1 -> 2 -> 3 -> 4 -> 5 -> 6 -> 1)
    ///         - Pull Down / Backward: Previous Location (6 -> 5 -> 4 -> 3 -> 2 -> 1)
    ///         - Push Left / Right: Smooth 360° Rotation (80°/s) & 45° Snap Turn.
    ///     * Thumbstick Click (Press down): Reset View / Center Orientation.
    ///
    /// - Tactile Haptic Pulses on every button press & stick action.
    /// </summary>
    public class QuestJoystickController : MonoBehaviour
    {
        [Header("Joystick Location Navigation")]
        [Tooltip("Seconds cooldown between joystick location switches.")]
        public float locationSwitchCooldown = 0.38f;

        [Tooltip("Seconds cooldown between joystick menu steps.")]
        public float menuStepCooldown = 0.22f;

        [Header("Joystick Turn Settings")]
        [Tooltip("Enable continuous smooth turning with joystick.")]
        public bool enableSmoothTurn = true;

        [Tooltip("Degrees per second for smooth turning.")]
        public float smoothTurnSpeed = 80f;

        [Tooltip("Enable 45-degree snap turning on quick stick flick.")]
        public bool enableSnapTurn = true;

        [Tooltip("Angle in degrees for snap turn.")]
        public float snapTurnAngle = 45f;

        [Tooltip("Deadzone threshold for joystick input.")]
        [Range(0.1f, 0.5f)]
        public float deadzone = 0.22f;

        [Header("Haptics")]
        [Tooltip("Enable haptic vibration on Meta Quest controllers.")]
        public bool enableHaptics = true;

        // Runtime state
        private float lastSwitchTime = 0f;
        private float lastMenuStepTime = 0f;
        private bool snapTurnReset = true;

        // Edge detection state
        private bool bButtonWasPressed = false;
        private bool yButtonWasPressed = false;
        private bool selectWasPressed = false;
        private bool stickClickWasPressed = false;

        private void Update()
        {
            Vector2 stick = Vector2.zero;
            bool bButtonDown = false;    // Right Controller 'B' -> Toggle Menu
            bool yButtonDown = false;    // Left Controller 'Y'  -> Toggle Info
            bool selectButtonDown = false; // 'A' (Right) or 'X' (Left) -> Select
            bool stickClickDown = false; // Thumbstick Click -> Reset View

            // Pipeline 1: Native OpenXR Nodes for Meta Quest Touch Controllers
            UnityEngine.XR.InputDevice rightXR = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            UnityEngine.XR.InputDevice leftXR = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            UnityEngine.XR.InputDevice activeXR = rightXR.isValid ? rightXR : leftXR;

            // --- RIGHT CONTROLLER ---
            if (rightXR.isValid)
            {
                // Right Thumbstick
                if (rightXR.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 rStick) && rStick.sqrMagnitude > deadzone * deadzone)
                    stick = rStick;

                // Right 'B' Button (Secondary Button on Right Hand)
                if (rightXR.TryGetFeatureValue(CommonUsages.secondaryButton, out bool rSec) && rSec)
                    bButtonDown = true;

                // Right 'A' Button (Primary Button on Right Hand) -> Select
                if (rightXR.TryGetFeatureValue(CommonUsages.primaryButton, out bool rPrim) && rPrim)
                    selectButtonDown = true;

                // Right Trigger also acts as Select
                if (rightXR.TryGetFeatureValue(CommonUsages.triggerButton, out bool rTrig) && rTrig)
                    selectButtonDown = true;

                // Right Thumbstick Click
                if (rightXR.TryGetFeatureValue(CommonUsages.primary2DAxisClick, out bool rClk) && rClk)
                    stickClickDown = true;
            }

            // --- LEFT CONTROLLER ---
            if (leftXR.isValid)
            {
                // Left Thumbstick
                if (stick == Vector2.zero && leftXR.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 lStick) && lStick.sqrMagnitude > deadzone * deadzone)
                    stick = lStick;

                // Left 'Y' Button (Secondary Button on Left Hand)
                if (leftXR.TryGetFeatureValue(CommonUsages.secondaryButton, out bool lSec) && lSec)
                    yButtonDown = true;

                // Left Menu Button also maps to 'Y' / Info just in case
                if (leftXR.TryGetFeatureValue(CommonUsages.menuButton, out bool lMenu) && lMenu)
                    yButtonDown = true;

                // Left 'X' Button (Primary Button on Left Hand) -> Select
                if (leftXR.TryGetFeatureValue(CommonUsages.primaryButton, out bool lPrim) && lPrim)
                    selectButtonDown = true;

                // Left Trigger also acts as Select
                if (leftXR.TryGetFeatureValue(CommonUsages.triggerButton, out bool lTrig) && lTrig)
                    selectButtonDown = true;

                // Left Thumbstick Click
                if (leftXR.TryGetFeatureValue(CommonUsages.primary2DAxisClick, out bool lClk) && lClk)
                    stickClickDown = true;
            }

            // Pipeline 2: Unity New Input System (Gamepad fallback)
            if (Gamepad.current != null)
            {
                if (stick == Vector2.zero)
                {
                    Vector2 gpStick = Gamepad.current.leftStick.ReadValue();
                    if (gpStick.sqrMagnitude < deadzone * deadzone) gpStick = Gamepad.current.rightStick.ReadValue();
                    if (gpStick.sqrMagnitude > deadzone * deadzone) stick = gpStick;
                }

                if (Gamepad.current.buttonEast.isPressed) bButtonDown = true;     // 'B'
                if (Gamepad.current.buttonNorth.isPressed) yButtonDown = true;    // 'Y'
                if (Gamepad.current.buttonSouth.isPressed || Gamepad.current.buttonWest.isPressed ||
                    Gamepad.current.rightTrigger.isPressed || Gamepad.current.leftTrigger.isPressed)
                    selectButtonDown = true; // 'A' or 'X'
                if (Gamepad.current.leftStickButton.isPressed || Gamepad.current.rightStickButton.isPressed)
                    stickClickDown = true;
            }

            // Pipeline 3: Keyboard Fallback for Unity Editor testing
            #if UNITY_EDITOR
            if (Keyboard.current != null)
            {
                var kb = Keyboard.current;
                float h = 0f, v = 0f;
                if (kb.leftArrowKey.isPressed || kb.aKey.isPressed) h -= 1f;
                if (kb.rightArrowKey.isPressed || kb.dKey.isPressed) h += 1f;
                if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) v += 1f;
                if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) v -= 1f;

                if (kb.bKey.wasPressedThisFrame) bButtonDown = true; // 'B' -> Menu
                if (kb.yKey.wasPressedThisFrame || kb.iKey.wasPressedThisFrame) yButtonDown = true; // 'Y' -> Info
                if (kb.xKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame) selectButtonDown = true; // Select
                if (kb.rKey.wasPressedThisFrame) stickClickDown = true; // Reset

                if (Mathf.Abs(h) > 0.01f || Mathf.Abs(v) > 0.01f) stick = new Vector2(h, v);
            }
            #endif

            bool isMenuOpen = CampusTourUI.Instance != null && CampusTourUI.Instance.IsMenuOpen;
            bool isInfoOpen = CampusTourUI.Instance != null && CampusTourUI.Instance.infoModalPanel != null && CampusTourUI.Instance.infoModalPanel.activeSelf;

            // 1. Right Controller 'B' Button -> OPEN & CLOSE LOCATION MENU
            if (bButtonDown && !bButtonWasPressed)
            {
                CampusTourUI.Instance?.ToggleMenu();
                TriggerHaptic(rightXR, 0.5f, 0.08f);
                Debug.Log("[QuestJoystickController] Right 'B' Pressed -> Toggled Location Menu (Open/Close)");
            }
            bButtonWasPressed = bButtonDown;

            // 2. Left Controller 'Y' Button -> OPEN & CLOSE INFO OF CURRENT LOCATION
            if (yButtonDown && !yButtonWasPressed)
            {
                CampusTourUI.Instance?.ToggleInfoModal();
                TriggerHaptic(leftXR, 0.5f, 0.08f);
                Debug.Log("[QuestJoystickController] Left 'Y' Pressed -> Toggled Location Info Card (Open/Close)");
            }
            yButtonWasPressed = yButtonDown;

            // 3. 'A' (Right) and 'X' (Left) Buttons -> SELECTING
            if (selectButtonDown && !selectWasPressed)
            {
                if (isMenuOpen)
                {
                    // Menu is open: Confirm & select highlighted location, immediately jumps & closes menu
                    CampusTourUI.Instance?.ConfirmMenuSelection();
                    TriggerHaptic(activeXR, 0.75f, 0.1f);
                    Debug.Log("[QuestJoystickController] 'A'/'X' Pressed -> Confirmed Menu Location Selection");
                }
                else if (isInfoOpen)
                {
                    // Info is open: Close it
                    CampusTourUI.Instance?.SetInfoModalVisible(false);
                    TriggerHaptic(activeXR, 0.4f, 0.06f);
                    Debug.Log("[QuestJoystickController] 'A'/'X' Pressed -> Closed Info Card");
                }
                else
                {
                    // Neither is open: Advance to Next Location
                    if (Time.time - lastSwitchTime > locationSwitchCooldown)
                    {
                        lastSwitchTime = Time.time;
                        CampusTourManager.Instance?.GoToNextLocation();
                        TriggerHaptic(activeXR, 0.55f, 0.08f);
                        Debug.Log("[QuestJoystickController] 'A'/'X' Pressed -> Next Location");
                    }
                }
            }
            selectWasPressed = selectButtonDown;

            // 4. Thumbstick Click -> Reset View
            if (stickClickDown && !stickClickWasPressed)
            {
                CampusTourManager.Instance?.ResetView();
                TriggerHaptic(activeXR, 0.4f, 0.06f);
                Debug.Log("[QuestJoystickController] Thumbstick Clicked -> Reset View Orientation");
            }
            stickClickWasPressed = stickClickDown;

            // 5. Thumbstick Handling (Navigation & 360 Rotation)
            if (isMenuOpen)
            {
                // When Menu is OPEN: Thumbstick Up/Down navigates through the 6 location buttons
                if (Mathf.Abs(stick.y) > 0.50f)
                {
                    if (Time.time - lastMenuStepTime > menuStepCooldown)
                    {
                        lastMenuStepTime = Time.time;
                        int dir = (stick.y > 0.50f) ? -1 : 1; // Up = previous item, Down = next item
                        CampusTourUI.Instance?.NavigateMenuSelection(dir);
                        TriggerHaptic(activeXR, 0.35f, 0.04f);
                    }
                }
            }
            else
            {
                // When Menu is CLOSED: Thumbstick Up/Down switches locations directly
                if (Mathf.Abs(stick.y) > 0.60f)
                {
                    if (Time.time - lastSwitchTime > locationSwitchCooldown)
                    {
                        lastSwitchTime = Time.time;
                        if (stick.y > 0.60f)
                        {
                            CampusTourManager.Instance?.GoToNextLocation();
                            Debug.Log("[QuestJoystickController] Thumbstick UP -> Next Location");
                        }
                        else
                        {
                            CampusTourManager.Instance?.GoToPreviousLocation();
                            Debug.Log("[QuestJoystickController] Thumbstick DOWN -> Previous Location");
                        }
                        TriggerHaptic(activeXR, 0.55f, 0.08f);
                    }
                }

                // Thumbstick Left/Right: Smooth turn (80 deg/s) & 45 deg Snap Turn
                if (Mathf.Abs(stick.x) > deadzone)
                {
                    if (enableSnapTurn && Mathf.Abs(stick.x) > 0.72f)
                    {
                        if (snapTurnReset)
                        {
                            float snapAngle = (stick.x > 0f) ? -snapTurnAngle : snapTurnAngle;
                            CampusTourManager.Instance?.AddYawOffset(snapAngle);
                            TriggerHaptic(activeXR, 0.4f, 0.06f);
                            snapTurnReset = false;
                        }
                    }
                    else if (enableSmoothTurn)
                    {
                        float turnDelta = -stick.x * smoothTurnSpeed * Time.deltaTime;
                        CampusTourManager.Instance?.AddYawOffset(turnDelta);
                    }
                }
                else
                {
                    snapTurnReset = true;
                }
            }
        }

        private void TriggerHaptic(UnityEngine.XR.InputDevice device, float amplitude, float duration)
        {
            if (!enableHaptics || !device.isValid) return;

            if (device.TryGetHapticCapabilities(out HapticCapabilities capabilities) && capabilities.supportsImpulse)
            {
                device.SendHapticImpulse(0, amplitude, duration);
            }
        }
    }
}
