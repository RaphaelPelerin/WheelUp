using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using WheelingMoto.Core;
using WheelingMoto.Gameplay;

namespace WheelingMoto.UI
{
    /// <summary>
    /// HUD de la scène Métropole : commandes tactiles (direction par flèches ou par joystick selon les
    /// Paramètres, GAZ / LEVER / FREIN), vitesse, jauge d'angle (wheeling ou roue avant), bandeau « Chute ! »,
    /// bouton de changement de vue et zone de glissement pour tourner la caméra.
    /// Tout ce qui se touche ou se lit est cadré dans la zone sûre de l'écran (encoche, coins arrondis,
    /// barre d'accueil), avec une marge : rien n'est rogné sur les téléphones à écran bord à bord.
    /// </summary>
    public class MetropoleHUD : MonoBehaviour
    {
        const float FallOverlayHold = 0.9f;
        const float FallOverlayFade = 0.5f;

        // Gabarit des commandes, en unités du canvas (référence 1920 x 1080), mesuré depuis la zone sûre.
        const float Margin = 24f;
        const float Gap = 20f;
        const float SteerButtonSize = 250f;
        const float JoystickSize = 330f;
        const float JoystickKnobSize = 140f;
        const float PedalWidth = 250f;
        const float ThrottleHeight = 310f;
        const float BrakeHeight = 210f;
        const float LiftHeight = 180f;
        const int ControlFontSize = 34;

        const string WheelieFallHint = "Dose LEVER et FREIN pour rester dans la zone verte";
        const string StoppieFallHint = "Roue avant : relâche FREIN avant la zone rouge, rien ne te rattrape au-delà";

        // Boutons translucides : la route reste visible sous les pouces. À l'appui, le fond passe à
        // l'orange du thème, l'assombrissement par défaut du Button ne se verrait pas sur ce fond.
        static readonly Color ControlColor = new Color(0.17f, 0.18f, 0.22f, 0.3f);
        static readonly Color ControlPressedColor = new Color(0.96f, 0.36f, 0.13f, 0.5f);
        static readonly Color ControlTextColor = new Color(1f, 1f, 1f, 0.85f);
        static readonly Color JoystickRailColor = new Color(1f, 1f, 1f, 0.12f);
        static readonly Color KnobColor = new Color(1f, 1f, 1f, 0.45f);

        MotorcycleController controller;
        MotoCameraRig cameraRig;
        UITheme theme;
        TextMeshProUGUI speedText;
        TextMeshProUGUI viewLabel;
        CameraView displayedView;
        WheelieGauge gauge;
        CanvasGroup fallOverlay;
        TextMeshProUGUI fallHint;
        float fallOverlayTimer;
        bool leftHeld;
        bool rightHeld;

        void Awake()
        {
            controller = FindAnyObjectByType<MotorcycleController>();
            cameraRig = FindAnyObjectByType<MotoCameraRig>();
            theme = new UITheme();
            Build();

            if (controller != null)
            {
                controller.Fell += OnFell;
            }
        }

        void OnDestroy()
        {
            if (controller != null)
            {
                controller.Fell -= OnFell;
            }
        }

        void Update()
        {
            if (controller != null)
            {
                speedText.text = $"{Mathf.RoundToInt(controller.SpeedKmh)} km/h";
                gauge?.Tick();
            }

            // La vue peut aussi changer au clavier (C / V) : le libellé suit.
            if (cameraRig != null && viewLabel != null && cameraRig.CurrentView != displayedView)
            {
                RefreshViewLabel();
            }

            if (fallOverlayTimer > 0f)
            {
                fallOverlayTimer -= Time.deltaTime;
                fallOverlay.alpha = Mathf.Clamp01(fallOverlayTimer / FallOverlayFade);
            }
        }

        void OnFell()
        {
            fallHint.text = controller != null && controller.LastFallForward ? StoppieFallHint : WheelieFallHint;
            fallOverlayTimer = FallOverlayHold + FallOverlayFade;
            fallOverlay.alpha = 1f;
        }

