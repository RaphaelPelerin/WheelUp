using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace WheelingMoto.UI
{
    /// <summary>
    /// Construit l'interface entièrement par code (Canvas, panneaux, boutons, texte),
    /// sans dépendre de prefabs ou de TextMeshPro, pour que tout le menu tienne dans les scripts.
    /// </summary>
    public static class UIFactory
    {
        static Font cachedFont;

        public static Font UIFont
        {
            get
            {
                if (cachedFont == null)
                {
                    cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }
                return cachedFont;
            }
        }

        public static Canvas CreateRootCanvas(string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            EnsureEventSystem();
            return canvas;
        }

        public static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;

            var go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        public static RectTransform CreateUIObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        public static void SetRect(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
        }

        public static Image AddPanel(Transform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var rt = CreateUIObject(name, parent);
            SetRect(rt, anchorMin, anchorMax, offsetMin, offsetMax);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = color.a > 0f;
            return img;
        }

        public static Text AddText(Transform parent, string name, string text, int fontSize, Color color, TextAnchor alignment, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var rt = CreateUIObject(name, parent);
            SetRect(rt, anchorMin, anchorMax, offsetMin, offsetMax);
            var t = rt.gameObject.AddComponent<Text>();
            t.text = text;
            t.font = UIFont;
            t.fontSize = fontSize;
            t.color = color;
            t.alignment = alignment;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.raycastTarget = false;
            return t;
        }

        public static Button AddButton(Transform parent, string name, string label, Color bgColor, Color textColor, int fontSize, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, UnityAction onClick)
        {
            var img = AddPanel(parent, name, bgColor, anchorMin, anchorMax, offsetMin, offsetMax);
            img.raycastTarget = true;
            var btn = img.gameObject.AddComponent<Button>();

            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(1f, 1f, 1f, 0.5f);
            btn.colors = colors;

            if (!string.IsNullOrEmpty(label))
            {
                AddText(img.transform, "Label", label, fontSize, textColor, TextAnchor.MiddleCenter,
                    Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            }

            if (onClick != null)
            {
                btn.onClick.AddListener(onClick);
            }

            return btn;
        }

        /// <summary>Met à jour la couleur de fond "normale" d'un bouton créé avec AddButton (ex: état sélectionné).</summary>
        public static void SetButtonColor(Button button, Color color)
        {
            var img = button.GetComponent<Image>();
            img.color = color;
        }

        public static StepperWidget AddStepper(Transform parent, string name, UITheme theme, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, UnityAction onPrev, UnityAction onNext)
        {
            var rt = CreateUIObject(name, parent);
            SetRect(rt, anchorMin, anchorMax, offsetMin, offsetMax);

            var widget = new StepperWidget { Root = rt.gameObject };

            widget.PrevButton = AddButton(rt, "Prev", "-", theme.PanelAlt, theme.Text, 26,
                new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, new Vector2(64, 0), onPrev);

            widget.Label = AddText(rt, "Label", "", 20, theme.Text, TextAnchor.MiddleCenter,
                new Vector2(0, 0), new Vector2(1, 1), new Vector2(70, 0), new Vector2(-70, 0));

            widget.NextButton = AddButton(rt, "Next", "+", theme.PanelAlt, theme.Text, 26,
                new Vector2(1, 0), new Vector2(1, 1), new Vector2(-64, 0), Vector2.zero, onNext);

            return widget;
        }
    }
}
