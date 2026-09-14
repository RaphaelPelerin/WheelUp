using System;
using UnityEngine;
using WheelingMoto.Core;

namespace WheelingMoto.UI
{
    /// <summary>
    /// Menu pause, entre la conduite et le menu principal : reprendre, ouvrir les Paramètres sans quitter la
    /// partie, ou revenir au menu. Le jeu est gelé tant qu'il est ouvert (temps, physique, son).
    /// Les Paramètres sont ceux du menu principal, repris tels quels (<see cref="SettingsMenu"/>) : un réglage
    /// changé ici l'est partout.
    /// </summary>
    public class PauseMenu
    {
        const float CardWidth = 640f;
        const float CardHeight = 470f;

        static readonly Color BackdropColor = new Color(0f, 0f, 0f, 0.7f);

        GameObject root;
        GameObject card;
        GameObject settingsView;
        Action closed;
        Action quit;
        float timeScaleBeforePause = 1f;
        bool open;

        /// <summary>Vrai tant que le menu pause (ou ses Paramètres) est affiché : le jeu est gelé.</summary>
        public bool Showing => open;

        /// <param name="onClosed">Appelé à la reprise : le HUD relit les réglages qui le concernent.</param>
        /// <param name="onQuit">
        /// Sortie vers le menu principal. Laissé nul, le menu y va directement ; le HUD s'en sert pour
        /// clore la session de mesure et montrer le bilan de run avant de rendre la main.
        /// </param>
        public void Build(Transform canvasRoot, UITheme theme, Action onClosed, Action onQuit = null)
        {
            closed = onClosed;
            quit = onQuit;

            // Plein écran et opaque aux touchers : rien du HUD ne se déclenche derrière la pause.
            var backdrop = UIFactory.AddPanel(canvasRoot, "PauseMenu", BackdropColor,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            root = backdrop.gameObject;
            var safeArea = UIFactory.CreateSafeArea(root.transform);

            card = UIFactory.AddPanel(safeArea, "Card", theme.Panel,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-CardWidth * 0.5f, -CardHeight * 0.5f), new Vector2(CardWidth * 0.5f, CardHeight * 0.5f),
                rounded: true).gameObject;
            var cardRect = card.transform;

            UIFactory.AddText(cardRect, "Title", "PAUSE", UITheme.FontDisplay, theme.Text, TextAnchor.MiddleCenter,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(32f, -110f), new Vector2(-32f, -30f), TMPro.FontStyles.Bold);

            UIFactory.AddButton(cardRect, "ResumeButton", "REPRENDRE", theme.Accent, theme.Text, UITheme.FontHeading,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(48f, -222f), new Vector2(-48f, -134f),
                Close, ButtonKind.Primary);
            UIFactory.AddButton(cardRect, "SettingsButton", "PARAMÈTRES", theme.PanelAlt, theme.Text, UITheme.FontBody,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(48f, -320f), new Vector2(-48f, -242f),
                ShowSettings);
            UIFactory.AddButton(cardRect, "QuitButton", "« MENU PRINCIPAL", theme.PanelAlt, theme.TextMuted, UITheme.FontLabel,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(48f, -418f), new Vector2(-48f, -340f),
                QuitToMainMenu);

            BuildSettingsView(safeArea, theme);

            root.SetActive(false);
        }

        /// <summary>Paramètres en plein écran, avec un retour vers la pause.</summary>
        void BuildSettingsView(Transform parent, UITheme theme)
        {
            var view = UIFactory.AddPanel(parent, "SettingsView", new Color(theme.Background.r, theme.Background.g, theme.Background.b, 0.97f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            settingsView = view.gameObject;

            UIFactory.AddButton(view.transform, "BackButton", "« RETOUR", theme.PanelAlt, theme.Text, UITheme.FontLabel,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -76f), new Vector2(244f, -20f),
                ShowCard);

            var content = UIFactory.CreateUIObject("Content", view.transform);
            UIFactory.SetRect(content, Vector2.zero, Vector2.one, new Vector2(0f, 0f), new Vector2(0f, -88f));
            new SettingsMenu().Build(content, theme);

            settingsView.SetActive(false);
        }

        public void Toggle()
        {
            if (open) Close();
            else Open();
        }

        public void Open()
        {
            if (open) return;
            open = true;

            // Tout se fige : conduite, physique de la chute, caméra, compteurs. Seule l'interface vit encore,
            // ses transitions tournant en temps réel.
            timeScaleBeforePause = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            AudioListener.pause = true;

            ShowCard();
            root.SetActive(true);
            root.transform.SetAsLastSibling();
        }

        /// <summary>Retour en arrière : des Paramètres vers la pause, de la pause vers le jeu.</summary>
        public void Back()
        {
            if (!open) return;
            if (settingsView.activeSelf) ShowCard();
            else Close();
        }

        public void Close()
        {
            if (!open) return;
            open = false;

            root.SetActive(false);
            Resume();
            closed?.Invoke();
        }

        /// <summary>Le jeu ne doit jamais rester gelé : ni après la pause, ni si la scène se ferme pendant.</summary>
        public void Dispose()
        {
            if (open) Resume();
        }

        void Resume()
        {
            Time.timeScale = timeScaleBeforePause;
            AudioListener.pause = false;
        }

        void ShowSettings()
        {
            card.SetActive(false);
            settingsView.SetActive(true);
        }

        void ShowCard()
        {
            settingsView.SetActive(false);
            card.SetActive(true);
        }

        void QuitToMainMenu()
        {
            open = false;
            Resume();

            if (quit != null)
            {
                quit();
                return;
            }

            SceneLoader.LoadMainMenu();
        }
    }
}
