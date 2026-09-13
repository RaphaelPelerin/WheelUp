namespace WheelingMoto.Data
{
    /// <summary>
    /// Un succès : une grandeur suivie depuis la première partie, franchie en quatre paliers. Là où
    /// une mission se renouvelle et s'oublie, un succès ne redescend jamais — c'est la trace longue
    /// de ce que le joueur a fait, et la seule progression qui survit à une semaine d'absence.
    /// </summary>
    public class AchievementInfo
    {
        public readonly string Id;
        public readonly MissionMetric Metric;

        /// <summary>Nom de la famille, affiché en tête de carte : « Roue levée », « Funambule ».</summary>
        public readonly string Name;

        /// <summary>Libellé du palier, où {0} reçoit la cible mise en forme.</summary>
        public readonly string Format;

        public readonly float[] Targets;
        public readonly int[] Coins;

        /// <summary>Coffre offert au dernier palier, récupérable depuis l'onglet Succès.</summary>
        public readonly string ChestId;

        public AchievementInfo(string id, MissionMetric metric, string name, string format,
            float[] targets, int[] coins, string chestId)
        {
            Id = id;
            Metric = metric;
            Name = name;
            Format = format;
            Targets = targets;
            Coins = coins;
            ChestId = chestId;
        }

        public int TierCount => Targets.Length;

        public string Describe(int tier) => string.Format(Format, MissionCatalog.FormatValue(Metric, Targets[tier]));
    }

    /// <summary>
    /// Les succès du jeu. Ils reposent sur les mêmes grandeurs que les missions
    /// (<see cref="MissionMetric"/>) : une figure rapportée une seule fois par le gameplay fait
    /// avancer la mission du jour et le succès qui la suit, sans double comptage ni double rapport.
    ///
    /// Les cibles sont volontairement longues — le dernier palier de « Roue levée » demande 200 km
    /// de roue en l'air. Un succès qui tombe en une soirée n'est qu'une mission déguisée ; celui-ci
    /// doit rester devant le joueur pendant des mois.
    /// </summary>
    public static class AchievementCatalog
    {
        static readonly int[] Coins = { 250, 600, 1500, 4000 };

        /// <summary>Coffre du dernier palier : le plus cher du jeu, parce que c'est le plus long à mériter.</summary>
        const string FinalChest = "or";

        public static readonly AchievementInfo[] All =
        {
            // Pilotage : le cœur du jeu, donc les familles les plus fournies et les plus exigeantes.
            new AchievementInfo("a_wheelie_dist", MissionMetric.WheelieDistance, "Roue levée",
                "Parcours {0} en wheeling", new[] { 1000f, 10000f, 50000f, 200000f }, Coins, FinalChest),

            new AchievementInfo("a_wheelie_streak", MissionMetric.WheelieStreak, "Funambule",
                "Tiens un wheeling de {0}", new[] { 15f, 30f, 60f, 120f }, Coins, FinalChest),

            new AchievementInfo("a_balance", MissionMetric.BalanceTime, "Point d'équilibre",
                "Passe {0} dans la zone d'équilibre", new[] { 300f, 1800f, 7200f, 21600f }, Coins, FinalChest),

            new AchievementInfo("a_stoppie", MissionMetric.StoppieTime, "Sur le nez",
                "Passe {0} sur la roue avant", new[] { 60f, 300f, 1200f, 3600f }, Coins, FinalChest),

            new AchievementInfo("a_top_speed", MissionMetric.TopSpeed, "Plein gaz",
                "Atteins {0}", new[] { 30f, 42f, 50f, 55f }, Coins, FinalChest),

            // Kilométrage : la famille que tout le monde finit par avancer, même sans rien viser.
            new AchievementInfo("a_distance", MissionMetric.Distance, "Rouleur",
                "Parcours {0}", new[] { 10000f, 100000f, 500000f, 2000000f }, Coins, FinalChest),

            new AchievementInfo("a_night", MissionMetric.NightDistance, "Noctambule",
                "Parcours {0} de nuit", new[] { 5000f, 25000f, 100000f, 400000f }, Coins, FinalChest),

            new AchievementInfo("a_sessions", MissionMetric.SessionsPlayed, "Increvable",
                "Lance {0} parties", new[] { 10f, 50f, 200f, 750f }, Coins, FinalChest),

            // Garage et coffres : la progression hors piste.
            new AchievementInfo("a_motos", MissionMetric.MotoBought, "Collectionneur",
                "Possède {0} motos achetées", new[] { 3f, 8f, 15f, 25f }, Coins, FinalChest),

            new AchievementInfo("a_upgrades", MissionMetric.UpgradeBought, "Mécano",
                "Achète {0} améliorations", new[] { 10f, 50f, 150f, 400f }, Coins, FinalChest),

            new AchievementInfo("a_chests", MissionMetric.ChestOpened, "Ouvreur de caisses",
                "Ouvre {0} coffres", new[] { 5f, 25f, 100f, 300f }, Coins, FinalChest),
        };

        public static AchievementInfo Find(string id)
        {
            foreach (var achievement in All)
            {
                if (achievement.Id == id) return achievement;
            }
            return null;
        }

        /// <summary>
        /// Nom du palier. Quatre rangs plutôt que des chiffres romains : « Roue levée — Légende » se
        /// retient et se raconte, « Roue levée IV » ne dit rien de l'effort fourni.
        /// </summary>
        public static string TierLabel(int tier)
        {
            switch (tier)
            {
                case 0: return "Bronze";
                case 1: return "Argent";
                case 2: return "Or";
                default: return "Légende";
            }
        }
    }
}