        void Build()
        {
            var canvas = UIFactory.CreateRootCanvas("MetropoleHUDCanvas");
            var root = canvas.transform;

            // Créée en premier, donc dessinée derrière : les boutons restent prioritaires.
            // Plein écran : glisser depuis les bords tourne aussi la caméra.
            BuildCameraDragZone(root);
            BuildFallOverlay(root);

            var safeArea = UIFactory.CreateUIObject("SafeArea", root);
            safeArea.gameObject.AddComponent<SafeAreaFitter>();

            speedText = UIFactory.AddText(safeArea, "SpeedText", "0 km/h", 26, theme.Text, TextAnchor.MiddleLeft,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(Margin, -60), new Vector2(Margin + 230, -20));

            UIFactory.AddButton(safeArea, "BackButton", "◀ MENU", ControlColor, ControlTextColor, 16,
                new Vector2(1, 1), new Vector2(1, 1), new Vector2(-Margin - 160, -60), new Vector2(-Margin, -20),
                () => SceneLoader.LoadMainMenu());

            if (cameraRig != null)
            {
                var viewButton = UIFactory.AddButton(safeArea, "ViewButton", "VUE", ControlColor, ControlTextColor, 16,
                    new Vector2(1, 1), new Vector2(1, 1), new Vector2(-Margin - 310, -60), new Vector2(-Margin - 170, -20),
                    () => cameraRig.ToggleView());
                viewLabel = viewButton.GetComponentInChildren<TextMeshProUGUI>();
                RefreshViewLabel();
            }

            if (controller != null)
            {
                gauge = new WheelieGauge();
                gauge.Build(safeArea, theme, controller);
            }

            if (SettingsManager.Steering == SteeringControl.Joystick)
            {
                BuildJoystick(safeArea);
            }
            else
            {
                BuildSteerArrows(safeArea);
            }

            BuildPedals(safeArea);
        }

        void BuildSteerArrows(Transform parent)
        {
            AddHold(parent, "SteerLeft", "◀", Vector2.zero, Vector2.zero,
                new Vector2(Margin, Margin), new Vector2(Margin + SteerButtonSize, Margin + SteerButtonSize),
                () => { leftHeld = true; UpdateSteer(); }, () => { leftHeld = false; UpdateSteer(); });

            float x = Margin + SteerButtonSize + Gap;
            AddHold(parent, "SteerRight", "▶", Vector2.zero, Vector2.zero,
                new Vector2(x, Margin), new Vector2(x + SteerButtonSize, Margin + SteerButtonSize),
                () => { rightHeld = true; UpdateSteer(); }, () => { rightHeld = false; UpdateSteer(); });
        }

        /// <summary>Joystick horizontal : la direction suit l'écart du doigt et revient au milieu au relâchement.</summary>
        void BuildJoystick(Transform parent)
        {
            var area = UIFactory.CreateUIObject("SteerJoystick", parent);
            UIFactory.SetRect(area, Vector2.zero, Vector2.zero,
                new Vector2(Margin, Margin), new Vector2(Margin + JoystickSize, Margin + JoystickSize));
            var background = area.gameObject.AddComponent<Image>();
            background.sprite = SteerJoystick.CircleSprite;
            background.color = ControlColor;
            background.raycastTarget = true;

            // Rail horizontal : le joystick ne sert qu'à diriger.
            var rail = UIFactory.AddPanel(area, "Rail", JoystickRailColor, new Vector2(0.12f, 0.5f), new Vector2(0.88f, 0.5f),
                new Vector2(0f, -6f), new Vector2(0f, 6f), rounded: true);
            rail.raycastTarget = false;

            float half = JoystickKnobSize * 0.5f;
            var knob = UIFactory.CreateUIObject("Knob", area);
            UIFactory.SetRect(knob, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-half, -half), new Vector2(half, half));
            var knobImage = knob.gameObject.AddComponent<Image>();
            knobImage.sprite = SteerJoystick.CircleSprite;
            knobImage.color = KnobColor;
            knobImage.raycastTarget = false;

