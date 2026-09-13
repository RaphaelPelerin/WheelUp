using UnityEngine;

namespace WheelingMoto.Data
{
    /// <summary>
    /// Ce qu'une mission mesure. C'est le vocabulaire commun entre le gameplay, qui rapporte, et les
    /// missions, qui comptent : le contrôleur de moto ne connaît pas les missions, il ne connaît que
    /// ces grandeurs. Ajouter un objectif se fait donc en ajoutant une ligne au catalogue, sans
    /// toucher à la conduite.
    /// </summary>
    public enum MissionMetric
    {
        /// <summary>Mètres parcourus roue avant en l'air.</summary>
        WheelieDistance,
        /// <summary>Plus long wheeling tenu d'une traite, en secondes.</summary>
        WheelieStreak,
        /// <summary>Secondes passées dans la zone d'équilibre du wheeling.</summary>
        BalanceTime,
        /// <summary>Secondes passées sur la roue avant.</summary>
        StoppieTime,
        /// <summary>Roues avant réussies, c'est-à-dire tenues assez longtemps pour compter.</summary>
        StoppieCount,
        /// <summary>Mètres parcourus, toutes roues confondues.</summary>
        Distance,
        /// <summary>Vitesse de pointe de la session, en km/h.</summary>
        TopSpeed,
        /// <summary>Secondes passées moto en mouvement.</summary>
        RideTime,
        /// <summary>Mètres parcourus sur une carte réglée sur la nuit.</summary>
        NightDistance,
        /// <summary>Chutes. Aucune mission ne s'en sert : c'est une ligne du récapitulatif de session.</summary>
        Falls,
        /// <summary>Coffres ouverts, quelle que soit leur provenance.</summary>
        ChestOpened,
        /// <summary>Niveaux d'amélioration achetés au garage.</summary>
        UpgradeBought,
        /// <summary>Motos achetées au garage.</summary>
        MotoBought,
        /// <summary>Sessions de jeu lancées.</summary>
        SessionsPlayed,
    }

    /// <summary>Rythme de renouvellement d'une mission.</summary>
    public enum MissionScope
    {
        Daily,
        Weekly,
    }

    /// <summary>Façon dont la grandeur progresse, déduite de la métrique et non déclarée par mission.</summary>
    public enum MissionValueKind
    {
        /// <summary>Les rapports s'additionnent : distances, durées, compteurs.</summary>
        Cumulative,
        /// <summary>Seul le meilleur compte : plus long wheeling, vitesse de pointe.</summary>
        Record,
    }

    /// <summary>
    /// Modèle de mission : une grandeur à atteindre, déclinée en trois paliers de difficulté. Le
    /// tirage du jour choisit le modèle et le palier ; le libellé se compose à partir de la cible,
    /// pour qu'une même ligne de catalogue serve les trois niveaux sans les réécrire.
    /// </summary>
    public class MissionTemplate
    {
        public readonly string Id;
        public readonly MissionMetric Metric;
        public readonly MissionScope Scope;

        /// <summary>Libellé où {0} reçoit la cible déjà mise en forme, unité comprise.</summary>
        public readonly string Format;

        /// <summary>Cibles des trois paliers, dans l'unité de la métrique (mètres, secondes, km/h, unités).</summary>
        public readonly float[] Targets;

        /// <summary>Pièces versées par palier, alignées sur <see cref="Targets"/>.</summary>
        public readonly int[] Coins;

        public MissionTemplate(string id, MissionMetric metric, MissionScope scope, string format, float[] targets, int[] coins)
        {
            Id = id;
            Metric = metric;
            Scope = scope;
            Format = format;
            Targets = targets;
            Coins = coins;
        }

        public int TierCount => Targets.Length;

        public string Describe(int tier) => string.Format(Format, MissionCatalog.FormatValue(Metric, Targets[tier]));
    }

    /// <summary>
    /// Réserve de missions dans laquelle le tirage pioche. Comme les autres catalogues du projet
    /// (motos, coffres, lots de pièces), c'est une table en dur : ajouter une mission, c'est ajouter
    /// une ligne, sans créer d'asset ni ouvrir l'éditeur.
    ///
    /// Équilibrage : les paliers journaliers paient 120 / 200 / 320 pièces, soit 640 par jour si le
    /// joueur boucle les trois. La semaine ajoute 2 900 pièces, plus les coffres de complétion. Un
    /// joueur régulier approche donc les 7 000 pièces hebdomadaires, de quoi s'offrir une Caisse Or
    /// (4 000) et garder de la marge pour le garage. Tous ces nombres tiennent dans ce fichier :
    /// c'est le seul endroit à toucher pour resserrer ou relâcher l'économie.
    /// </summary>
    public static class MissionCatalog
    {
        static readonly int[] DailyCoins = { 120, 200, 320 };
        static readonly int[] WeeklyCoins = { 600, 900, 1400 };

