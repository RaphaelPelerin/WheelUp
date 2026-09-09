using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace WheelingMoto.UI
{
    /// <summary>Classes de boutons disponibles via AddButton (portent chacune un traitement visuel différent).</summary>
    public enum ButtonKind
    {
        /// <summary>Rectangle arrondi plein, sans relief : onglets, cartes, lignes de liste.</summary>
        Filled,
        /// <summary>Comme Filled, avec une ombre portée : action principale d'un écran (CTA).</summary>
        Primary,
    }

    /// <summary>
    /// Construit l'interface entièrement par code (Canvas, panneaux, boutons, texte) via TextMeshPro,
    /// sans dépendre de prefabs, pour que tout le menu tienne dans les scripts.
    /// </summary>
    public static class UIFactory
    {
        static Sprite cachedRoundedSprite;
        static TMP_FontAsset cachedFontAsset;
        static readonly Color ShadowColor = new Color(0f, 0f, 0f, 0.35f);
        static readonly Vector2 ShadowOffset = new Vector2(0f, -5f);

        /// <summary>
        /// TextMeshPro n'a aucune police tant que "TMP Essential Resources" n'est pas importé, et tout
        /// le texte reste alors invisible. On génère donc une police dynamique depuis la police intégrée
        /// de Unity : le menu s'affiche sans dépendre d'un asset importé.
        /// </summary>
        static TMP_FontAsset UIFont
        {
            get
            {
                if (cachedFontAsset == null)
                {
                    cachedFontAsset = TMP_FontAsset.CreateFontAsset(Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
                }
                return cachedFontAsset;
            }
        }

        /// <summary>Sprite arrondi standard de Unity (celui du Button par défaut), utilisé pour tous les panneaux "rounded".</summary>
        static Sprite RoundedSprite
        {
            get
            {
                if (cachedRoundedSprite == null)
                {
                    cachedRoundedSprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
                }
                return cachedRoundedSprite;
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

        /// <summary>Panneau de fond. Passer rounded=true pour un rectangle à coins arrondis (cartes, boutons, badges).</summary>
        public static Image AddPanel(Transform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, bool rounded = false)
        {
            var rt = CreateUIObject(name, parent);
            SetRect(rt, anchorMin, anchorMax, offsetMin, offsetMax);
            var img = rt.gameObject.AddComponent<Image>();
            if (rounded)
            {
                img.sprite = RoundedSprite;
                img.type = Image.Type.Sliced;
            }
            img.color = color;
            img.raycastTarget = color.a > 0f;
            return img;
        }

        public static TextMeshProUGUI AddText(Transform parent, string name, string text, int fontSize, Color color, TextAnchor alignment, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, FontStyles style = FontStyles.Normal)
        {
            var rt = CreateUIObject(name, parent);
            SetRect(rt, anchorMin, anchorMax, offsetMin, offsetMax);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = UIFont;
            t.text = text;
            t.fontSize = fontSize;
            t.color = color;
            t.alignment = ToTMPAlignment(alignment);
            t.fontStyle = style;
            t.raycastTarget = false;
            return t;
        }

        static TextAlignmentOptions ToTMPAlignment(TextAnchor anchor)
        {
            switch (anchor)
            {
                case TextAnchor.UpperLeft: return TextAlignmentOptions.TopLeft;
                case TextAnchor.UpperCenter: return TextAlignmentOptions.Top;
                case TextAnchor.UpperRight: return TextAlignmentOptions.TopRight;
                case TextAnchor.MiddleLeft: return TextAlignmentOptions.Left;
                case TextAnchor.MiddleRight: return TextAlignmentOptions.Right;
                case TextAnchor.LowerLeft: return TextAlignmentOptions.BottomLeft;
                case TextAnchor.LowerCenter: return TextAlignmentOptions.Bottom;
                case TextAnchor.LowerRight: return TextAlignmentOptions.BottomRight;
                default: return TextAlignmentOptions.Center;
            }
        }

        /// <summary>Bouton rectangulaire arrondi. kind=Primary ajoute une ombre portée pour les actions principales (CTA).</summary>
        public static Button AddButton(Transform parent, string name, string label, Color bgColor, Color textColor, int fontSize, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, UnityAction onClick, ButtonKind kind = ButtonKind.Filled)
        {
            if (kind == ButtonKind.Primary)
            {
                var shadow = AddPanel(parent, name + "_Shadow", ShadowColor, anchorMin, anchorMax,
                    offsetMin + ShadowOffset, offsetMax + ShadowOffset, rounded: true);
                shadow.raycastTarget = false;
            }

            var img = AddPanel(parent, name, bgColor, anchorMin, anchorMax, offsetMin, offsetMax, rounded: true);
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
                    Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, FontStyles.Bold);
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

        /// <summary>
        /// Item de navigation façon barre latérale : pas de fond plein, juste un libellé et une barre
        /// d'indicateur à gauche qui s'active quand l'item est sélectionné. locked=true grise l'item,
        /// le rend non cliquable et ajoute une étiquette "BIENTÔT".
        /// </summary>
        public static NavItemWidget AddNavItem(Transform parent, string name, string label, UITheme theme, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, UnityAction onClick, bool locked = false)
        {
            var rt = CreateUIObject(name, parent);
            SetRect(rt, anchorMin, anchorMax, offsetMin, offsetMax);

            var widget = new NavItemWidget { Root = rt.gameObject };

            var rowImage = rt.gameObject.AddComponent<Image>();
            rowImage.color = Color.clear;
            rowImage.raycastTarget = true;

            var btn = rt.gameObject.AddComponent<Button>();
            btn.interactable = !locked;
            if (onClick != null)
            {
                btn.onClick.AddListener(onClick);
            }
            widget.Button = btn;

            widget.Indicator = AddPanel(rt, "Indicator", theme.Accent, new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, new Vector2(5, 0));
            widget.Indicator.raycastTarget = false;
            widget.Indicator.gameObject.SetActive(false);

            widget.Label = AddText(rt, "Label", label, 26, locked ? theme.NavTextLocked : theme.NavTextInactive,
                TextAnchor.MiddleLeft, Vector2.zero, Vector2.one, new Vector2(30, 0), new Vector2(-20, 0),
                FontStyles.Bold | FontStyles.Italic);

            if (locked)
            {
                var tag = AddPanel(rt, "SoonTag", theme.PanelAlt, new Vector2(1, 0.5f), new Vector2(1, 0.5f),
                    new Vector2(-104, -16), new Vector2(-24, 16), rounded: true);
                tag.raycastTarget = false;
                AddText(tag.transform, "SoonLabel", "BIENTÔT", 12, theme.NavTextLocked, TextAnchor.MiddleCenter,
                    Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, FontStyles.Bold);
            }

            return widget;
        }
    }
}
