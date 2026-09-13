using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WheelingMoto.UI
{
    /// <summary>
    /// Page d'information en plein écran, appelée par les boutons « i ». Elle existe parce que le
    /// jeu ne veut plus de petits textes : ce qui ne peut pas s'écrire en gros sur un écran de
    /// téléphone — une description, un tableau de probabilités, une mention légale — n'est pas
    /// rétréci, il est déplacé ici, où il a toute la place pour rester lisible.
    ///
    /// Un seul exemplaire vit par canvas, construit à la première demande et réutilisé ensuite :
    /// une page par bouton coûterait autant d'objets que de cartes à l'écran. Le champ statique
    /// redevient nul au changement de scène, ce qui déclenche naturellement sa reconstruction.
    /// </summary>
    public static class InfoPopup
    {
        const float PanelWidth = 980f;
        const float PanelHeight = 620f;

        static GameObject root;
        static TextMeshProUGUI titleText;
        static TextMeshProUGUI bodyText;

        public static void Show(Transform canvasRoot, UITheme theme, string title, string body)
        {
            if (canvasRoot == null || theme == null) return;

            if (root == null) Build(canvasRoot, theme);

            titleText.text = title;
            bodyText.text = body;

            // Remis au premier plan à chaque ouverture : les écrans construits après lui — l'ouverture
            // de coffre, par exemple — passeraient sinon par-dessus la page.
            root.transform.SetAsLastSibling();
            root.SetActive(true);
        }

        public static void Hide()
        {
            if (root != null) root.SetActive(false);
        }

        static void Build(Transform canvasRoot, UITheme theme)
        {
            var container = UIFactory.CreateUIObject("InfoPopup", canvasRoot);
            UIFactory.SetRect(container, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            root = container.gameObject;

            // Le voile couvre la dalle entière, coins arrondis et découpe compris, et avale les clics
            // destinés à l'écran du dessous. Un appui n'importe où referme : pas de cible à viser.
            var veil = UIFactory.AddPanel(container, "Veil", new Color(0f, 0f, 0f, 0.82f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            veil.raycastTarget = true;
            var dismiss = veil.gameObject.AddComponent<Button>();
            dismiss.transition = Selectable.Transition.None;
            dismiss.onClick.AddListener(Hide);

            // Le contenu, lui, se range dans la zone sûre.
            var frame = UIFactory.AddSafeArea(container, "SafeFrame").transform;

            var panel = UIFactory.AddPanel(frame, "Panel", theme.Panel,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-PanelWidth * 0.5f, -PanelHeight * 0.5f),
                new Vector2(PanelWidth * 0.5f, PanelHeight * 0.5f), rounded: true);
            // Le panneau ne prend pas le clic : il le laisse au voile, qui referme. Sans cela, un
            // appui sur le texte lui-même ne ferait rien et la page paraîtrait bloquée.
            panel.raycastTarget = false;

            UIFactory.AddPanel(panel.transform, "AccentBar", theme.Accent,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -6), Vector2.zero).raycastTarget = false;

            titleText = UIFactory.AddText(panel.transform, "Title", "", UITheme.FontTitle, theme.Text,
                TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(44, -96), new Vector2(-44, -30), FontStyles.Bold | FontStyles.Italic);

            bodyText = UIFactory.AddText(panel.transform, "Body", "", UITheme.FontBody, theme.TextMuted,
                TextAnchor.UpperLeft, Vector2.zero, Vector2.one, new Vector2(44, 96), new Vector2(-44, -110));

            UIFactory.AddText(panel.transform, "Hint", "APPUIE POUR FERMER", UITheme.FontLabel,
                theme.NavTextInactive, TextAnchor.MiddleCenter, new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(44, 30), new Vector2(-44, 82), FontStyles.Bold);

            root.SetActive(false);
        }
    }
}