        /// <summary>
        /// Missions du jour. Le pool mêle des objectifs de pilotage et des objectifs de menu : sans
        /// ces derniers, un joueur qui ouvre l'application cinq minutes sans rouler repart les mains
        /// vides, et la mission quotidienne cesse d'être une raison de revenir.
        /// </summary>
        public static readonly MissionTemplate[] Daily =
        {
            // Wheeling : le cœur du jeu, donc la famille la plus fournie du tirage.
            new MissionTemplate("d_wheelie_dist", MissionMetric.WheelieDistance, MissionScope.Daily,
                "Parcours {0} en wheeling", new[] { 200f, 400f, 800f }, DailyCoins),
            new MissionTemplate("d_wheelie_streak", MissionMetric.WheelieStreak, MissionScope.Daily,
                "Tiens un wheeling de {0} d'affilée", new[] { 8f, 14f, 22f }, DailyCoins),
            new MissionTemplate("d_balance", MissionMetric.BalanceTime, MissionScope.Daily,
                "Reste {0} dans la zone d'équilibre", new[] { 20f, 45f, 90f }, DailyCoins),

            // Roue avant : plus difficile à tenir, donc des cibles plus courtes à palier égal.
            new MissionTemplate("d_stoppie_time", MissionMetric.StoppieTime, MissionScope.Daily,
                "Tiens {0} sur la roue avant", new[] { 4f, 8f, 15f }, DailyCoins),
            new MissionTemplate("d_stoppie_count", MissionMetric.StoppieCount, MissionScope.Daily,
                "Réussis {0} roues avant", new[] { 3f, 6f, 12f }, DailyCoins),

            // Conduite : les missions sûres, celles qu'on valide en roulant sans rien viser.
            new MissionTemplate("d_distance", MissionMetric.Distance, MissionScope.Daily,
                "Parcours {0}", new[] { 2000f, 5000f, 10000f }, DailyCoins),
            new MissionTemplate("d_ride_time", MissionMetric.RideTime, MissionScope.Daily,
                "Roule pendant {0}", new[] { 180f, 420f, 900f }, DailyCoins),
            new MissionTemplate("d_night", MissionMetric.NightDistance, MissionScope.Daily,
                "Parcours {0} de nuit", new[] { 1000f, 2500f, 5000f }, DailyCoins),

            // Vitesse : les cibles collent au plafond du contrôleur (maxSpeed = 15,5 m/s, soit
            // 56 km/h). Viser plus haut donnerait une mission impossible à boucler.
            new MissionTemplate("d_top_speed", MissionMetric.TopSpeed, MissionScope.Daily,
                "Atteins {0}", new[] { 40f, 48f, 54f }, DailyCoins),

            // Méta : aucune instrumentation de conduite, ces lignes se branchent sur le menu.
            new MissionTemplate("d_chest", MissionMetric.ChestOpened, MissionScope.Daily,
                "Ouvre {0} coffre", new[] { 1f, 2f, 3f }, DailyCoins),
            new MissionTemplate("d_upgrade", MissionMetric.UpgradeBought, MissionScope.Daily,
                "Achète {0} amélioration au garage", new[] { 1f, 2f, 3f }, DailyCoins),
            new MissionTemplate("d_sessions", MissionMetric.SessionsPlayed, MissionScope.Daily,
                "Lance {0} parties", new[] { 2f, 3f, 5f }, DailyCoins),
        };

        /// <summary>
        /// Missions de la semaine. Les cibles valent grossièrement cinq jours de jeu et non sept :
        /// une mission hebdomadaire qui exige d'être là tous les jours punit la moindre absence, et
        /// le joueur cesse de la regarder dès le premier jour manqué.
        /// </summary>
        public static readonly MissionTemplate[] Weekly =
        {
            new MissionTemplate("w_wheelie_dist", MissionMetric.WheelieDistance, MissionScope.Weekly,
                "Parcours {0} en wheeling", new[] { 2500f, 5000f, 10000f }, WeeklyCoins),
            new MissionTemplate("w_wheelie_streak", MissionMetric.WheelieStreak, MissionScope.Weekly,
                "Tiens un wheeling de {0} d'affilée", new[] { 20f, 28f, 36f }, WeeklyCoins),
            new MissionTemplate("w_balance", MissionMetric.BalanceTime, MissionScope.Weekly,
                "Reste {0} dans la zone d'équilibre", new[] { 240f, 480f, 900f }, WeeklyCoins),
            new MissionTemplate("w_stoppie", MissionMetric.StoppieTime, MissionScope.Weekly,
                "Tiens {0} sur la roue avant", new[] { 30f, 60f, 110f }, WeeklyCoins),
            new MissionTemplate("w_distance", MissionMetric.Distance, MissionScope.Weekly,
                "Parcours {0}", new[] { 15000f, 30000f, 60000f }, WeeklyCoins),
            new MissionTemplate("w_chests", MissionMetric.ChestOpened, MissionScope.Weekly,
                "Ouvre {0} coffres", new[] { 3f, 5f, 8f }, WeeklyCoins),
            new MissionTemplate("w_upgrades", MissionMetric.UpgradeBought, MissionScope.Weekly,
                "Achète {0} améliorations", new[] { 3f, 5f, 8f }, WeeklyCoins),
            new MissionTemplate("w_sessions", MissionMetric.SessionsPlayed, MissionScope.Weekly,
                "Lance {0} parties", new[] { 8f, 14f, 22f }, WeeklyCoins),
        };

