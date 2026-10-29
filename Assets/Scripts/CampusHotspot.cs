using System;
using UnityEngine;
using UnityEngine.UI;

namespace CampusTour
{
    /// <summary>
    /// Interactive world-space hotspot positioned spherically around the camera origin.
    /// Supports navigation arrows and information markers with billboard orientation and hover effects.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class CampusHotspot : MonoBehaviour
    {
        [Header("Hotspot Configuration")]
        public HotspotType type = HotspotType.Navigation;
        public int targetLocationIndex = 0;
        public string labelText = "Next Viewpoint";

        [Header("Visual References")]
        public Transform visualRoot;
        public Text iconText;
        public Text labelDisplay;
        public Image iconBackground;

        [Header("Styling")]
        public Color navColor = new Color(0.12f, 0.45f, 0.90f, 0.92f);      // Restrained Blue Accent
        public Color infoColor = new Color(0.15f, 0.65f, 0.35f, 0.92f);     // Emerald Info Accent
        public Color hoverColor = new Color(0.95f, 0.75f, 0.15f, 1.0f);     // Gold Hover

        private Transform targetCamera;
        private bool isHovered = false;
        private Vector3 defaultScale;

        public event Action<CampusHotspot> OnClicked;

        private void Awake()
        {
            if (visualRoot == null)
            {
                visualRoot = transform;
            }
            defaultScale = visualRoot.localScale;

            if (targetCamera == null && Camera.main != null)
            {
                targetCamera = Camera.main.transform;
            }
        }

        private void LateUpdate()
        {
            // Always billboard face the camera
            if (targetCamera != null)
            {
                transform.rotation = Quaternion.LookRotation(transform.position - targetCamera.position);
            }
        }

        /// <summary>
        /// Configures and places the hotspot in 3D world space at the specified spherical angles.
        /// </summary>
        public void Configure(HotspotData data, Transform cameraTransform, Action<CampusHotspot> clickCallback)
        {
            type = data.type;
            targetLocationIndex = data.targetLocationIndex;
            labelText = data.label;
            targetCamera = cameraTransform;
            OnClicked = clickCallback;

            // Calculate spherical position
            Quaternion rot = Quaternion.Euler(-data.pitch, data.yaw, 0f);
            Vector3 offset = rot * Vector3.forward * Mathf.Max(2f, data.distance);
            transform.position = (cameraTransform != null ? cameraTransform.position : Vector3.zero) + offset;

            UpdateVisuals();
            SetHovered(false);
        }

        public void UpdateVisuals()
        {
            if (iconText != null)
            {
                // Simple high-contrast symbols: Unicode arrow for navigation, 'i' for information
                iconText.text = (type == HotspotType.Navigation) ? "➜" : "ℹ";
            }

            if (labelDisplay != null)
            {
                labelDisplay.text = labelText;
                labelDisplay.gameObject.SetActive(!string.IsNullOrEmpty(labelText));
            }

            if (iconBackground != null)
            {
                iconBackground.color = (type == HotspotType.Navigation) ? navColor : infoColor;
            }
        }

        public void SetHovered(bool hovered)
        {
            isHovered = hovered;
            if (visualRoot != null)
            {
                visualRoot.localScale = defaultScale * (hovered ? 1.25f : 1.0f);
            }

            if (iconBackground != null)
            {
                if (hovered)
                {
                    iconBackground.color = hoverColor;
                }
                else
                {
                    iconBackground.color = (type == HotspotType.Navigation) ? navColor : infoColor;
                }
            }
        }

        public void TriggerInteraction()
        {
            OnClicked?.Invoke(this);
        }
    }
}
