using System;
using UnityEngine;
using WheelingMoto.Data;

namespace WheelingMoto.Core
{
    /// <summary>
    /// Cartes débloquées par le joueur. Même principe que le garage : l'achat passe par
    /// <see cref="EconomyManager"/>, et rien n'est livré si le solde ne suit pas.
    ///
    /// C'est le second débouché des pièces après les motos et les coffres. Il est volontairement
    /// cher : une carte change le décor de toutes les parties suivantes, là où une moto de plus ne
    /// change qu'une ligne de statistiques.
    /// </summary>
    public static class MapOwnership
    {
        /// <summary>Émis après un déblocage : l'onglet Jouer s'y abonne.</summary>
        public static event Action Changed;

        public static bool IsUnlocked(MapInfo map)
        {
            if (map == null) return false;
            return map.UnlockedByDefault || PlayerPrefs.GetInt(Key(map), 0) == 1;
        }

        public static bool IsUnlocked(MapId id) => IsUnlocked(MapCatalog.Find(id));

        public static bool CanAfford(MapInfo map) => map != null && EconomyManager.Coins >= map.Price;

        /// <summary>
        /// Débite et débloque. Retourne false si la carte est déjà ouverte ou si le solde ne suffit
        /// pas — dans les deux cas rien n'est prélevé.
        /// </summary>
        public static bool Buy(MapInfo map)
        {
            if (map == null || IsUnlocked(map)) return false;
            if (!EconomyManager.SpendCoins(map.Price)) return false;

            PlayerPrefs.SetInt(Key(map), 1);
            PlayerPrefs.Save();
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// Carte réellement utilisable pour lancer une partie. Une carte verrouillée peut rester
        /// sélectionnée dans <see cref="GameSession"/> — une sauvegarde effacée, par exemple — et
        /// c'est ici qu'on s'assure de ne jamais lancer une partie dessus.
        /// </summary>
        public static MapInfo Playable(MapId id)
        {
            var map = MapCatalog.Find(id);
            return IsUnlocked(map) ? map : MapCatalog.Default;
        }

        static string Key(MapInfo map) => "map_unlocked_" + map.Id;
    }
}
