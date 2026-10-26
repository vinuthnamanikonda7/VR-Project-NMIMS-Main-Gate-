using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CampusTour
{
    public class CampusTourUI : MonoBehaviour
    {
        [Header("Banner & Info")]
        [SerializeField] private Text locationNameText;
        [SerializeField] private GameObject infoPopupPanel;
        [SerializeField] private Text infoTitleText;
        [SerializeField] private Text infoDescriptionText;
        [SerializeField] private Button infoCloseButton;

        [Header("Menu")]
        [SerializeField] private Button menuToggleButton;
        [SerializeField] private GameObject menuPanel;
        [SerializeField] private Transform menuListContent;

        [Header("Status & Overlays")]
        [SerializeField] private GameObject loadingIndicator;
        [SerializeField] private GameObject errorPanel;
        [SerializeField] private Text errorMessageText;
        [SerializeField] private Button errorCloseButton;
        [SerializeField] private Image fadeOverlayImage;

        private void Awake()
        {
            SetupButtonListeners();
        }

        private void Start()
        {
            if (fadeOverlayImage != null)
            {
                Color c = fadeOverlayImage.color;
                c.a = 0f;
                fadeOverlayImage.color = c;
                fadeOverlayImage.gameObject.SetActive(false);
            }

            if (infoPopupPanel != null) infoPopupPanel.SetActive(false);
            if (errorPanel != null) errorPanel.SetActive(false);
            if (menuPanel != null) menuPanel.SetActive(true);
            if (loadingIndicator != null) loadingIndicator.SetActive(false);
        }

        private void SetupButtonListeners()
        {
            if (menuToggleButton != null)
            {
                menuToggleButton.onClick.AddListener(ToggleMenu);
            }

            if (infoCloseButton != null)
            {
                infoCloseButton.onClick.AddListener(HideInfoPopup);
            }

            if (errorCloseButton != null)
            {
                errorCloseButton.onClick.AddListener(HideErrorMessage);
            }
        }

        public void SetLocationName(string name)
        {
            if (locationNameText != null)
            {
                locationNameText.text = name;
            }
        }

        public void ShowLoading(bool show)
        {
            if (loadingIndicator != null)
            {
                loadingIndicator.SetActive(show);
            }
        }

        public void ShowErrorMessage(string message)
        {
            if (errorPanel != null)
            {
                if (errorMessageText != null) errorMessageText.text = message;
                errorPanel.SetActive(true);
            }
        }

        public void HideErrorMessage()
        {
            if (errorPanel != null)
            {
                errorPanel.SetActive(false);
            }
        }

        public void ShowInfoPopup(string title, string description)
        {
            if (infoPopupPanel != null)
            {
                if (infoTitleText != null) infoTitleText.text = title;
                if (infoDescriptionText != null) infoDescriptionText.text = description;
                infoPopupPanel.SetActive(true);
            }
        }

        public void HideInfoPopup()
        {
            if (infoPopupPanel != null)
            {
                infoPopupPanel.SetActive(false);
            }
        }

        public void ToggleMenu()
        {
            if (menuPanel != null)
            {
                menuPanel.SetActive(!menuPanel.activeSelf);
            }
        }

        public void PopulateLocationsMenu(List<LocationData> locations, Action<int> onSelectLocation)
        {
            if (menuListContent == null || locations == null) return;

            foreach (Transform child in menuListContent)
            {
                Destroy(child.gameObject);
            }

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Sprite uisprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");

            for (int i = 0; i < locations.Count; i++)
            {
                int index = i;
                LocationData loc = locations[i];

                GameObject btnObj = new GameObject("MenuItem_Loc" + (i + 1), typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement), typeof(BoxCollider));
                btnObj.transform.SetParent(menuListContent, false);

                BoxCollider boxCol = btnObj.GetComponent<BoxCollider>();
                boxCol.size = new Vector3(210f, 44f, 5f);

                LayoutElement le = btnObj.GetComponent<LayoutElement>();
                le.minHeight = 40f;
                le.preferredHeight = 44f;
                le.flexibleHeight = 1f;

                Image img = btnObj.GetComponent<Image>();
                if (uisprite != null)
                {
                    img.sprite = uisprite;
                    img.type = Image.Type.Sliced;
                }
                img.color = Color.white;

                Button btn = btnObj.GetComponent<Button>();
                ColorBlock cb = btn.colors;
                cb.normalColor = Color.white;
                cb.highlightedColor = new Color(0.92f, 0.95f, 1f, 1f);
                cb.pressedColor = new Color(0.82f, 0.88f, 0.95f, 1f);
                cb.selectedColor = new Color(0.90f, 0.94f, 1f, 1f);
                btn.colors = cb;

                btn.onClick.AddListener(() =>
                {
                    onSelectLocation?.Invoke(index);
                });

                GameObject txtObj = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                txtObj.transform.SetParent(btnObj.transform, false);

                RectTransform trt = txtObj.GetComponent<RectTransform>();
                trt.anchorMin = Vector2.zero;
                trt.anchorMax = Vector2.one;
                trt.offsetMin = new Vector2(6, 0);
                trt.offsetMax = new Vector2(-6, 0);

                Text txt = txtObj.GetComponent<Text>();
                txt.font = font;
                txt.fontSize = 16;
                txt.fontStyle = FontStyle.Normal;

                string displayName = loc.name;
                if (displayName.StartsWith("Location ", StringComparison.OrdinalIgnoreCase))
                {
                    int colonIdx = displayName.IndexOf(':');
                    if (colonIdx > 0)
                    {
                        displayName = displayName.Substring(0, colonIdx).Trim();
                    }
                }
                txt.text = displayName;
                txt.alignment = TextAnchor.MiddleCenter;
                txt.color = new Color(0.18f, 0.18f, 0.18f, 1f);
            }
        }

        public IEnumerator FadeScreen(float fromAlpha, float toAlpha, float duration)
        {
            if (fadeOverlayImage == null) yield break;

            fadeOverlayImage.gameObject.SetActive(true);
            float elapsed = 0f;
            Color color = fadeOverlayImage.color;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                color.a = Mathf.Lerp(fromAlpha, toAlpha, elapsed / duration);
                fadeOverlayImage.color = color;
                yield return null;
            }

            color.a = toAlpha;
            fadeOverlayImage.color = color;

            if (toAlpha <= 0.001f)
            {
                fadeOverlayImage.gameObject.SetActive(false);
            }
        }
    }
}
