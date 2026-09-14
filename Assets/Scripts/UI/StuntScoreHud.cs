using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WheelingMoto.Gameplay;

namespace WheelingMoto.UI
{
    /// <summary>
    /// Compteur de prouesses, façon Forza : pendant la figure, un encart discret sur la droite montre les points
    /// qui montent, le multiplicateur en cours et ce qui les fait monter (équilibre, prise de risque, lenteur).
    /// À la repose des deux roues, le total encaissé s'affiche en vert ; après une chute, en rouge, perdu.
    /// Le cumul de la partie et le record restent affichés dans le coin haut-gauche.
    /// </summary>
    public class StuntScoreHud
    {
        static readonly Color LiveColor = Color.white;
        static readonly Color BalanceColor = new Color(0.3f, 0.95f, 0.45f);
        static readonly Color RiskColor = new Color(1f, 0.55f, 0.15f);
        static readonly Color BankedColor = new Color(0.35f, 1f, 0.5f);
        static readonly Color FailedColor = new Color(1f, 0.32f, 0.32f);
        static readonly Color ChainTrackColor = new Color(1f, 1f, 1f, 0.25f);

        // Annonce d'encaissement : affichée en plein, puis estompée.
        const float FlashHold = 1f;
        const float FlashFade = 0.45f;
        const float FadeSpeed = 6f;
        const float PopTime = 0.25f;
        const float PopScale = 0.45f;

        // Séparateur de milliers : une espace simple, présente dans toutes les polices (12 450).
        static readonly NumberFormatInfo Number = new NumberFormatInfo { NumberGroupSeparator = " " };

        StuntScorer scorer;
        CanvasGroup group;
        Image chainFill;
        TextMeshProUGUI titleText;
        TextMeshProUGUI pointsText;
        TextMeshProUGUI multiplierText;
        TextMeshProUGUI bonusText;
        TextMeshProUGUI totalText;
        TextMeshProUGUI recordText;
        float flashTimer;
        float popTimer;
        int shownMultiplier = 1;

        /// <summary>Hauteur de l'encart en direct, en unités de canvas.</summary>
        public const float LiveHeight = 146f;
        const float LiveWidth = 370f;
        const float TotalWidth = 336f;

        /// <param name="parent">Zone sûre du HUD.</param>
        /// <param name="edge">Marge aux bords de la zone sûre, en unités de canvas.</param>
        /// <param name="liveTop">Distance du haut de l'encart en direct au haut de la zone sûre (sous VUE et PAUSE).</param>
        public void Build(Transform parent, UITheme theme, MotorcycleController controller, float edge, float liveTop)
        {
            scorer = StuntScorer.Attach(controller);
            if (scorer == null) return;

            // Colonne de droite, sous les boutons VUE et PAUSE et au-dessus des pédales : tout est aligné sur le
            // bord droit, hors du champ où le joueur regarde la route. Sans fond : seuls les chiffres se posent
            // sur la ville, détachés par un liseré sombre.
            var root = UIFactory.CreateUIObject("StuntScore", parent);
            UIFactory.SetRect(root, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-edge - LiveWidth, -liveTop - LiveHeight), new Vector2(-edge, -liveTop));
            group = root.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            group.alpha = 0f;

            titleText = Outlined(UIFactory.AddText(root, "Title", "", 18, LiveColor, TextAnchor.MiddleRight,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(12f, -32f), new Vector2(-12f, -8f), FontStyles.Bold), 0.14f);
            pointsText = Outlined(UIFactory.AddText(root, "Points", "0", 56, LiveColor, TextAnchor.MiddleRight,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(12f, -108f), new Vector2(-96f, -34f), FontStyles.Bold), 0.18f);
            multiplierText = Outlined(UIFactory.AddText(root, "Multiplier", "x1", 30, LiveColor, TextAnchor.MiddleRight,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-86f, -100f), new Vector2(-12f, -42f), FontStyles.Bold), 0.16f);
            bonusText = Outlined(UIFactory.AddText(root, "Bonus", "", 15, theme.TextMuted, TextAnchor.MiddleRight,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(12f, -132f), new Vector2(-12f, -110f)), 0.12f);

            // Barre de progression vers le cran de multiplicateur suivant.
            var track = UIFactory.AddPanel(root, "ChainTrack", ChainTrackColor,
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(14f, 6f), new Vector2(-12f, 12f), rounded: true);
            track.raycastTarget = false;
            chainFill = UIFactory.AddPanel(track.transform, "ChainFill", LiveColor,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, rounded: true);
            chainFill.raycastTarget = false;
            chainFill.type = Image.Type.Filled;
            chainFill.fillMethod = Image.FillMethod.Horizontal;
            chainFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            chainFill.fillAmount = 0f;

            // Cumul de la partie dans le coin haut-gauche, libéré par la vitesse passée au centre.
            totalText = Outlined(UIFactory.AddText(parent, "StuntTotal", "", 20, theme.Text, TextAnchor.MiddleLeft,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(edge, -edge - 34f), new Vector2(edge + TotalWidth, -edge), FontStyles.Bold), 0.14f);
            recordText = Outlined(UIFactory.AddText(parent, "StuntRecord", "", 15, theme.TextMuted, TextAnchor.MiddleLeft,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(edge, -edge - 58f), new Vector2(edge + TotalWidth, -edge - 34f)), 0.12f);

