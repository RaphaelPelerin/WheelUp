using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using WheelingMoto.Core;
using WheelingMoto.Gameplay;

namespace WheelingMoto.UI
{
    /// <summary>Commandes tactiles de la scène Métropole (diriger / accélérer / freiner / cabrer) et affichage vitesse/chute.</summary>
    public class MetropoleHUD : MonoBehaviour
    {
        MotorcycleController controller;
        UITheme theme;
        TextMeshProUGUI speedText;
        TextMeshProUGUI fallText;
        bool leftHeld;
        bool rightHeld;

        void Awake()
        {
            controller = FindFirstObjectByType<MotorcycleController>();
            theme = new UITheme();
            Build();
        }

        void Update()
        {
            if (controller == null) return;
            speedText.text = $"{Mathf.RoundToInt(controller.SpeedKmh)} km/h";
            fallText.gameObject.SetActive(controller.IsFallen);
        }

        void Build()
        {
            var canvas = UIFactory.CreateRootCanvas("MetropoleHUDCanvas");
            var root = canvas.transform;

            speedText = UIFactory.AddText(root, "SpeedText", "0 km/h", 26, theme.Text, TextAnchor.MiddleLeft,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -60), new Vector2(260, -20));

            fallText = UIFactory.AddText(root, "FallText", "CHUTE !", 44, theme.Accent, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-220, -35), new Vector2(220, 35));
            fallText.gameObject.SetActive(false);

            UIFactory.AddButton(root, "BackButton", "◀ MENU", theme.PanelAlt, theme.Text, 16,
                new Vector2(1, 1), new Vector2(1, 1), new Vector2(-190, -60), new Vector2(-30, -20),
                () => SceneLoader.LoadMainMenu());

            AddHold(root, "SteerLeft", "◀",
                new Vector2(0, 0), new Vector2(0, 0), new Vector2(30, 30), new Vector2(150, 150),
                () => { leftHeld = true; UpdateSteer(); }, () => { leftHeld = false; UpdateSteer(); });

            AddHold(root, "SteerRight", "▶",
                new Vector2(0, 0), new Vector2(0, 0), new Vector2(170, 30), new Vector2(290, 150),
                () => { rightHeld = true; UpdateSteer(); }, () => { rightHeld = false; UpdateSteer(); });

            AddHold(root, "BrakeButton", "FREIN",
                new Vector2(1, 0), new Vector2(1, 0), new Vector2(-320, 30), new Vector2(-180, 130),
                () => controller?.SetBrake(true), () => controller?.SetBrake(false));

            AddHold(root, "ThrottleButton", "GAZ",
                new Vector2(1, 0), new Vector2(1, 0), new Vector2(-160, 30), new Vector2(-20, 170),
                () => controller?.SetThrottle(true), () => controller?.SetThrottle(false));

            AddHold(root, "WheelieButton", "CABRER",
                new Vector2(1, 0), new Vector2(1, 0), new Vector2(-320, 150), new Vector2(-180, 230),
                () => controller?.SetWheelie(true), () => controller?.SetWheelie(false));
        }

        void UpdateSteer()
        {
            float value = (rightHeld ? 1f : 0f) - (leftHeld ? 1f : 0f);
            controller?.SetSteer(value);
        }

        void AddHold(Transform parent, string name, string label, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, UnityAction onPressed, UnityAction onReleased)
        {
            var btn = UIFactory.AddButton(parent, name, label, theme.PanelAlt, theme.Text, 20, anchorMin, anchorMax, offsetMin, offsetMax, null);
            var hold = btn.gameObject.AddComponent<HoldButton>();
            hold.OnPressed.AddListener(onPressed);
            hold.OnReleased.AddListener(onReleased);
        }
    }
}
