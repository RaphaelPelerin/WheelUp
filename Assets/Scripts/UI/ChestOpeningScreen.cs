using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WheelingMoto.Core;
using WheelingMoto.Data;

namespace WheelingMoto.UI
{
    /// <summary>
    /// Écran plein cadre d'ouverture de coffre, dans l'esprit de Clash Royale : le menu disparaît, le
    /// coffre occupe l'écran, tremble, s'ouvre, puis ses lots se révèlent un par un du plus commun au
    /// plus rare. Un clic n'importe où fait avancer la séquence, et l'écran se referme sur le bouton final.
    /// </summary>
    [DisallowMultipleComponent]
    public class ChestOpeningScreen : MonoBehaviour
    {
        const float CardWidth = 300f;
        const float CardHeight = 320f;
        const float CardGap = 26f;
        const float RevealInterval = 0.55f;
        const float PopDuration = 0.34f;
        const float FadeDuration = 0.22f;

        /// <summary>Durée de maintien nécessaire pour faire sauter le couvercle.</summary>
        const float HoldDuration = 1.15f;
        /// <summary>Retombée de la jauge quand le doigt se lève : plus rapide que la montée.</summary>
        const float HoldDecayRate = 2.2f;
        const float HoldBarWidth = 240f;

        class RewardCard
        {
            public GameObject Root;
            public CanvasGroup Group;
            public RectTransform Rect;
        }

        // Recyclés d'une ouverture à l'autre : un rig neuf par carte fuirait caméras et RenderTextures.
        readonly List<RewardIcon> iconPool = new List<RewardIcon>();

        public GameObject Root { get; private set; }
        public bool IsOpen => Root != null && Root.activeSelf;

        UITheme theme;
        CanvasGroup rootGroup;

        Image backdrop;
        Image glow;
        RectTransform chestVisual;
        RawImage chestRender;
        GameObject chestPlaceholder;
        TextMeshProUGUI chestPlaceholderText;
        ChestPreview preview;

        TextMeshProUGUI titleText;
        TextMeshProUGUI subtitleText;
        TextMeshProUGUI hintText;
        Button closeButton;

        ChestTouchSurface surface;
        GameObject holdGauge;
        RectTransform holdFill;

        RectTransform rewardRow;
        readonly List<RewardCard> cards = new List<RewardCard>();

        IList<ChestReward> currentRewards;
        Action onClosed;

        int revealed;
        bool waitingForOpen;
        bool sequenceRunning;
        bool holding;
        float holdProgress;

        public static ChestOpeningScreen Create(Transform canvasRoot, UITheme theme)
        {
            var go = new GameObject("ChestOpeningScreen");
            var screen = go.AddComponent<ChestOpeningScreen>();
            screen.theme = theme;
            screen.Build(canvasRoot);
            return screen;
        }

        void Build(Transform canvasRoot)
        {
            var root = UIFactory.AddPanel(canvasRoot, "ChestOpening", Color.clear,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            root.raycastTarget = false;
            Root = root.gameObject;
            rootGroup = Root.AddComponent<CanvasGroup>();

            // Fond opaque : il avale les clics du menu resté dessous et porte tous les gestes de
            // l'écran — maintien pour ouvrir, glissement pour tourner le coffre, appui bref pour
            // enchaîner les révélations. Une seule surface, pour qu'aucun geste ne tombe à côté.
            backdrop = UIFactory.AddPanel(Root.transform, "Backdrop", new Color(0.035f, 0.04f, 0.055f, 0.985f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            backdrop.raycastTarget = true;

            surface = backdrop.gameObject.AddComponent<ChestTouchSurface>();
            surface.Pressed = OnPressed;
            surface.HoldEnded = OnHoldEnded;
            surface.Tapped = OnScreenTapped;
            surface.Dragged = OnDragged;

            glow = UIFactory.AddGlow(Root.transform, "Glow", new Color(1f, 1f, 1f, 0f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-820, -760), new Vector2(820, 760));

            // Titre et sous-titre sont poussés vers le haut du cadre : la place qu'ils libèrent
            // revient au coffre, qui est le sujet de l'écran.
            titleText = UIFactory.AddText(Root.transform, "Title", "", 46, theme.Text, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-600, 428), new Vector2(600, 502),
                FontStyles.Bold | FontStyles.Italic);

            subtitleText = UIFactory.AddText(Root.transform, "Subtitle", "", 20, theme.TextMuted, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-600, 392), new Vector2(600, 428));

            BuildChestStage();

            rewardRow = UIFactory.CreateUIObject("Rewards", Root.transform);
            UIFactory.SetRect(rewardRow, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-960, -CardHeight * 0.5f), new Vector2(960, CardHeight * 0.5f));

            BuildHoldGauge();

            hintText = UIFactory.AddText(Root.transform, "Hint", "", 20, theme.NavTextInactive, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-600, -505), new Vector2(600, -462),
                FontStyles.Bold);