            scorer.Banked += OnBanked;
            scorer.Failed += OnFailed;
            RefreshTotals();
        }

        public void Dispose()
        {
            if (scorer == null) return;
            scorer.Banked -= OnBanked;
            scorer.Failed -= OnFailed;
        }

        public void Tick()
        {
            if (scorer == null) return;
            float dt = Time.deltaTime;

            if (popTimer > 0f) popTimer -= dt;
            multiplierText.rectTransform.localScale = Vector3.one * (1f + PopScale * Mathf.Clamp01(popTimer / PopTime));

            if (scorer.Active)
            {
                // Une nouvelle figure reprend la main sur l'annonce de la précédente.
                flashTimer = 0f;
                ShowLive();
                group.alpha = Mathf.MoveTowards(group.alpha, 1f, FadeSpeed * dt);
                return;
            }

            if (flashTimer > 0f)
            {
                flashTimer -= dt;
                group.alpha = Mathf.Clamp01(flashTimer / FlashFade);
                return;
            }

            group.alpha = Mathf.MoveTowards(group.alpha, 0f, FadeSpeed * dt);
        }

        void ShowLive()
        {
            WheelieZone zone = scorer.Zone;
            Color accent = zone == WheelieZone.Balance ? BalanceColor
                : zone == WheelieZone.Critical ? RiskColor
                : LiveColor;

            int multiplier = scorer.Multiplier;
            if (multiplier != shownMultiplier)
            {
                shownMultiplier = multiplier;
                popTimer = PopTime;
            }

            titleText.text = Label(scorer.Kind);
            pointsText.text = Format(scorer.PendingPoints);
            multiplierText.text = "x" + multiplier;
            bonusText.text = BonusLabel(zone);
            chainFill.fillAmount = scorer.ChainProgress;

            titleText.color = accent;
            pointsText.color = accent;
            multiplierText.color = accent;
            chainFill.color = accent;
            multiplierText.gameObject.SetActive(true);
        }

        /// <summary>Ce qui fait monter le compteur en ce moment : le joueur voit où sont les points.</summary>
        string BonusLabel(WheelieZone zone)
        {
            string quality = zone == WheelieZone.Balance ? "ÉQUILIBRE"
                : zone == WheelieZone.Critical ? "AU BORD !"
                : "";
            // Au pas, la roue est bien plus dure à tenir : c'est là que ça rapporte le plus.
            string slow = scorer.SpeedBonus >= 1.4f ? "AU RALENTI" : "";

            if (quality.Length > 0 && slow.Length > 0) return quality + "  ·  " + slow;
            return quality.Length > 0 ? quality : slow;
        }

        void OnBanked(StuntResult result)
        {
            RefreshTotals();
            if (result.Points < scorer.minAnnouncedPoints) return;

            titleText.text = result.Record ? "RECORD !" : Label(result.Kind) + " ENCAISSÉ";
            pointsText.text = "+" + Format(result.Points);
            multiplierText.text = "x" + result.Multiplier;
            bonusText.text = $"{result.Duration.ToString("0.0", CultureInfo.InvariantCulture)} s en l'air";
            chainFill.fillAmount = 1f;

            Paint(BankedColor);
            Flash();
        }

        void OnFailed(int lost)
        {
            titleText.text = "PROUESSE PERDUE";
            pointsText.text = "-" + Format(lost);
            bonusText.text = "la chute annule les points";
            chainFill.fillAmount = 0f;
            multiplierText.gameObject.SetActive(false);

            Paint(FailedColor);
            Flash();
        }

        void Paint(Color color)
        {
            titleText.color = color;
            pointsText.color = color;
            multiplierText.color = color;
            chainFill.color = color;
        }

        void Flash()
        {
            flashTimer = FlashHold + FlashFade;
            group.alpha = 1f;
            popTimer = PopTime;
            shownMultiplier = 1;
        }

        void RefreshTotals()
        {
            totalText.text = "PROUESSES  " + Format(scorer.Total);
            int best = scorer.Best;
            recordText.text = best > 0 ? "record  " + Format(best) : "";
        }

        /// <summary>
        /// Liseré sombre autour du texte : sans fond derrière lui, c'est ce qui le tient lisible sur une
        /// chaussée claire comme sur un mur sombre. Passer par fontMaterial (et non fontSharedMaterial) donne
        /// à ce texte sa propre copie du matériau, sinon le liseré gagnerait toute l'interface ; la marge du
        /// maillage est recalculée derrière, sans quoi le trait serait rogné.
        /// </summary>
        static TextMeshProUGUI Outlined(TextMeshProUGUI text, float width)
        {
            Material material = text.fontMaterial;
            material.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0f, 0f, 0f, 0.8f));
            material.SetFloat(ShaderUtilities.ID_OutlineWidth, width);
            text.UpdateMeshPadding();
            return text;
        }

        static string Label(StuntKind kind) => kind == StuntKind.Stoppie ? "ROUE AVANT" : "WHEELING";

        /// <summary>Points écrits comme partout ailleurs dans le jeu : milliers séparés par une espace.</summary>
        internal static string Format(int value) => value.ToString("N0", Number);
    }
}
