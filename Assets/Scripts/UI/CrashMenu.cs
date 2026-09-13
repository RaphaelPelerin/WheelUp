using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WheelingMoto.Core;
using WheelingMoto.Gameplay;

namespace WheelingMoto.UI
{
    /// <summary>
    /// Menu de chute : le pilote est à terre, la caméra tourne autour de lui, et le joueur décide — repartir,
    /// ou regarder une publicité pour rattraper la prouesse tombée avec lui.
    ///
    /// La carte n'arrive qu'après un temps de latence : le joueur voit d'abord le roulé-boulé, on ne lui pose
    /// la question qu'ensuite. Le voile de fond ne capte aucun toucher, pour qu'on puisse continuer à faire
    /// tourner la caméra autour du corps pendant qu'on choisit.
    /// </summary>
    public class CrashMenu
    {
        // Temps laissé à la chute avant qu'on demande quoi faire.
        const float OpenDelay = 1.6f;
        const float FadeSpeed = 4f;
        const float CardWidth = 820f;
        const float FullHeight = 480f;
        const float ShortHeight = 320f;

        static readonly Color BackdropColor = new Color(0f, 0f, 0f, 0.55f);
        static readonly Color TitleColor = new Color(1f, 0.32f, 0.32f);
        static readonly Color RiskColor = new Color(1f, 0.62f, 0.2f);

        MotorcycleController controller;
        StuntScorer scorer;
        UITheme theme;
        CanvasGroup group;
        RectTransform card;
        TextMeshProUGUI titleText;
        TextMeshProUGUI hintText;
        TextMeshProUGUI riskText;
        TextMeshProUGUI adLabel;
        Button adButton;
        Button respawnButton;
        float openTimer;
        bool open;
        bool watchingAd;

        /// <summary>Vrai dès la chute, temps de latence compris : le HUD range ses commandes.</summary>
        public bool Showing => open || openTimer > 0f;

