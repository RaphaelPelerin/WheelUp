using System;
using UnityEngine;
using WheelingMoto.Data;

namespace WheelingMoto.Core
{
    /// <summary>
    /// Moto que le joueur enfourche. Avant elle, le garage ne servait qu'à regarder : le contrôleur
    /// chargeait toujours <see cref="MotoCatalog.Default"/>, et choisir une moto ne changeait rien
    /// une fois en piste.
    ///
    /// Le choix est validé à chaque lecture plutôt qu'à l'écriture : une sauvegarde effacée, un nom
    /// retiré du catalogue ou une moto perdue ne doivent jamais laisser le joueur sans monture.
    /// </summary>
    public static class Loadout
    {
        const string KeySelected = "loadout_moto";

        /// <summary>Émis au changement de moto : le menu principal et l'onglet Jouer s'y abonnent.</summary>
        public static event Action Changed;

        /// <summary>Moto équipée. Jamais null tant que le catalogue n'est pas vide.</summary>
        public static MotoInfo Selected
        {
            get
            {
                var moto = MotoCatalog.Find(PlayerPrefs.GetString(KeySelected, null));
                return moto != null && IsOwned(moto) ? moto : MotoCatalog.Default;
            }
        }

        public static bool IsSelected(MotoInfo moto) => moto != null && Selected == moto;

        /// <summary>Équipe une moto possédée. Retourne false si elle ne l'est pas — le garage n'a alors rien à changer.</summary>
        public static bool Select(MotoInfo moto)
        {
            if (moto == null || !IsOwned(moto)) return false;
            if (Selected == moto) return false;

            PlayerPrefs.SetString(KeySelected, moto.Name);
            PlayerPrefs.Save();
            Changed?.Invoke();
            return true;
        }

        public static bool IsOwned(MotoInfo moto) => moto.OwnedByDefault || GarageOwnership.IsOwned(moto.Name);

        /// <summary>
        /// Modèle 3D à charger en jeu. La plupart des motos du catalogue n'ont pas encore le leur :
        /// plutôt que de partir sans monture, on retombe sur celui de la moto de départ, qui en a un.
        /// Le joueur roule donc avec la bonne fiche technique et le mauvais dessin, ce qui vaut mieux
        /// qu'une scène vide — et le message dit exactement ce qui manque.
        /// </summary>
        public static string RideableModelPath()
        {
            var moto = Selected;
            if (moto != null && !string.IsNullOrEmpty(moto.ModelResourcePath)) return moto.ModelResourcePath;

            var fallback = MotoCatalog.Default;
            if (moto != null && fallback != null && moto != fallback)
            {
                Debug.LogWarning($"Pas de modèle 3D pour {moto.Name} : {fallback.Name} est affichée à la place.");
            }

            return fallback?.ModelResourcePath;
        }
    }
}
