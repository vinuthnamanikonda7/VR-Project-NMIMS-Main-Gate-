using UnityEngine;

namespace CampusTour
{
    [RequireComponent(typeof(Collider))]
    public class Hotspot : MonoBehaviour
    {
        [SerializeField] private HotspotData data;
        [SerializeField] private Transform targetCamera;
        [SerializeField] private TextMesh labelTextMesh;
        [SerializeField] private Renderer ringRenderer;
        [SerializeField] private Renderer arrowRenderer;

        public HotspotData Data => data;

        public void Initialize(HotspotData hotspotData, Transform cameraTransform)
        {
            data = hotspotData;
            targetCamera = cameraTransform;
            UpdateTransformFromData();
            UpdateVisuals();
        }

        private void LateUpdate()
        {
            if (targetCamera != null)
            {
                // Always face camera
                transform.rotation = Quaternion.LookRotation(transform.position - targetCamera.position);
            }
        }

        public void UpdateTransformFromData()
        {
            if (data == null) return;

            float yawRad = data.yaw * Mathf.Deg2Rad;
            float pitchRad = data.pitch * Mathf.Deg2Rad;

            float x = data.distance * Mathf.Cos(pitchRad) * Mathf.Sin(yawRad);
            float y = data.distance * Mathf.Sin(pitchRad);
            float z = data.distance * Mathf.Cos(pitchRad) * Mathf.Cos(yawRad);

            Vector3 center = targetCamera != null ? targetCamera.position : Vector3.zero;
            transform.position = center + new Vector3(x, y, z);
        }

        private void UpdateVisuals()
        {
            if (labelTextMesh == null)
            {
                labelTextMesh = GetComponentInChildren<TextMesh>();
            }

            if (labelTextMesh != null && data != null)
            {
                labelTextMesh.text = data.title;
            }

            Color themeColor = (data != null && data.type == HotspotType.Navigation)
                ? new Color(0.2f, 0.85f, 1f, 0.95f) // Cyan
                : new Color(1f, 0.85f, 0.2f, 0.95f); // Amber

            if (ringRenderer != null)
            {
                ringRenderer.material.color = themeColor;
            }

            if (arrowRenderer != null)
            {
                arrowRenderer.material.color = themeColor;
            }
        }

        public void OnClicked()
        {
            CampusTourManager.Instance?.OnHotspotActivated(data);
        }
    }
}
