using System;
using System.Collections.Generic;
using UnityEngine;
using WheelingMoto.Data;

namespace WheelingMoto.Core
{
    /// <summary>
    /// Compteurs à vie du joueur : tout ce qu'il a parcouru, tenu, acheté depuis sa première partie.
    /// Là où <see cref="MissionManager"/> remet ses compteurs à zéro chaque jour, ceux-ci ne
    /// redescendent jamais — ce sont eux qui portent les succès.
    ///
    /// Les deux systèmes sont nourris par la même source (<see cref="MissionTracker"/>) et ne se
    /// connaissent pas : une mesure rapportée une fois avance la mission du jour et le succès qui la
    /// suit, sans que le gameplay ait à la rapporter deux fois.
    /// </summary>
    public static class PlayerStats
    {
        const string SaveKey = "player_stats";

        /// <summary>Émis après chaque mise à jour : les succès s'y accrochent pour se réévaluer.</summary>
        public static event Action Changed;

        static Dictionary<MissionMetric, float> values;

        public static float Get(MissionMetric metric)
        {
            EnsureLoaded();
            return values.TryGetValue(metric, out float value) ? value : 0f;
        }

        /// <summary>Ajoute à un compteur cumulatif (distance, durée, nombre).</summary>
        public static void Add(MissionMetric metric, float amount)
        {
            if (amount <= 0f) return;

            EnsureLoaded();
            values[metric] = Get(metric) + amount;
            Save();
        }

        /// <summary>Retient un record, s'il bat le précédent (plus long wheeling, vitesse de pointe).</summary>
        public static void Best(MissionMetric metric, float value)
        {
            EnsureLoaded();
            if (value <= Get(metric)) return;

            values[metric] = value;
            Save();
        }

        // ---------------------------------------------------------------- sauvegarde

        /// <summary>
        /// JsonUtility ne sait pas sérialiser un dictionnaire : il est écrit en deux listes
        /// parallèles. Les clés sont stockées par nom et non par valeur d'enum, pour qu'insérer une
        /// métrique au milieu de <see cref="MissionMetric"/> ne décale pas les compteurs déjà en
        /// place chez les joueurs.
        /// </summary>
        [Serializable]
        class StatsSave
        {
            public List<string> Keys = new List<string>();
            public List<float> Values = new List<float>();
        }

        static void EnsureLoaded()
        {
            if (values != null) return;

            values = new Dictionary<MissionMetric, float>();

            string json = PlayerPrefs.GetString(SaveKey, null);
            if (string.IsNullOrEmpty(json)) return;

            StatsSave saved = null;
            try
            {
                saved = JsonUtility.FromJson<StatsSave>(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Statistiques illisibles, remises à zéro : " + e.Message);
            }

            if (saved == null) return;

            int count = Mathf.Min(saved.Keys.Count, saved.Values.Count);
            for (int i = 0; i < count; i++)
            {
                // Une métrique retirée du jeu est simplement oubliée au chargement suivant.
                if (Enum.TryParse(saved.Keys[i], out MissionMetric metric)) values[metric] = saved.Values[i];
            }
        }

        static void Save()
        {
            var saved = new StatsSave();
            foreach (var pair in values)
            {
                saved.Keys.Add(pair.Key.ToString());
                saved.Values.Add(pair.Value);
            }

            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(saved));
            Changed?.Invoke();
        }

        /// <summary>Remet tous les compteurs à zéro. Réservé aux outils de test.</summary>
        public static void ResetAll()
        {
            PlayerPrefs.DeleteKey(SaveKey);
            PlayerPrefs.Save();
            values = null;
            Changed?.Invoke();
        }
    }
}
