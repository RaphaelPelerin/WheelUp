using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WheelingMoto.Core;

namespace WheelingMoto.UI
{
    /// <summary>Onglet Paramètres : graphismes, audio et rappel des contrôles tactiles.</summary>
    public class SettingsMenu
    {
        /// <summary>
        /// Notice des contrôles, ouverte par la pastille « i » de la colonne de droite. Elle est
        /// aérée d'une ligne vide entre les entrées : en plein écran et en gros corps, une liste
        /// serrée se lit moins bien qu'une liste espacée.
        /// </summary>
        const string ControlsHelp =
            "◀ / ▶ ou joystick\ndiriger la moto. Lâché, le joystick revient au milieu.\n\n" +
            "GAZ\naccélérer.\n\n" +
            "LEVER\naccélérer et lever la roue avant.\n\n" +
            "FREIN\nfreiner. Appuyé fort en roulant vite, il lève l'arrière et met la moto sur la " +
            "roue avant. À l'arrêt, il fait reculer.\n\n" +
            "GLISSER L'ÉCRAN\ntourner la caméra. Le bouton VUE change de point de vue.";

        public GameObject Root { get; private set; }

        UITheme theme;
        string[] qualityNames;
        int qualityIndex;
        float masterVol;
        float musicVol;
        float sfxVol;

        StepperWidget qualityStepper;
        StepperWidget masterStepper;
        StepperWidget musicStepper;
        StepperWidget sfxStepper;
        Button arrowsButton;
        Button joystickButton;

        TextMeshProUGUI adsStateText;