        public void Build(Transform root, UITheme uiTheme, MotorcycleController target)
        {
            controller = target;
            theme = uiTheme;
            if (controller == null) return;
            scorer = StuntScorer.Attach(controller);

            // À la racine du canvas, plein écran : le voile couvre aussi l'encoche et les coins arrondis.
            var backdrop = UIFactory.AddPanel(root, "CrashMenu", BackdropColor,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            // Il laisse passer les glissements : on peut tourner autour du pilote à terre pendant qu'on choisit.
            backdrop.raycastTarget = false;
            group = backdrop.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            card = (RectTransform)UIFactory.AddPanel(backdrop.transform, "Card",
                new Color(theme.Panel.r, theme.Panel.g, theme.Panel.b, 0.96f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-CardWidth * 0.5f, -FullHeight * 0.5f), new Vector2(CardWidth * 0.5f, FullHeight * 0.5f),
                rounded: true).transform;

            titleText = UIFactory.AddText(card, "Title", "CHUTE", 54, TitleColor, TextAnchor.MiddleCenter,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(32f, -104f), new Vector2(-32f, -32f), FontStyles.Bold);
            hintText = UIFactory.AddText(card, "Hint", "", 19, theme.TextMuted, TextAnchor.UpperCenter,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(32f, -166f), new Vector2(-32f, -106f));
            riskText = UIFactory.AddText(card, "Risk", "", 24, RiskColor, TextAnchor.MiddleCenter,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(32f, -216f), new Vector2(-32f, -172f), FontStyles.Bold);

            // Libellé posé ici pour que le texte existe ; il est réécrit à chaque ouverture avec les points en jeu.
            adButton = UIFactory.AddButton(card, "WatchAdButton", "REGARDER UNE PUB", theme.Accent, theme.Text, 28,
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(36f, 134f), new Vector2(-36f, 250f),
                OnWatchAd, ButtonKind.Primary);
            adLabel = adButton.GetComponentInChildren<TextMeshProUGUI>();

            respawnButton = UIFactory.AddButton(card, "RespawnButton", "REPARTIR", theme.PanelAlt, theme.Text, 26,
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(36f, 32f), new Vector2(-36f, 118f),
                OnRespawn);

            controller.Fell += OnFell;
            controller.Respawned += OnRespawned;
        }

        public void Dispose()
        {
            if (controller == null) return;
            controller.Fell -= OnFell;
            controller.Respawned -= OnRespawned;
        }

        public void Tick()
        {
            if (group == null) return;
            float dt = Time.deltaTime;

            if (openTimer > 0f)
            {
                openTimer -= dt;
                if (openTimer <= 0f) Open();
            }

            group.alpha = Mathf.MoveTowards(group.alpha, open ? 1f : 0f, FadeSpeed * dt);
            // Les boutons ne prennent la main qu'une fois la carte lisible : pas de clic sur un menu fantôme.
            group.blocksRaycasts = open && group.alpha > 0.5f;
            group.interactable = group.blocksRaycasts;
        }

        void OnFell()
        {
            openTimer = OpenDelay;
            open = false;
            watchingAd = false;
        }

        void OnRespawned()
        {
            openTimer = 0f;
            open = false;
        }

        void Open()
        {
            int lost = scorer != null ? scorer.LostPoints : 0;
            bool canRecover = lost > 0;

            titleText.text = canRecover ? "PROUESSE PERDUE" : "CHUTE";
            hintText.text = Hint(controller);
            riskText.text = canRecover ? StuntScoreHud.Format(lost) + " POINTS EN JEU" : "";
            riskText.gameObject.SetActive(canRecover);

            adButton.gameObject.SetActive(canRecover);
            SetShadowActive(canRecover);
            if (canRecover) RefreshAdLabel(lost);

            // Sans rien à racheter, la carte se résume à « repartir » : elle se resserre d'autant.
            float half = (canRecover ? FullHeight : ShortHeight) * 0.5f;
            card.offsetMin = new Vector2(-CardWidth * 0.5f, -half);
            card.offsetMax = new Vector2(CardWidth * 0.5f, half);

            SetInteractable(true);
            open = true;
        }

        void RefreshAdLabel(int points)
        {
            // Publicités retirées (achat unique) : la récompense est due sans rien montrer au joueur.
            string action = MonetizationManager.AdsRemoved ? "GARDER MES POINTS" : "REGARDER UNE PUB";
            adLabel.text = $"{action}\n<size=58%>{StuntScoreHud.Format(points)} points gardés</size>";
        }

        void OnWatchAd()
        {
            if (watchingAd || scorer == null) return;

            watchingAd = true;
            SetInteractable(false);
            if (!MonetizationManager.AdsRemoved) adLabel.text = "PUBLICITÉ EN COURS...";

            MonetizationManager.ShowRewardedAd(watched =>
            {
                watchingAd = false;
                if (!watched)
                {
                    // Pas de vidéo disponible : le choix reste entier, rien n'est perdu pour l'instant.
                    hintText.text = "Publicité indisponible pour le moment.";
                    RefreshAdLabel(scorer.LostPoints);
                    SetInteractable(true);
                    return;
                }

                // Prouesse encaissée (StuntScoreHud l'annonce en vert), puis retour en selle.
                scorer.RecoverLost();
                controller.RequestRespawn();
            });
        }

        void OnRespawn()
        {
            // Repartir sans racheter perd les points en jeu : le compteur le fait lui-même au respawn.
            if (!watchingAd) controller.RequestRespawn();
        }

        void SetInteractable(bool value)
        {
            adButton.interactable = value;
            respawnButton.interactable = value;
        }

        /// <summary>L'ombre portée du bouton principal est un panneau à part : elle suit sa visibilité.</summary>
        void SetShadowActive(bool value)
        {
            Transform shadow = card.Find("WatchAdButton_Shadow");
            if (shadow != null) shadow.gameObject.SetActive(value);
        }

        /// <summary>Conseil affiché après une chute, selon ce qui a mis le pilote à terre.</summary>
        internal static string Hint(MotorcycleController bike)
        {
            if (bike == null) return "";

            switch (bike.LastCrash)
            {
                case CrashCause.Stoppie:
                    return "Roue avant : relâche FREIN avant la zone rouge, rien ne te rattrape au-delà";
                case CrashCause.Wall:
                    return $"Un obstacle pris à plus de {Mathf.RoundToInt(bike.wallCrashSpeed * 3.6f)} km/h "
                        + "t'envoie par-dessus le guidon";
                case CrashCause.Landing:
                    return "Réception manquée : de si haut, la suspension ne pardonne pas";
                default:
                    return "Dose LEVER et FREIN pour rester dans la zone verte";
            }
        }
    }
}
