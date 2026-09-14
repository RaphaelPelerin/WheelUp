using TMPro;
using UnityEngine;
using WheelingMoto.Gameplay;

namespace WheelingMoto.UI
{
    /// <summary>
    /// Compteur de vitesse du HUD, en haut au centre : un cadran à aiguille (<see cref="SpeedDial"/>) et la vitesse
    /// en grands chiffres en son cœur. Seule la vitesse est lue : la boîte est automatique, rapport et régime
    /// chargeaient le haut de l'écran pour des chiffres sur lesquels le joueur n'a pas la main.
    ///
    /// Le cadran vit comme celui d'une vraie moto :
    /// - l'aiguille suit la vitesse avec l'inertie d'une aiguille à ressort : elle traîne un peu sur un départ
    ///   arraché, dépasse légèrement sur un freinage sec et rebondit contre ses butées ;
    /// - elle frémit quand le moteur tire, d'autant plus qu'il est haut dans les tours ;
    /// - l'arc vire au rouge de la marque en haut du cadran ;
    /// - les chiffres défilent au lieu de sauter, et prennent un léger élan sous une forte accélération.
    ///
    /// Tout suit le temps du jeu : la pause fige l'aiguille là où elle est.
    /// </summary>
    public class SpeedReadout
    {
        // Assez large pour qu'une vitesse à trois chiffres garde de l'air entre elle et les graduations.
        const float Radius = 124f;
        const float Thickness = 9f;
        const float Sweep = 240f;
        // Marge autour de l'arc : ses bouts arrondis débordent du rayon.
        const float Padding = 14f;

        // Échelle du cadran : un peu au-delà de la vitesse de pointe, arrondie à une graduation ronde, avec
        // au plus MaxIntervals intervalles pour que les graduations restent lisibles.
        static readonly int[] ScaleSteps = { 10, 20, 25, 50 };
        const int MaxIntervals = 12;
        const float ScaleHeadroom = 1.08f;

        // Aiguille : ressort amorti, en part du cadran (amortissement relatif d'environ 0,65).
        const float NeedleStiffness = 120f;
        const float NeedleDamping = 14f;
        const float MaxSubstep = 1f / 120f;
        // Rebond contre une butée : part de la vitesse rendue.
        const float StopBounce = 0.25f;

        // Frémissement moteur, en part du cadran : environ un degré gaz en grand et haut dans les tours.
        const float TrembleAmplitude = 0.0035f;
        const float TrembleFrequency = 22f;

        // Chiffres : lissés pour défiler, et changés seulement au-delà d'un écart qui évite qu'ils hésitent
        // entre deux valeurs quand la vitesse tombe pile entre les deux.
        const float DigitsSmoothTime = 0.1f;
        const float DigitsHysteresis = 0.6f;
        // Élan des chiffres : accélération, en part du cadran par seconde, où il commence et où il est complet.
        const float PunchFrom = 0.04f;
        const float PunchFull = 0.16f;
        const float PunchScale = 0.06f;
        const float PunchResponse = 10f;

        const float BackdropAlpha = 0.5f;

        static readonly string[] DigitStrings = new string[1000];

        MotorcycleController controller;
        UITheme theme;
        SpeedDial dial;
        TextMeshProUGUI speedText;
        TextMeshProUGUI unitText;

        int dialMax;
        float needle;
        float needleVelocity;
        float shownSpeed;
        float shownSpeedVelocity;
        int displayedSpeed;
        float punch;

        /// <param name="parent">Zone sûre du HUD.</param>
        /// <param name="top">Distance du haut de l'arc au haut de la zone sûre : celle des boutons VUE et PAUSE.</param>
        public void Build(Transform parent, UITheme uiTheme, MotorcycleController target, float top)
        {
            controller = target;
            theme = uiTheme;

            // Carré centré sur l'axe du cadran : le haut de l'arc tombe au ras des boutons du haut. Le bas du carré
            // reste vide, le cadran étant ouvert vers le bas.
            float extent = Radius + Padding;
            var root = UIFactory.CreateUIObject("SpeedReadout", parent);
            UIFactory.SetRect(root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-extent, -top + Padding - 2f * extent), new Vector2(extent, -top + Padding));

            // Voile sombre sans bord derrière le cadran : l'arc et les chiffres restent lisibles sur un ciel clair.
            var center = new Vector2(0.5f, 0.5f);
            float backdrop = Radius + 50f;
            UIFactory.AddGlow(root, "Backdrop", new Color(0f, 0f, 0f, BackdropAlpha), center, center,
                new Vector2(-backdrop, -backdrop), new Vector2(backdrop, backdrop));