        public void Build(Transform parent, UITheme t)
        {
            theme = t;
            qualityNames = QualitySettings.names;
            qualityIndex = Mathf.Clamp(SettingsManager.QualityLevel, 0, Mathf.Max(0, qualityNames.Length - 1));
            masterVol = SettingsManager.MasterVolume;
            musicVol = SettingsManager.MusicVolume;
            sfxVol = SettingsManager.SfxVolume;

            var panel = UIFactory.AddPanel(parent, "SettingsPanel", Color.clear, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Root = panel.gameObject;

            UIFactory.AddText(Root.transform, "Title", "PARAMÈTRES & BOUTIQUE", UITheme.FontTitle, theme.Text, TextAnchor.MiddleLeft,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(30, -56), new Vector2(-30, -6));

            var left = UIFactory.AddPanel(Root.transform, "Graphics", theme.Panel,
                new Vector2(0, 0), new Vector2(0.5f, 1), new Vector2(20, 10), new Vector2(-10, -60));

            UIFactory.AddText(left.transform, "SectionTitle", "Graphismes & Audio", UITheme.FontHeading, theme.Text, TextAnchor.MiddleLeft,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(22, -60), new Vector2(-22, -14));

            UIFactory.AddText(left.transform, "QualityLabel", "Qualité graphique", UITheme.FontLabel, theme.TextMuted, TextAnchor.MiddleLeft,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(22, -102), new Vector2(-22, -70));
            qualityStepper = UIFactory.AddStepper(left.transform, "QualityStepper", theme,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(22, -156), new Vector2(-22, -108), PrevQuality, NextQuality);

            UIFactory.AddText(left.transform, "MasterLabel", "Volume général", UITheme.FontLabel, theme.TextMuted, TextAnchor.MiddleLeft,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(22, -202), new Vector2(-22, -170));
            masterStepper = UIFactory.AddStepper(left.transform, "MasterStepper", theme,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(22, -256), new Vector2(-22, -208),
                () => AdjustMaster(-0.1f), () => AdjustMaster(0.1f));

            UIFactory.AddText(left.transform, "MusicLabel", "Musique", UITheme.FontLabel, theme.TextMuted, TextAnchor.MiddleLeft,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(22, -302), new Vector2(-22, -270));
            musicStepper = UIFactory.AddStepper(left.transform, "MusicStepper", theme,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(22, -356), new Vector2(-22, -308),
                () => AdjustMusic(-0.1f), () => AdjustMusic(0.1f));

            UIFactory.AddText(left.transform, "SfxLabel", "Effets sonores", UITheme.FontLabel, theme.TextMuted, TextAnchor.MiddleLeft,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(22, -402), new Vector2(-22, -370));
            sfxStepper = UIFactory.AddStepper(left.transform, "SfxStepper", theme,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(22, -456), new Vector2(-22, -408),
                () => AdjustSfx(-0.1f), () => AdjustSfx(0.1f));

            var right = UIFactory.AddPanel(Root.transform, "Controls", theme.Panel,
                new Vector2(0.5f, 0), new Vector2(1, 1), new Vector2(10, 10), new Vector2(-20, -60));

            UIFactory.AddText(right.transform, "SectionTitle", "Contrôles", UITheme.FontHeading, theme.Text, TextAnchor.MiddleLeft,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(22, -60), new Vector2(-22, -14));

            // Les six lignes de la notice tenaient en corps 16 dans la colonne. Elles s'ouvrent
            // maintenant en plein écran, où elles se lisent au même corps que le reste du jeu.
            UIFactory.AddInfoButton(right.transform, "ControlsInfo", theme, "Contrôles tactiles", ControlsHelp,
                new Vector2(1, 1), new Vector2(1, 1), new Vector2(-78, -70), new Vector2(-22, -14));

            UIFactory.AddText(right.transform, "SteeringLabel", "Direction", UITheme.FontLabel, theme.TextMuted, TextAnchor.MiddleLeft,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(22, -102), new Vector2(-22, -70));
            arrowsButton = UIFactory.AddButton(right.transform, "ArrowsButton", "FLÈCHES", theme.PanelAlt, theme.Text, UITheme.FontLabel,
                new Vector2(0, 1), new Vector2(0.5f, 1), new Vector2(22, -164), new Vector2(-6, -108),
                () => SelectSteering(SteeringControl.Fleches));
            joystickButton = UIFactory.AddButton(right.transform, "JoystickButton", "JOYSTICK", theme.PanelAlt, theme.Text, UITheme.FontLabel,
                new Vector2(0.5f, 1), new Vector2(1, 1), new Vector2(6, -164), new Vector2(-22, -108),
                () => SelectSteering(SteeringControl.Joystick));

            // Les achats ont quitté cet écran pour l'onglet Boutique : ils y côtoient les lots de
            // pièces, sous la même bannière que les coffres.
            adsStateText = UIFactory.AddText(right.transform, "AdsState", "", UITheme.FontLabel, theme.TextMuted, TextAnchor.UpperLeft,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(22, -300), new Vector2(-22, -196));

            UpdateQualityLabel();
            UpdateMasterLabel();
            UpdateMusicLabel();
            UpdateSfxLabel();
            RefreshSteeringButtons();
            RefreshAdsState();
        }

        void PrevQuality()
        {
            qualityIndex = Mathf.Max(0, qualityIndex - 1);
            SettingsManager.QualityLevel = qualityIndex;
            UpdateQualityLabel();
        }

        void NextQuality()
        {
            qualityIndex = Mathf.Min(qualityNames.Length - 1, qualityIndex + 1);
            SettingsManager.QualityLevel = qualityIndex;
            UpdateQualityLabel();
        }

        void UpdateQualityLabel()
        {
            qualityStepper.Label.text = qualityNames.Length > 0 ? qualityNames[qualityIndex] : "-";
        }

        void AdjustMaster(float delta)
        {
            masterVol = Mathf.Clamp01(masterVol + delta);
            SettingsManager.MasterVolume = masterVol;
            UpdateMasterLabel();
        }

        void UpdateMasterLabel() => masterStepper.Label.text = $"{Mathf.RoundToInt(masterVol * 100)}%";

        void AdjustMusic(float delta)
        {
            musicVol = Mathf.Clamp01(musicVol + delta);
            SettingsManager.MusicVolume = musicVol;
            UpdateMusicLabel();
        }

        void UpdateMusicLabel() => musicStepper.Label.text = $"{Mathf.RoundToInt(musicVol * 100)}%";

        void AdjustSfx(float delta)
        {
            sfxVol = Mathf.Clamp01(sfxVol + delta);
            SettingsManager.SfxVolume = sfxVol;
            UpdateSfxLabel();
        }

        void UpdateSfxLabel() => sfxStepper.Label.text = $"{Mathf.RoundToInt(sfxVol * 100)}%";

        void SelectSteering(SteeringControl control)
        {
            SettingsManager.Steering = control;
            RefreshSteeringButtons();
        }

        void RefreshSteeringButtons()
        {
            bool joystick = SettingsManager.Steering == SteeringControl.Joystick;
            UIFactory.SetButtonColor(arrowsButton, joystick ? theme.PanelAlt : theme.Accent);
            UIFactory.SetButtonColor(joystickButton, joystick ? theme.Accent : theme.PanelAlt);
        }

        void RefreshAdsState()
        {
            adsStateText.text = MonetizationManager.AdsRemoved
                ? "Publicités désactivées."
                : "Des publicités s'affichent entre les parties.\nLe retrait s'achète dans l'onglet Boutique.";
        }
    }
}