        /// <summary>
        /// Nombre de missions actives à la fois, par rythme. Trois : assez pour varier, assez peu
        /// pour tenir sur un écran de téléphone sans défilement.
        /// </summary>
        public const int DailySlots = 3;
        public const int WeeklySlots = 3;

        public static MissionTemplate[] Pool(MissionScope scope) => scope == MissionScope.Weekly ? Weekly : Daily;

        public static int Slots(MissionScope scope) => scope == MissionScope.Weekly ? WeeklySlots : DailySlots;

        /// <summary>Retrouve un modèle par son identifiant. Null si la sauvegarde cite une mission retirée du catalogue.</summary>
        public static MissionTemplate Find(string id)
        {
            foreach (var template in Daily)
            {
                if (template.Id == id) return template;
            }
            foreach (var template in Weekly)
            {
                if (template.Id == id) return template;
            }
            return null;
        }

        /// <summary>
        /// Nature de la grandeur. Elle se déduit de la métrique plutôt que d'être déclarée sur chaque
        /// modèle : une vitesse de pointe ne s'additionne jamais, et laisser le choix ligne à ligne
        /// finirait par produire une mission « atteins 150 km/h cumulés ».
        /// </summary>
        public static MissionValueKind ValueKind(MissionMetric metric)
        {
            switch (metric)
            {
                case MissionMetric.WheelieStreak:
                case MissionMetric.TopSpeed:
                    return MissionValueKind.Record;
                default:
                    return MissionValueKind.Cumulative;
            }
        }

        /// <summary>Met en forme une valeur dans l'unité de sa métrique, aussi bien une cible qu'une progression.</summary>
        public static string FormatValue(MissionMetric metric, float value)
        {
            switch (metric)
            {
                case MissionMetric.WheelieDistance:
                case MissionMetric.Distance:
                case MissionMetric.NightDistance:
                    // Au-delà du kilomètre, les mètres ne se lisent plus d'un coup d'œil.
                    return value >= 1000f
                        ? $"{value / 1000f:0.#} km"
                        : $"{Mathf.RoundToInt(value)} m";

                case MissionMetric.WheelieStreak:
                case MissionMetric.BalanceTime:
                case MissionMetric.StoppieTime:
                case MissionMetric.RideTime:
                    return FormatDuration(value);

                case MissionMetric.TopSpeed:
                    return $"{Mathf.RoundToInt(value)} km/h";

                default:
                    return Mathf.RoundToInt(value).ToString();
            }
        }

        static string FormatDuration(float seconds)
        {
            int total = Mathf.RoundToInt(seconds);
            if (total < 60) return $"{total} s";

            int minutes = total / 60;
            int rest = total % 60;
            return rest == 0 ? $"{minutes} min" : $"{minutes} min {rest} s";
        }

        /// <summary>Nom court de la grandeur, pour le récapitulatif de fin de session.</summary>
        public static string MetricLabel(MissionMetric metric)
        {
            switch (metric)
            {
                case MissionMetric.WheelieDistance: return "Distance en wheeling";
                case MissionMetric.WheelieStreak: return "Plus long wheeling";
                case MissionMetric.BalanceTime: return "Temps en équilibre";
                case MissionMetric.StoppieTime: return "Temps sur la roue avant";
                case MissionMetric.StoppieCount: return "Roues avant";
                case MissionMetric.Distance: return "Distance parcourue";
                case MissionMetric.TopSpeed: return "Vitesse de pointe";
                case MissionMetric.RideTime: return "Temps de conduite";
                case MissionMetric.NightDistance: return "Distance de nuit";
                case MissionMetric.Falls: return "Chutes";
                default: return metric.ToString();
            }
        }
    }
}
