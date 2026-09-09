using UnityEngine;

namespace WheelingMoto.Core
{
    /// <summary>Réglages graphiques/audio persistés via PlayerPrefs.</summary>
    public static class SettingsManager
    {
        const string KeyMasterVolume = "settings_master_volume";
        const string KeyMusicVolume = "settings_music_volume";
        const string KeySfxVolume = "settings_sfx_volume";
        const string KeyQualityLevel = "settings_quality_level";

        public static float MasterVolume
        {
            get => PlayerPrefs.GetFloat(KeyMasterVolume, 1f);
            set
            {
                PlayerPrefs.SetFloat(KeyMasterVolume, value);
                AudioListener.volume = value;
            }
        }

        public static float MusicVolume
        {
            get => PlayerPrefs.GetFloat(KeyMusicVolume, 0.8f);
            set => PlayerPrefs.SetFloat(KeyMusicVolume, value);
        }

        public static float SfxVolume
        {
            get => PlayerPrefs.GetFloat(KeySfxVolume, 0.8f);
            set => PlayerPrefs.SetFloat(KeySfxVolume, value);
        }

        public static int QualityLevel
        {
            get => PlayerPrefs.GetInt(KeyQualityLevel, QualitySettings.GetQualityLevel());
            set
            {
                PlayerPrefs.SetInt(KeyQualityLevel, value);
                QualitySettings.SetQualityLevel(value, true);
            }
        }

        /// <summary>À appeler au lancement pour ré-appliquer les réglages sauvegardés.</summary>
        public static void Apply()
        {
            AudioListener.volume = MasterVolume;
            int savedQuality = PlayerPrefs.GetInt(KeyQualityLevel, -1);
            if (savedQuality >= 0 && savedQuality < QualitySettings.names.Length)
            {
                QualitySettings.SetQualityLevel(savedQuality, true);
            }
        }
    }
}
