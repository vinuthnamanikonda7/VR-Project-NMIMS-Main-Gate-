using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.XR;

namespace CampusTour
{
    public class VRControllerLaser : MonoBehaviour
    {
        [Header("Hand Settings")]
        [SerializeField] private bool isLeftHand = false;
        [SerializeField] private LayerMask hitLayers = ~0;
        [SerializeField] private float maxDistance = 30f;

        [Header("Visuals")]
        [SerializeField] private LineRenderer lineRenderer;
        [SerializeField] private GameObject hitReticle;

        private XRController currentController;
        private bool isTracking = false;
        private Hotspot currentHoveredHotspot;
        private UnityEngine.UI.Button currentHoveredButton;

        public bool IsActiveAndTracking => isTracking;
        public Ray CurrentRay => new Ray(transform.position, transform.forward);

        private void Awake()
        {
            SetupVisuals();
        }

        private void SetupVisuals()
        {
            if (lineRenderer == null)
            {
                lineRenderer = gameObject.AddComponent<LineRenderer>();
                lineRenderer.startWidth = 0.015f;
                lineRenderer.endWidth = 0.005f;
                lineRenderer.positionCount = 2;
                lineRenderer.useWorldSpace = true;

                Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit")
                                  ?? Shader.Find("Sprites/Default")
                                  ?? Shader.Find("Unlit/Color");

                Material laserMat = new Material(unlitShader);
                laserMat.color = new Color(0.2f, 0.7f, 1f, 0.8f);
                lineRenderer.material = laserMat;
            }

            if (hitReticle == null)
            {
                hitReticle = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                hitReticle.name = "LaserReticle_" + (isLeftHand ? "L" : "R");
                hitReticle.transform.localScale = Vector3.one * 0.06f;

                Collider col = hitReticle.GetComponent<Collider>();
                if (col != null) Destroy(col);

                Renderer rend = hitReticle.GetComponent<Renderer>();
                if (rend != null)
                {
                    rend.material = lineRenderer.material;
                }
                hitReticle.SetActive(false);
            }
        }

        private void Update()
        {
            FindControllerDevice();
            UpdateTrackingPositionRotation();

            if (isTracking)
            {
                UpdateLaserRaycast();
                CheckTriggerInput();
            }
            else
            {
                if (lineRenderer != null && lineRenderer.enabled) lineRenderer.enabled = false;
                if (hitReticle != null && hitReticle.activeSelf) hitReticle.SetActive(false);
            }
        }

        private void FindControllerDevice()
        {
            foreach (var device in InputSystem.devices)
            {
                if (device is XRController controller)
                {
                    string name = controller.name.ToLower();
                    bool isLeft = name.Contains("left");
                    bool isRight = name.Contains("right");

                    if (isLeftHand && isLeft)
                    {
                        currentController = controller;
                        break;
                    }
                    else if (!isLeftHand && (isRight || (!isLeft && currentController == null)))
                    {
                        currentController = controller;
                        break;
                    }
                }
            }
        }

        private void UpdateTrackingPositionRotation()
        {
            if (currentController == null)
            {
                isTracking = false;
                return;
            }

            var posControl = currentController.GetChildControl<Vector3Control>("devicePosition");
            var rotControl = currentController.GetChildControl<QuaternionControl>("deviceRotation");

            if (posControl != null && rotControl != null)
            {
                Vector3 pos = posControl.ReadValue();
                Quaternion rot = rotControl.ReadValue();

                if (pos != Vector3.zero || rot != Quaternion.identity)
                {
                    transform.localPosition = pos;
                    transform.localRotation = rot;
                    isTracking = true;
                    return;
                }
            }

            isTracking = false;
        }

        private void UpdateLaserRaycast()
        {
            if (lineRenderer == null) return;
            lineRenderer.enabled = true;

            Ray ray = CurrentRay;
            Vector3 endPoint = ray.origin + ray.direction * maxDistance;

            if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, hitLayers))
            {
                endPoint = hit.point;
                Hotspot spot = hit.collider.GetComponentInParent<Hotspot>();
                currentHoveredHotspot = spot;

                UnityEngine.UI.Button btn = hit.collider.GetComponentInParent<UnityEngine.UI.Button>();
                currentHoveredButton = btn;

                if (hitReticle != null)
                {
                    hitReticle.SetActive(true);
                    hitReticle.transform.position = hit.point;
                    hitReticle.transform.rotation = Quaternion.LookRotation(hit.normal);
                }
            }
            else
            {
                currentHoveredHotspot = null;
                currentHoveredButton = null;
                if (hitReticle != null) hitReticle.SetActive(false);
            }

            lineRenderer.SetPosition(0, ray.origin);
            lineRenderer.SetPosition(1, endPoint);
        }

        private void CheckTriggerInput()
        {
            if (currentController == null) return;

            var triggerBtn = currentController.GetChildControl<ButtonControl>("triggerPressed")
                          ?? currentController.GetChildControl<ButtonControl>("trigger")
                          ?? currentController.GetChildControl<ButtonControl>("primaryButton");

            if (triggerBtn != null && triggerBtn.wasPressedThisFrame)
            {
                if (currentHoveredButton != null)
                {
                    currentHoveredButton.onClick.Invoke();
                }
                else if (currentHoveredHotspot != null)
                {
                    currentHoveredHotspot.OnClicked();
                }
            }
        }

        public void SetHand(bool leftHand)
        {
            isLeftHand = leftHand;
        }

        private void OnDestroy()
        {
            if (hitReticle != null) Destroy(hitReticle);
        }
    }
}
