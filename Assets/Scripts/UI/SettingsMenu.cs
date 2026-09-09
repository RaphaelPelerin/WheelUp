using UnityEngine;
using UnityEngine.UI;
using WheelingMoto.Core;

namespace WheelingMoto.UI
{
    /// <summary>Onglet Paramètres & Boutique : graphismes, audio, contrôles tactiles, retrait des pubs (5€).</summary>
    public class SettingsMenu
    {
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

        Button removeAdsButton;
        Text removeAdsStateText;

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

            UIFactory.AddText(Root.transform, "Title", "PARAMÈTRES & BOUTIQUE", 30, theme.Text, TextAnchor.MiddleLeft,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(30, -50), new Vector2(-30, -6));

            var left = UIFactory.AddPanel(Root.transform, "Graphics", theme.Panel,
                new Vector2(0, 0), new Vector2(0.5f, 1), new Vector2(20, 10), new Vector2(-10, -60));

            UIFactory.AddText(left.transform, "SectionTitle", "Graphismes & Audio", 22, theme.Text, TextAnchor.MiddleLeft,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(22, -56), new Vector2(-22, -16));

            UIFactory.AddText(left.transform, "QualityLabel", "Qualité graphique", 17, theme.TextMuted, TextAnchor.MiddleLeft,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(22, -100), new Vector2(-22, -76));
            qualityStepper = UIFactory.AddStepper(left.transform, "QualityStepper", theme,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(22, -156), new Vector2(-22, -108), PrevQuality, NextQuality);

            UIFactory.AddText(left.transform, "MasterLabel", "Volume général", 17, theme.TextMuted, TextAnchor.MiddleLeft,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(22, -200), new Vector2(-22, -176));
            masterStepper = UIFactory.AddStepper(left.transform, "MasterStepper", theme,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(22, -256), new Vector2(-22, -208),
                () => AdjustMaster(-0.1f), () => AdjustMaster(0.1f));

            UIFactory.AddText(left.transform, "MusicLabel", "Musique", 17, theme.TextMuted, TextAnchor.MiddleLeft,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(22, -300), new Vector2(-22, -276));
            musicStepper = UIFactory.AddStepper(left.transform, "MusicStepper", theme,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(22, -356), new Vector2(-22, -308),
                () => AdjustMusic(-0.1f), () => AdjustMusic(0.1f));

            UIFactory.AddText(left.transform, "SfxLabel", "Effets sonores", 17, theme.TextMuted, TextAnchor.MiddleLeft,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(22, -400), new Vector2(-22, -376));
            sfxStepper = UIFactory.AddStepper(left.transform, "SfxStepper", theme,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(22, -456), new Vector2(-22, -408),
                () => AdjustSfx(-0.1f), () => AdjustSfx(0.1f));

            var right = UIFactory.AddPanel(Root.transform, "Shop", theme.Panel,
                new Vector2(0.5f, 0), new Vector2(1, 1), new Vector2(10, 10), new Vector2(-20, -60));

            UIFactory.AddText(right.transform, "SectionTitle", "Contrôles & Boutique", 22, theme.Text, TextAnchor.MiddleLeft,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(22, -56), new Vector2(-22, -16));

            UIFactory.AddText(right.transform, "ControlsInfo",
                "Contrôles tactiles :\n• ◀ / ▶ (bas gauche) : diriger\n• GAZ / FREIN (bas droite) : accélérer / freiner\n• CABRER (bas droite) : lever la roue avant",
                16, theme.TextMuted, TextAnchor.UpperLeft,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(22, -220), new Vector2(-22, -76));

            removeAdsStateText = UIFactory.AddText(right.transform, "AdsState", "", 15, theme.TextMuted, TextAnchor.UpperLeft,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(22, -256), new Vector2(-22, -228));

            removeAdsButton = UIFactory.AddButton(right.transform, "RemoveAdsButton",
                $"Retirer les publicités — {MonetizationManager.RemoveAdsPrice}", theme.Accent, Color.white, 18,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(22, -324), new Vector2(-22, -266), OnRemoveAdsPressed);

            UpdateQualityLabel();
            UpdateMasterLabel();
            UpdateMusicLabel();
            UpdateSfxLabel();
            RefreshShopState();
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

        void RefreshShopState()
        {
            bool removed = MonetizationManager.AdsRemoved;
            removeAdsStateText.text = removed ? "Publicités désactivées." : "Des publicités s'affichent entre les parties.";
            removeAdsButton.gameObject.SetActive(!removed);
        }

        void OnRemoveAdsPressed()
        {
            removeAdsButton.interactable = false;
            MonetizationManager.PurchaseRemoveAds(success => RefreshShopState());
        }
    }
}