            var dialRect = UIFactory.CreateUIObject("Dial", root);
            UIFactory.SetRect(dialRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            dial = dialRect.gameObject.AddComponent<SpeedDial>();
            dial.Setup(Radius, Thickness, Sweep);

            // Chiffres au cœur du cadran, un peu au-dessus du centre : avec l'unité dessous, le bloc tombe au milieu
            // de l'arc, qui monte plus haut qu'il ne descend. Centrés sur leur dessin en hauteur (Midline) plutôt
            // que sur la ligne de texte, et sur leur chasse en largeur : les chiffres de la police ont tous la même,
            // le nombre ne danse pas quand il change.
            speedText = StuntScoreHud.Outlined(UIFactory.AddText(root, "Speed", "0", UITheme.FontReadout, theme.Text, TextAnchor.MiddleCenter,
                center, center, new Vector2(-92f, -30f), new Vector2(92f, 62f), FontStyles.Bold), 0.2f);
            speedText.alignment = TextAlignmentOptions.Midline;

            unitText = StuntScoreHud.Outlined(UIFactory.AddText(root, "Unit", "KM/H", UITheme.FontLabel, theme.TextMuted, TextAnchor.MiddleCenter,
                center, center, new Vector2(-70f, -58f), new Vector2(70f, -26f), FontStyles.Bold), 0.14f);
            unitText.characterSpacing = 12f;

            Refresh(0f);
        }

        public void Tick()
        {
            if (controller == null || dial == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            // La fiche technique n'est appliquée qu'à l'éveil de la moto, qui peut passer après celui du HUD.
            if (dialMax <= 0 && !TryMeasureScale()) return;

            float speed = controller.SpeedKmh;
            UpdateNeedle(Mathf.Clamp01(speed / dialMax), dt);
            UpdateDigits(speed, dt);
            Refresh(Mathf.Clamp01(needle + Tremble()));
        }

        bool TryMeasureScale()
        {
            float topSpeed = controller.Stats.TopSpeedKmh;
            if (topSpeed <= 0f) return false;

            dialMax = Scale(topSpeed, out int intervals);
            dial.Intervals = intervals;
            return true;
        }

        static int Scale(float topSpeedKmh, out int intervals)
        {
            float wanted = Mathf.Max(40f, topSpeedKmh * ScaleHeadroom);
            int step = ScaleSteps[ScaleSteps.Length - 1];
            foreach (int candidate in ScaleSteps)
            {
                if (Mathf.CeilToInt(wanted / candidate) > MaxIntervals) continue;
                step = candidate;
                break;
            }
            intervals = Mathf.CeilToInt(wanted / step);
            return intervals * step;
        }

        /// <summary>
        /// Ressort amorti intégré par petits pas : il reste stable même quand une image tarde. Les butées arrêtent
        /// l'aiguille et la renvoient un peu, comme la goupille d'un vrai compteur.
        /// </summary>
        void UpdateNeedle(float target, float dt)
        {
            float remaining = Mathf.Min(dt, 0.1f);
            while (remaining > 0f)
            {
                float step = Mathf.Min(remaining, MaxSubstep);
                needleVelocity += (NeedleStiffness * (target - needle) - NeedleDamping * needleVelocity) * step;
                needle += needleVelocity * step;
                remaining -= step;
            }

            if (needle < 0f)
            {
                needle = 0f;
                if (needleVelocity < 0f) needleVelocity *= -StopBounce;
            }
            else if (needle > 1f)
            {
                needle = 1f;
                if (needleVelocity > 0f) needleVelocity *= -StopBounce;
            }
        }

        /// <summary>Frémissement de l'aiguille quand le moteur tire : il grandit avec les gaz et les tours.</summary>
        float Tremble()
        {
            if (controller.IsFallen) return 0f;

            float revs = Mathf.Clamp01(controller.EngineRpm / Mathf.Max(1f, controller.Stats.RedlineRpm));
            float amount = controller.Throttle * Mathf.Lerp(0.35f, 1f, revs);
            float noise = Mathf.PerlinNoise(Time.time * TrembleFrequency, 0.37f) * 2f - 1f;
            return TrembleAmplitude * amount * noise;
        }

        void UpdateDigits(float target, float dt)
        {
            float previous = shownSpeed;
            shownSpeed = Mathf.SmoothDamp(shownSpeed, target, ref shownSpeedVelocity, DigitsSmoothTime, Mathf.Infinity, dt);

            int rounded = Mathf.RoundToInt(shownSpeed);
            if (rounded != displayedSpeed && Mathf.Abs(shownSpeed - displayedSpeed) >= DigitsHysteresis)
            {
                displayedSpeed = rounded;
                speedText.text = Digits(rounded);
            }

            // L'élan se mesure en part du cadran : un scooter qui arrache aussi fort que le lui permet son moteur
            // en profite autant qu'une sportive.
            float acceleration = (shownSpeed - previous) / (dt * dialMax);
            float wanted = PunchScale * Mathf.Clamp01(Mathf.InverseLerp(PunchFrom, PunchFull, acceleration));
            punch = Mathf.Lerp(punch, wanted, 1f - Mathf.Exp(-PunchResponse * dt));
            speedText.rectTransform.localScale = Vector3.one * (1f + punch);
        }

        /// <summary>Pose l'aiguille et la teinte de l'unité, qui rougit avec l'arc en haut du cadran.</summary>
        void Refresh(float value)
        {
            dial.Value = value;

            float hot = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(SpeedDial.HotFrom, 1f, value));
            unitText.color = Color.Lerp(theme.TextMuted, UITheme.Brand, hot);
        }

        /// <summary>Chaînes des vitesses gardées une fois écrites : le texte change souvent, sans rien allouer.</summary>
        static string Digits(int value)
        {
            if (value < 0 || value >= DigitStrings.Length) return value.ToString();
            return DigitStrings[value] ??= value.ToString();
        }
    }
}