            closeButton = UIFactory.AddButton(Root.transform, "Close", "TERMINÉ", theme.Accent, Color.white, 22,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-220, -512), new Vector2(220, -436),
                Close, ButtonKind.Primary);
            SetCloseVisible(false);

            Root.SetActive(false);
        }

        void BuildChestStage()
        {
            // Le coffre prend tout le cadre laissé libre entre le sous-titre et la jauge. Il était
            // auparavant cantonné à une vignette au centre, alors que rien d'autre ne s'affiche
            // pendant l'ouverture.
            var stage = UIFactory.CreateUIObject("ChestStage", Root.transform);
            UIFactory.SetRect(stage, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-520, -400), new Vector2(520, 380));

            // L'éclat est créé AVANT le conteneur, donc dessiné derrière lui : posé par-dessus, il
            // repeignait tout le rendu — coffre et confettis — à la couleur de la rareté. Derrière,
            // il fait contre-jour et laisse les particules garder leur propre teinte.
            var flash = UIFactory.AddGlow(stage, "Flash", new Color(1f, 1f, 1f, 0f),
                Vector2.zero, Vector2.one, new Vector2(-180, -180), new Vector2(180, 180));

            // Conteneur secoué par l'animation : il porte le rendu 3D et le repli visuel.
            chestVisual = UIFactory.CreateUIObject("ChestVisual", stage);
            UIFactory.SetRect(chestVisual, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var placeholderPanel = UIFactory.AddPanel(chestVisual, "Placeholder", theme.PanelAlt,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-240, -190), new Vector2(240, 190),
                rounded: true);
            // Sans cela, le repli visuel intercepte les gestes destinés au fond et le coffre
            // devient impossible à tourner tant qu'aucun modèle 3D n'est disponible.
            placeholderPanel.raycastTarget = false;
            chestPlaceholder = placeholderPanel.gameObject;
            chestPlaceholderText = UIFactory.AddText(chestPlaceholder.transform, "Label", "", 26, theme.TextMuted,
                TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, new Vector2(14, 14), new Vector2(-14, -14),
                FontStyles.Bold | FontStyles.Italic);

            chestRender = UIFactory.AddRawImage(chestVisual, "ChestRender", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);


            preview = ChestPreview.Create(chestRender, chestVisual, flash);
        }

        /// <summary>
        /// Jauge de maintien, sous le coffre. Elle donne au geste un point d'arrivée visible : sans
        /// elle, un appui maintenu qui n'ouvre pas encore se lit comme un contrôle qui ne répond pas.
        /// </summary>
        void BuildHoldGauge()
        {
            var track = UIFactory.AddPanel(Root.transform, "HoldTrack", theme.PanelAlt,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-HoldBarWidth * 0.5f, -452), new Vector2(HoldBarWidth * 0.5f, -428),
                rounded: true);
            track.raycastTarget = false;
            holdGauge = track.gameObject;

            var fill = UIFactory.AddPanel(track.transform, "HoldFill", theme.Accent,
                Vector2.zero, new Vector2(0f, 1f), new Vector2(4, 4), new Vector2(-4, -4), rounded: true);
            fill.raycastTarget = false;
            holdFill = fill.rectTransform;

            holdGauge.SetActive(false);
        }

        void SetHoldGauge(float progress)
        {
            bool visible = waitingForOpen && progress > 0.01f;
            holdGauge.SetActive(visible);
            if (!visible) return;

            // La largeur passe par l'ancre plutôt que par sizeDelta : le remplissage suit alors la
            // largeur du rail, quelle que soit la résolution.
            holdFill.anchorMax = new Vector2(Mathf.Clamp01(progress), 1f);
        }

        /// <summary>
        /// Fait monter ou redescendre la charge selon que le doigt est posé. Le coffre tremble et
        /// aspire ses particules au même rythme : le maintien se lit sur le coffre, pas seulement
        /// sur la jauge.
        /// </summary>
        void Update()
        {
            if (!IsOpen || !waitingForOpen) return;

            float previous = holdProgress;
            holdProgress = holding
                ? Mathf.Min(1f, holdProgress + Time.deltaTime / HoldDuration)
                : Mathf.Max(0f, holdProgress - Time.deltaTime * HoldDecayRate);

            // Jauge retombée à zéro et doigt levé : rien à réafficher, et surtout rien à réappliquer
            // au coffre, qui doit reprendre son tournoiement de repos sans être secoué à chaque image.
            if (previous <= 0f && holdProgress <= 0f) return;

            SetHoldGauge(holdProgress);
            preview.SetCharge(holdProgress);

            if (holdProgress < 1f) return;

            waitingForOpen = false;
            holding = false;
            holdProgress = 0f;
            holdGauge.SetActive(false);
            StartCoroutine(OpenRoutine());
        }

        /// <summary>
        /// Ouvre l'écran sur un coffre déjà payé et déjà attribué : la séquence ne fait que révéler des
        /// lots acquis, rien ne peut être perdu si le joueur quitte en cours de route.
        /// </summary>
        public void Play(ChestInfo chest, IList<ChestReward> rewards, Action closedCallback)
        {
            if (chest == null || rewards == null || rewards.Count == 0) return;

            currentRewards = rewards;
            onClosed = closedCallback;
            revealed = 0;

            Root.SetActive(true);
            Root.transform.SetAsLastSibling();

            titleText.text = chest.Name.ToUpperInvariant();
            subtitleText.text = $"{rewards.Count} récompenses";
            hintText.text = "MAINTIENS POUR OUVRIR  •  GLISSE POUR TOURNER";
            SetCloseVisible(false);

            var best = rewards[rewards.Count - 1].Rarity;
            var bestColor = ChestCatalog.RarityColor(best);
            glow.color = new Color(bestColor.r, bestColor.g, bestColor.b, 0.22f);
            glow.rectTransform.localScale = Vector3.one;

            ShowChestClosed(chest);
            BuildCards(rewards);

            chestVisual.localScale = Vector3.one;
            SetChestAlpha(1f);

            waitingForOpen = true;
            sequenceRunning = false;
            holding = false;
            holdProgress = 0f;
            SetHoldGauge(0f);
            StartCoroutine(FadeRoot(0f, 1f));
        }

        void ShowChestClosed(ChestInfo chest)
        {
            bool has3D = preview.ShowChest(chest);
            chestPlaceholder.SetActive(!has3D);
            chestPlaceholderText.text = chest.Name;
        }

        void BuildCards(IList<ChestReward> rewards)
        {
            foreach (var card in cards)
            {
                if (card.Root != null) Destroy(card.Root);
            }
            cards.Clear();

            int count = rewards.Count;
            float total = count * CardWidth + (count - 1) * CardGap;
            float x = -total * 0.5f;

            for (int i = 0; i < count; i++)
            {
                cards.Add(BuildCard(rewards[i], x, i));
                x += CardWidth + CardGap;
            }

            // Un coffre à 3 lots après un coffre à 4 : l'icône en trop doit lâcher son modèle.
            for (int i = count; i < iconPool.Count; i++) iconPool[i].Hide();
        }

        RewardCard BuildCard(ChestReward reward, float x, int index)
        {
            var rarityColor = ChestCatalog.RarityColor(reward.Rarity);

            var holder = UIFactory.CreateUIObject("RewardCard", rewardRow);
            UIFactory.SetRect(holder, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(x - 3, -CardHeight * 0.5f - 3), new Vector2(x + CardWidth + 3, CardHeight * 0.5f + 3));

            // Le liseré de rareté est le panneau du dessous, que le corps de la carte laisse dépasser de 3 px.
            var frame = UIFactory.AddPanel(holder, "Frame", rarityColor,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, rounded: true);
            frame.raycastTarget = false;

            var body = UIFactory.AddPanel(holder, "Body", theme.Panel,
                Vector2.zero, Vector2.one, new Vector2(3, 3), new Vector2(-3, -3), rounded: true);
            body.raycastTarget = false;

            var swatchBack = UIFactory.AddGlow(body.transform, "SwatchGlow",
                new Color(reward.Swatch.r, reward.Swatch.g, reward.Swatch.b, 0.55f),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-96, -180), new Vector2(96, 12));

            var swatch = UIFactory.AddGlow(swatchBack.transform, "Swatch", reward.Swatch,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-46, -46), new Vector2(46, 46));

            // Icône 3D par-dessus la pastille. Sans modèle, la pastille pleine reste le repli.
            var iconRender = UIFactory.AddRawImage(swatchBack.transform, "Icon",
                Vector2.zero, Vector2.one, new Vector2(18, 18), new Vector2(-18, -18));

            bool hasIcon = GetIcon(index, iconRender).Show(iconRender, reward.IconPath, reward.Swatch);
            swatch.gameObject.SetActive(!hasIcon);

            UIFactory.AddText(body.transform, "Kind", KindLabel(reward.Kind), 14, rarityColor, TextAnchor.MiddleCenter,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(12, -178), new Vector2(-12, -150), FontStyles.Bold);

            UIFactory.AddText(body.transform, "Title", reward.Title, 24, theme.Text, TextAnchor.MiddleCenter,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(12, -238), new Vector2(-12, -182),
                FontStyles.Bold | FontStyles.Italic);

            UIFactory.AddText(body.transform, "Detail", reward.Detail, 14, theme.TextMuted, TextAnchor.UpperCenter,
                new Vector2(0, 0), new Vector2(1, 1), new Vector2(14, 14), new Vector2(-14, -242));

            var group = holder.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            holder.localScale = Vector3.one * 0.6f;

            return new RewardCard { Root = holder.gameObject, Group = group, Rect = holder };
        }

        /// <summary>Rig d'icône du rang demandé, créé à la première utilisation puis réutilisé.</summary>
        RewardIcon GetIcon(int index, RawImage output)
        {
            while (iconPool.Count <= index) iconPool.Add(RewardIcon.Create(output));

            return iconPool[index];
        }

        static string KindLabel(ChestRewardKind kind)
        {
            switch (kind)
            {
                case ChestRewardKind.Moto: return "MOTO";
                case ChestRewardKind.Upgrade: return "AMÉLIORATION";
                case ChestRewardKind.Paint: return "PEINTURE";
                default: return "PIÈCES";
            }
        }

        void OnPressed()
        {
            if (waitingForOpen) holding = true;
        }

        void OnHoldEnded()
        {
            holding = false;
        }

        /// <summary>
        /// Un appui bref fait sauter l'attente entre deux révélations. Il n'ouvre plus le coffre :
        /// l'ouverture demande un maintien, sinon un clic perdu déclencherait toute la séquence.
        /// </summary>
        void OnScreenTapped()
        {
            if (sequenceRunning) RevealNext();
        }

        /// <summary>Le glissement tourne le coffre, à tout moment de la séquence.</summary>
        void OnDragged(Vector2 delta)
        {
            if (preview != null) preview.Drag(delta);
        }

        IEnumerator OpenRoutine()
        {
            sequenceRunning = true;
            hintText.text = "";

            var bestRarity = currentRewards[currentRewards.Count - 1].Rarity;
            var best = ChestCatalog.RarityColor(bestRarity);
            var confetti = ChestCatalog.ConfettiColor(bestRarity);

            bool finished = false;
            preview.PlayOpening(best, confetti,
                () => StartCoroutine(GlowPunch(best)), () => finished = true);
            while (!finished) yield return null;

            // Le coffre s'efface pour laisser la place aux lots, comme dans Brawl Stars.
            yield return StartCoroutine(FadeChestOut());

            hintText.text = "APPUIE POUR CONTINUER";

            while (revealed < currentRewards.Count)
            {
                int shownBefore = revealed;
                RevealNext();

                float wait = 0f;
                while (wait < RevealInterval && revealed == shownBefore + 1)
                {
                    wait += Time.deltaTime;
                    yield return null;
                }
            }

            sequenceRunning = false;
            hintText.text = "";
            SetCloseVisible(true);
        }

        /// <summary>
        /// Coup de halo derrière le coffre à l'instant de l'éclat. La caméra d'aperçu n'a pas de
        /// post-traitement : c'est cette montée au niveau de l'UI qui fait lire une vraie source de
        /// lumière plutôt qu'un tas de particules claires.
        /// </summary>
        IEnumerator GlowPunch(Color color)
        {
            const float rise = 0.10f;
            const float fall = 0.85f;

            float elapsed = 0f;
            while (elapsed < rise + fall)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed <= rise ? elapsed / rise : 1f - (elapsed - rise) / fall);

                glow.color = new Color(color.r, color.g, color.b, 0.22f + 0.42f * t);
                glow.rectTransform.localScale = Vector3.one * (0.86f + 0.32f * t);
                yield return null;
            }

            glow.color = new Color(color.r, color.g, color.b, 0.22f);
            glow.rectTransform.localScale = Vector3.one;
        }

        void RevealNext()
        {
            if (revealed >= cards.Count) return;

            var card = cards[revealed];
            revealed++;
            StartCoroutine(PopCard(card));
        }

        IEnumerator PopCard(RewardCard card)
        {
            float elapsed = 0f;
            while (elapsed < PopDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / PopDuration);

                card.Group.alpha = t;
                // Léger dépassement puis retour : la carte "claque" au lieu de grandir platement.
                float scale = 0.6f + 0.48f * t - 0.08f * Mathf.Sin(t * Mathf.PI) * (1f - t);
                card.Rect.localScale = Vector3.one * Mathf.Min(scale, 1.06f);
                yield return null;
            }

            card.Group.alpha = 1f;
            card.Rect.localScale = Vector3.one;
        }

        IEnumerator FadeChestOut()
        {
            float elapsed = 0f;
            while (elapsed < FadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / FadeDuration);
                SetChestAlpha(1f - t);
                chestVisual.localScale = Vector3.one * (1f - 0.12f * t);
                yield return null;
            }

            SetChestAlpha(0f);
        }

        void SetChestAlpha(float alpha)
        {
            if (chestRender != null) chestRender.color = new Color(1f, 1f, 1f, alpha);

            var placeholder = chestPlaceholder.GetComponent<Image>();
            if (placeholder != null)
            {
                placeholder.color = new Color(theme.PanelAlt.r, theme.PanelAlt.g, theme.PanelAlt.b, theme.PanelAlt.a * alpha);
            }
            chestPlaceholderText.color = new Color(theme.TextMuted.r, theme.TextMuted.g, theme.TextMuted.b, alpha);
        }

        void SetCloseVisible(bool visible)
        {
            closeButton.gameObject.SetActive(visible);

            // AddButton(Primary) crée l'ombre comme frère juste avant le bouton : elle suit son état.
            var shadow = Root.transform.Find("Close_Shadow");
            if (shadow != null) shadow.gameObject.SetActive(visible);
        }

        void Close()
        {
            if (!IsOpen) return;

            waitingForOpen = false;
            holding = false;
            holdProgress = 0f;
            holdGauge.SetActive(false);

            StopAllCoroutines();
            StartCoroutine(CloseRoutine());
        }

        IEnumerator CloseRoutine()
        {
            yield return StartCoroutine(FadeRoot(1f, 0f));

            Root.SetActive(false);
            onClosed?.Invoke();
        }

        IEnumerator FadeRoot(float from, float to)
        {
            rootGroup.alpha = from;

            float elapsed = 0f;
            while (elapsed < FadeDuration)
            {
                elapsed += Time.deltaTime;
                rootGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / FadeDuration));
                yield return null;
            }

            rootGroup.alpha = to;
        }
    }
}
