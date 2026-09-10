using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using WheelingMoto.Core;
using WheelingMoto.Gameplay;

namespace WheelingMoto.UI
{
    /// <summary>
    /// HUD de la scène Métropole : commandes tactiles (diriger / GAZ / LEVER / FREIN), vitesse,
    /// jauge d'angle de wheeling, bandeau « Chute ! », bouton de changement de vue
    /// et zone de glissement pour tourner la caméra.
    /// </summary>
    public class MetropoleHUD : MonoBehaviour
    {
        const float FallOverlayHold = 0.9f;
        const float FallOverlayFade = 0.5f;

        MotorcycleController controller;
        MotoCameraRig cameraRig;
        UITheme theme;
        Text speedText;
        Text viewLabel;
        CameraView displayedView;
        WheelieGauge gauge;
        CanvasGroup fallOverlay;
        float fallOverlayTimer;
        bool leftHeld;
        bool rightHeld;

        void Awake()
        {
            controller = FindFirstObjectByType<MotorcycleController>();
            cameraRig = FindFirstObjectByType<MotoCameraRig>();
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
            fallOverlayTimer = FallOverlayHold + FallOverlayFade;
            fallOverlay.alpha = 1f;
        }

        void Build()
        {
            var canvas = UIFactory.CreateRootCanvas("MetropoleHUDCanvas");
            var root = canvas.transform;

            // Créée en premier, donc dessinée derrière : les boutons restent prioritaires.
            BuildCameraDragZone(root);

            speedText = UIFactory.AddText(root, "SpeedText", "0 km/h", 26, theme.Text, TextAnchor.MiddleLeft,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -60), new Vector2(260, -20));

            BuildFallOverlay(root);

            UIFactory.AddButton(root, "BackButton", "◀ MENU", theme.PanelAlt, theme.Text, 16,
                new Vector2(1, 1), new Vector2(1, 1), new Vector2(-190, -60), new Vector2(-30, -20),
                () => SceneLoader.LoadMainMenu());

            if (cameraRig != null)
            {
                var viewButton = UIFactory.AddButton(root, "ViewButton", "VUE", theme.PanelAlt, theme.Text, 16,
                    new Vector2(1, 1), new Vector2(1, 1), new Vector2(-340, -60), new Vector2(-200, -20),
                    () => cameraRig.ToggleView());
                viewLabel = viewButton.GetComponentInChildren<Text>();
                RefreshViewLabel();
            }

            if (controller != null)
            {
                gauge = new WheelieGauge();
                gauge.Build(root, theme, controller);
            }

            AddHold(root, "SteerLeft", "◀",
                new Vector2(0, 0), new Vector2(0, 0), new Vector2(30, 30), new Vector2(230, 230),
                () => { leftHeld = true; UpdateSteer(); }, () => { leftHeld = false; UpdateSteer(); });

            AddHold(root, "SteerRight", "▶",
                new Vector2(0, 0), new Vector2(0, 0), new Vector2(250, 30), new Vector2(450, 230),
                () => { rightHeld = true; UpdateSteer(); }, () => { rightHeld = false; UpdateSteer(); });

            AddHold(root, "BrakeButton", "FREIN",
                new Vector2(1, 0), new Vector2(1, 0), new Vector2(-450, 30), new Vector2(-250, 200),
                () => controller?.SetBrake(true), () => controller?.SetBrake(false));

            AddHold(root, "ThrottleButton", "GAZ",
                new Vector2(1, 0), new Vector2(1, 0), new Vector2(-230, 30), new Vector2(-30, 280),
                () => controller?.SetThrottle(true), () => controller?.SetThrottle(false));

            AddHold(root, "LiftButton", "LEVER",
                new Vector2(1, 0), new Vector2(1, 0), new Vector2(-450, 220), new Vector2(-250, 370),
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
            UIFactory.AddText(band.transform, "FallHint", "Dose LEVER et FREIN pour rester dans la zone verte", 18, theme.TextMuted, TextAnchor.MiddleCenter,
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
            var btn = UIFactory.AddButton(parent, name, label, theme.PanelAlt, theme.Text, 30, anchorMin, anchorMax, offsetMin, offsetMax, null);
            var hold = btn.gameObject.AddComponent<HoldButton>();
            hold.OnPressed.AddListener(onPressed);
            hold.OnReleased.AddListener(onReleased);
        }
    }
}