            area.gameObject.AddComponent<SteerJoystick>().Init(knob,
                value => controller?.SetSteer(value),
                held => knobImage.color = held ? ControlPressedColor : KnobColor);
        }

        /// <summary>GAZ dans le coin, FREIN à sa gauche, LEVER au-dessus de FREIN.</summary>
        void BuildPedals(Transform parent)
        {
            Vector2 corner = new Vector2(1f, 0f);

            float throttleLeft = -Margin - PedalWidth;
            AddHold(parent, "ThrottleButton", "GAZ", corner, corner,
                new Vector2(throttleLeft, Margin), new Vector2(-Margin, Margin + ThrottleHeight),
                () => controller?.SetThrottle(true), () => controller?.SetThrottle(false));

            float brakeLeft = throttleLeft - Gap - PedalWidth;
            AddHold(parent, "BrakeButton", "FREIN", corner, corner,
                new Vector2(brakeLeft, Margin), new Vector2(brakeLeft + PedalWidth, Margin + BrakeHeight),
                () => controller?.SetBrake(true), () => controller?.SetBrake(false));

            float liftBottom = Margin + BrakeHeight + Gap;
            AddHold(parent, "LiftButton", "LEVER", corner, corner,
                new Vector2(brakeLeft, liftBottom), new Vector2(brakeLeft + PedalWidth, liftBottom + LiftHeight),
                () => controller?.SetLift(true), () => controller?.SetLift(false));
        }

        void BuildCameraDragZone(Transform root)
        {
            if (cameraRig == null) return;

            var zone = UIFactory.AddPanel(root, "CameraDragZone", new Color(0f, 0f, 0f, 0f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            zone.raycastTarget = true;
            // Transparente mais capte les glissements ; pas de coût de rendu plein écran sur mobile.
            zone.canvasRenderer.cullTransparentMesh = true;
            zone.gameObject.AddComponent<CameraDragZone>().rig = cameraRig;
        }

        /// <summary>Bandeau d'échec : ne capte aucun toucher, le joueur peut repartir pendant qu'il s'estompe.</summary>
        void BuildFallOverlay(Transform root)
        {
            var band = UIFactory.AddPanel(root, "FallOverlay", new Color(0.55f, 0.04f, 0.04f, 0.6f),
                new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, -75f), new Vector2(0f, 75f));
            band.raycastTarget = false;

            fallOverlay = band.gameObject.AddComponent<CanvasGroup>();
            fallOverlay.interactable = false;
            fallOverlay.blocksRaycasts = false;
            fallOverlay.alpha = 0f;

            UIFactory.AddText(band.transform, "FallTitle", "CHUTE !", 56, Color.white, TextAnchor.MiddleCenter,
                new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, -10f), new Vector2(0f, 60f));
            fallHint = UIFactory.AddText(band.transform, "FallHint", WheelieFallHint, 18, theme.TextMuted, TextAnchor.MiddleCenter,
                new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, -55f), new Vector2(0f, -15f));
        }

        void RefreshViewLabel()
        {
            displayedView = cameraRig.CurrentView;
            viewLabel.text = displayedView == CameraView.Exterior ? "VUE : EXT." : "VUE : 1re P.";
        }

        void UpdateSteer()
        {
            float value = (rightHeld ? 1f : 0f) - (leftHeld ? 1f : 0f);
            controller?.SetSteer(value);
        }

        void AddHold(Transform parent, string name, string label, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, UnityAction onPressed, UnityAction onReleased)
        {
            var btn = UIFactory.AddButton(parent, name, label, ControlColor, ControlTextColor, ControlFontSize, anchorMin, anchorMax, offsetMin, offsetMax, null);
            btn.transition = Selectable.Transition.None;
            var background = btn.GetComponent<Image>();

            var hold = btn.gameObject.AddComponent<HoldButton>();
            hold.OnPressed.AddListener(() => background.color = ControlPressedColor);
            hold.OnPressed.AddListener(onPressed);
            hold.OnReleased.AddListener(() => background.color = ControlColor);
            hold.OnReleased.AddListener(onReleased);
        }
    }
}
