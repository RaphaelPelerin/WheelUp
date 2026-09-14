using System;
using System.Globalization;
using UnityEngine;

namespace WheelingMoto.Core
{
    /// <summary>Commande de direction du HUD de conduite.</summary>
    public enum SteeringControl
    {
        Fleches,
        Joystick
    }

    /// <summary>Commandes de conduite que le joueur peut déplacer depuis les Paramètres.</summary>
    public enum DrivingControl
    {
        SteerLeft,
        SteerRight,
        Joystick,
        Throttle,
        Lift,
        FrontBrake,
        RearBrake
    }

    /// <summary>Réglages graphiques/audio/contrôles persistés via PlayerPrefs.</summary>
    public static class SettingsManager
    {
        const string KeySteering = "settings_steering_control";
        const string KeyMasterVolume = "settings_master_volume";
        const string KeyMusicVolume = "settings_music_volume";
        const string KeySfxVolume = "settings_sfx_volume";
        const string KeyQualityLevel = "settings_quality_level";
        const string KeyControlPositionPrefix = "settings_control_position_";

        /// <summary>Direction par flèches (par défaut) ou par joystick, qui revient au milieu au relâchement.</summary>
        public static SteeringControl Steering
        {
            get => (SteeringControl)PlayerPrefs.GetInt(KeySteering, (int)SteeringControl.Fleches);
            set => PlayerPrefs.SetInt(KeySteering, (int)value);
        }

        /// <summary>
        /// Avance à chaque changement de disposition des commandes : le HUD, qui la relit à la sortie de la
        /// pause, sait ainsi qu'il doit reposer ses boutons.
        /// </summary>
        public static int ControlLayoutRevision { get; private set; }

        /// <summary>
        /// Place choisie par le joueur pour une commande : le centre du bouton, en fraction de la zone sûre
        /// (0,0 en bas à gauche), pour qu'elle suive d'un écran à l'autre. Faux tant que la commande est à sa
        /// place par défaut.
        /// </summary>
        public static bool TryGetControlPosition(DrivingControl control, out Vector2 center)
        {
            center = default;
            string[] parts = PlayerPrefs.GetString(ControlPositionKey(control), "").Split(';');
            if (parts.Length != 2
                || !float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x)
                || !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y)
                || float.IsNaN(x) || float.IsNaN(y))
            {
                return false;
            }

            center = new Vector2(Mathf.Clamp01(x), Mathf.Clamp01(y));
            return true;
        }

        /// <summary>Enregistre la place d'une commande ; null la rend à sa place par défaut.</summary>
        public static void SetControlPosition(DrivingControl control, Vector2? center)
        {
            string key = ControlPositionKey(control);
            if (center.HasValue)
            {
                // Culture invariante : une virgule décimale se confondrait avec le séparateur sur un téléphone en français.
                PlayerPrefs.SetString(key, string.Format(CultureInfo.InvariantCulture, "{0:0.#####};{1:0.#####}", center.Value.x, center.Value.y));
            }
            else
            {
                PlayerPrefs.DeleteKey(key);
            }
            ControlLayoutRevision++;
        }

        /// <summary>Rend toutes les commandes à leur place par défaut, celles des deux modes de direction comprises.</summary>
        public static void ResetControlPositions()
        {
            foreach (DrivingControl control in Enum.GetValues(typeof(DrivingControl)))
            {
                SetControlPosition(control, null);
            }
        }

        static string ControlPositionKey(DrivingControl control) => KeyControlPositionPrefix + control;

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
