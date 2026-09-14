using System.Collections.Generic;
using UnityEngine;

namespace WheelingMoto.Data
{
    /// <summary>Ce qu'un niveau rapporte : soit un choix de moto, soit un coffre. Jamais les deux.</summary>
    public class LevelReward
    {
        /// <summary>Palier de motos proposé, de 1 à <see cref="LevelRewardCatalog.MotoTierCount"/>. Zéro si ce niveau donne un coffre.</summary>
        public readonly int MotoTier;

        /// <summary>Identifiant du coffre offert, ou null si ce niveau propose un choix de moto.</summary>
        public readonly string ChestId;

        LevelReward(int motoTier, string chestId)
        {
            MotoTier = motoTier;
            ChestId = chestId;
        }

        public bool IsMotoChoice => MotoTier > 0;

        public static LevelReward Moto(int tier) => new LevelReward(tier, null);
        public static LevelReward Chest(string id) => new LevelReward(0, id);
    }

    /// <summary>
    /// Table des récompenses de niveau.
    ///
    /// Tous les cinq niveaux, le joueur choisit une moto parmi trois — cinq fois, jusqu'au niveau 25.
    /// Ce créneau devient ensuite un coffre or. Entre les paliers, les niveaux versent du bronze et
    /// de l'argent en alternance, pour qu'aucune montée ne soit vide.
    ///
    /// Les trois motos d'un palier sont choisies **équivalentes en puissance** : au palier 2 les
    /// trois font exactement 15 ch, au palier 5 l'écart tombe à 3,5 %. Un palier dont une moto
    /// domine les deux autres ne propose pas un choix mais un piège, et le joueur qui découvre après
    /// coup qu'il a pris la mauvaise n'a plus qu'un regret. Ce qui les sépare, c'est le caractère :
    /// la Husqvarna du palier 3 est 20 kg plus légère que la MT-07 mais plafonne 24 km/h plus bas.
    ///
    /// Les motos absentes de cette table — la Ninja H2 la première — restent à acheter aux pièces,
    /// tout comme les deux motos écartées à chaque palier.
    /// </summary>
    public static class LevelRewardCatalog
    {
        /// <summary>Un palier de motos tous les cinq niveaux.</summary>
        public const int LevelsPerTier = 5;

        /// <summary>Nombre de paliers de moto, donc de motos offertes sur tout le parcours.</summary>
        public const int MotoTierCount = 5;

        /// <summary>Dernier niveau qui propose une moto. Au-delà, le créneau des multiples de cinq passe à l'or.</summary>
        public const int LastMotoLevel = LevelsPerTier * MotoTierCount;

        public const string BronzeChest = "bronze";
        public const string SilverChest = "argent";
        public const string GoldChest = "or";

        /// <summary>
        /// Noms des motos par palier, tels qu'ils figurent dans <see cref="MotoCatalog"/>. Les noms
        /// servent de clé parce que c'est déjà ainsi que <see cref="MotoCatalog.Find"/> et la
        /// possession au garage les désignent : un index se serait décalé au premier ajout au
        /// catalogue, et silencieusement.
        /// </summary>
        static readonly string[][] TierMotoNames =
        {
            // Palier 1 — les mécaboites. La Grom est empruntée aux 125 : le catalogue n'a que trois
            // cinquantièmes et la Beta RR 50 est la moto de départ. À 9,8 ch pour 103 kg, elle reste
            // dans la classe.
            new[] { "Derbi Senda X-Treme", "Rieju MRT 50", "Honda MSX125 Grom" },

            // Palier 2 — les 125. Les trois font exactement 15 ch : l'équivalence est parfaite, et le
            // choix ne porte que sur le caractère, roadster contre sportive.
            new[] { "Yamaha MT-125", "KTM 125 Duke", "Aprilia RS 125" },

            // Palier 3 — ça devient sérieux. Roadster, roadster, supermotard, à 68, 73 et 74 ch.
            new[] { "Kawasaki Z650", "Yamaha MT-07", "Husqvarna 701 Supermoto" },

            // Palier 4 — trois catégories différentes à puissance égale : supermotard, sportive,
            // roadster, dans un mouchoir de 4 %.
            new[] { "Ducati Hypermotard 950", "Yamaha YZF-R6", "Yamaha MT-09" },

            // Palier 5 — les superbikes. Que des sportives, et c'est délibéré : arriver au bout du
            // parcours doit donner une 1000, pas une moto intermédiaire de plus.
            new[] { "Yamaha YZF-R1", "Kawasaki Ninja ZX-10R", "BMW S1000RR" },
        };

        /// <summary>
        /// Récompense d'un niveau. Retourne null pour le niveau 1, qui est l'état de départ et ne se
        /// gagne pas.
        ///
        /// L'ordre des tests compte : le niveau 5 est à la fois impair et multiple de cinq, et c'est
        /// le palier de moto qui doit l'emporter sur le coffre bronze.
        /// </summary>
        public static LevelReward RewardFor(int level)
        {
            if (level <= 1) return null;

            if (level % LevelsPerTier == 0)
            {
                return level <= LastMotoLevel
                    ? LevelReward.Moto(level / LevelsPerTier)
                    : LevelReward.Chest(GoldChest);
            }

            return LevelReward.Chest(level % 2 == 0 ? SilverChest : BronzeChest);
        }

        /// <summary>
        /// XP versé par une mission récupérée. Une mission de la semaine vaut environ trois fois une
        /// mission du jour : elle demande sept jours de jeu, la payer au même prix reviendrait à
        /// n'avoir aucune raison de la suivre.
        /// </summary>
        public static int MissionXp(MissionScope scope, int tier)
        {
            int[] table = scope == MissionScope.Weekly ? WeeklyMissionXp : DailyMissionXp;
            return table[Mathf.Clamp(tier, 0, table.Length - 1)];
        }

        static readonly int[] DailyMissionXp = { 60, 100, 160 };
        static readonly int[] WeeklyMissionXp = { 200, 350, 500 };

        /// <summary>Points de prouesse qu'il faut amasser pour un point d'XP.</summary>
        public const int StuntPointsPerXp = 100;

        /// <summary>Niveau auquel tombe un palier donné, pour l'afficher dans la pop-up de progression.</summary>
        public static int LevelForTier(int tier) => tier * LevelsPerTier;

        /// <summary>
        /// Les trois motos d'un palier. Celles que le catalogue ne connaît plus sont écartées
        /// silencieusement plutôt que de faire tomber l'écran de choix : mieux vaut proposer deux
        /// motos qu'aucune si quelqu'un renomme une ligne du catalogue.
        /// </summary>
        public static List<MotoInfo> MotosForTier(int tier)
        {
            var motos = new List<MotoInfo>();
            if (tier < 1 || tier > TierMotoNames.Length) return motos;

            foreach (string name in TierMotoNames[tier - 1])
            {
                var moto = MotoCatalog.Find(name);
                if (moto != null) motos.Add(moto);
            }
            return motos;
        }

        /// <summary>Tous les noms figurant aux paliers, pour les tests de cohérence du catalogue.</summary>
        public static IEnumerable<string> AllTierMotoNames()
        {
            foreach (var tier in TierMotoNames)
            {
                foreach (string name in tier) yield return name;
            }
        }
    }
}
