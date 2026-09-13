using System;

namespace WheelingMoto.Core
{
    /// <summary>
    /// Annonce d'une récompense déjà versée. Volontairement sans référence à la mission ou au succès
    /// qui l'a produite : le bandeau de jeu et le récapitulatif n'ont besoin que de trois lignes de
    /// texte, et rester à ce niveau leur évite de connaître deux systèmes de progression au lieu d'un.
    /// </summary>
    public class RewardNotice
    {
        /// <summary>Ligne du haut, en petites capitales : « MISSION ACCOMPLIE », « SUCCÈS DÉBLOQUÉ ».</summary>
        public readonly string Headline;

        /// <summary>Ce qui a été accompli, dans les mots du joueur.</summary>
        public readonly string Label;

        public readonly int Coins;

        /// <summary>
        /// Vrai quand les pièces attendent encore d'être récupérées dans le menu. Les missions
        /// fonctionnent ainsi : boucler l'objectif et encaisser sont deux gestes séparés. Les succès,
        /// eux, paient sur-le-champ — annoncer un palier de plus à réclamer à chaque famille ferait
        /// quarante-quatre boutons à presser.
        /// </summary>
        public readonly bool Pending;

        public RewardNotice(string headline, string label, int coins, bool pending)
        {
            Headline = headline;
            Label = label;
            Coins = coins;
            Pending = pending;
        }
    }

    /// <summary>
    /// Canal unique par lequel passent toutes les récompenses gagnées en jouant, d'où qu'elles
    /// viennent. Les missions et les succès y publient ; le bandeau de jeu et le bilan de session y
    /// écoutent. Ajouter demain une troisième source de récompense n'obligera donc à toucher ni à
    /// l'un ni à l'autre.
    /// </summary>
    public static class RewardFeed
    {
        public static event Action<RewardNotice> Granted;

        public static void Raise(string headline, string label, int coins, bool pending = false)
        {
            Granted?.Invoke(new RewardNotice(headline, label, coins, pending));
        }
    }
}
