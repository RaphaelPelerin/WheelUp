using System.Collections.Generic;
using UnityEngine;
using WheelingMoto.Data;

namespace WheelingMoto.Core
{
    /// <summary>Gain obtenu à l'ouverture d'un coffre, déjà crédité au joueur.</summary>
    public class ChestReward
    {
        public ChestRewardKind Kind;
        public ChestRarity Rarity;
        public string Title;
        public string Detail;

        /// <summary>
        /// Couleur de la pastille affichée sur la carte de gain. Pour une peinture c'est la teinte
        /// réellement débloquée, ce qui rend le lot lisible d'un coup d'œil.
        /// </summary>
        public Color Swatch;

        /// <summary>Modèle 3D illustrant le lot, sous Resources. Vide : la carte retombe sur la pastille.</summary>
        public string IconPath;
    }

    /// <summary>
    /// Tirage et attribution des gains de coffres. Chaque type de gain a un repli : si le joueur possède
    /// déjà tout ce que le tirage voulait donner, on convertit en pièces plutôt que de rendre un coffre vide.
    /// </summary>
    public static class ChestManager
    {
        static readonly Color CoinColor = new Color(1f, 0.80f, 0.28f);
        static readonly Color UpgradeColor = new Color(0.96f, 0.36f, 0.13f);

        public static bool CanAfford(ChestInfo chest) => EconomyManager.Coins >= chest.Price;

        /// <summary>
        /// Débite le coffre et attribue tous ses lots. Retourne null si le joueur n'a pas assez de pièces.
        /// Les lots sont rendus du plus commun au plus rare : l'écran d'ouverture les révèle dans cet
        /// ordre, pour que le meilleur tombe en dernier.
        /// </summary>
        public static List<ChestReward> Open(ChestInfo chest)
        {
            if (chest == null || !EconomyManager.SpendCoins(chest.Price)) return null;

            var rewards = new List<ChestReward> { GrantCoins(CoinsEntry(chest)) };

            // Une seule moto par coffre : deux d'un coup banaliserait le seul gain vraiment rare.
            bool motoGranted = false;

            for (int i = 1; i < chest.RewardCount; i++)
            {
                var entry = DrawEntry(chest);
                if (entry.Kind == ChestRewardKind.Moto && motoGranted) entry = CoinsEntry(chest);

                ChestReward reward;
                switch (entry.Kind)
                {
                    case ChestRewardKind.Moto: reward = GrantMoto(entry); break;
                    case ChestRewardKind.Upgrade: reward = GrantUpgrade(entry); break;
                    case ChestRewardKind.Paint: reward = GrantPaint(entry); break;
                    default: reward = GrantCoins(entry); break;
                }

                motoGranted |= reward.Kind == ChestRewardKind.Moto;
                rewards.Add(reward);
            }

            rewards.Sort((a, b) => a.Rarity.CompareTo(b.Rarity));
            return rewards;
        }

        /// <summary>Ligne "pièces" du coffre, utilisée pour le lot garanti et comme repli de tirage.</summary>
        static ChestLootEntry CoinsEntry(ChestInfo chest)
        {
            foreach (var entry in chest.Loot)
            {
                if (entry.Kind == ChestRewardKind.Coins) return entry;
            }
            return chest.Loot[0];
        }

        /// <summary>
        /// Tire une ligne de butin en écartant les types qui n'ont plus rien à donner. Sans ce filtre,
        /// un vivier épuisé (toutes les peintures débloquées, toutes les motos possédées) continue de
        /// sortir au tirage et se convertit en pièces sans le dire : le joueur croit ne jamais rien
        /// gagner d'autre, alors que les chances affichées sur la carte lui promettent le contraire.
        /// </summary>
        static ChestLootEntry DrawEntry(ChestInfo chest)
        {
            int total = 0;
            foreach (var entry in chest.Loot)
            {
                if (HasSomethingToGive(entry.Kind)) total += entry.Weight;
            }

            if (total <= 0) return CoinsEntry(chest);

            int roll = Random.Range(0, total);
            foreach (var entry in chest.Loot)
            {
                if (!HasSomethingToGive(entry.Kind)) continue;

                roll -= entry.Weight;
                if (roll < 0) return entry;
            }

            return CoinsEntry(chest);
        }

        /// <summary>
        /// Probabilité que le coffre contienne au moins un lot de ce type, viviers épuisés exclus.
        /// C'est ce que la boutique affiche, et ça doit rester vrai au fil de la progression : une
        /// carte qui promet des peintures alors qu'il n'en reste aucune est un mensonge.
        /// </summary>
        public static float ChanceOfAtLeastOne(ChestInfo chest, ChestRewardKind kind)
        {
            if (!HasSomethingToGive(kind)) return 0f;
            if (kind == ChestRewardKind.Coins) return 1f;

            int total = 0;
            int weight = 0;
            foreach (var entry in chest.Loot)
            {
                if (!HasSomethingToGive(entry.Kind)) continue;

                total += entry.Weight;
                if (entry.Kind == kind) weight += entry.Weight;
            }

            if (weight <= 0 || total <= 0) return 0f;

            return 1f - Mathf.Pow(1f - (float)weight / total, chest.DrawnCount);
        }

        /// <summary>Ce type de lot a-t-il encore quelque chose à offrir au joueur ?</summary>
        public static bool HasSomethingToGive(ChestRewardKind kind)
        {
            switch (kind)
            {
                case ChestRewardKind.Paint: return MotoCustomization.LockedColorIndices().Count > 0;
                case ChestRewardKind.Moto: return LockedMotos().Count > 0;
                case ChestRewardKind.Upgrade: return UpgradableSlots().Count > 0;
                default: return true;
            }
        }

        /// <summary>Motos que le joueur ne possède pas encore.</summary>
        static List<MotoInfo> LockedMotos()
        {
            var candidates = new List<MotoInfo>();
            foreach (var moto in MotoCatalog.All)
            {
                if (!moto.OwnedByDefault && !GarageOwnership.IsOwned(moto.Name)) candidates.Add(moto);
            }
            return candidates;
        }

        /// <summary>Couples moto/poste qui peuvent encore progresser d'un niveau.</summary>
        static List<KeyValuePair<MotoInfo, UpgradeSlot>> UpgradableSlots()
        {
            var candidates = new List<KeyValuePair<MotoInfo, UpgradeSlot>>();
            foreach (var moto in MotoCatalog.All)
            {
                if (!moto.OwnedByDefault && !GarageOwnership.IsOwned(moto.Name)) continue;

                foreach (var slot in MotoUpgrades.Slots)
                {
                    if (GarageOwnership.GetUpgradeLevel(moto.Name, slot.Id) < MotoUpgrades.MaxLevel)
                    {
                        candidates.Add(new KeyValuePair<MotoInfo, UpgradeSlot>(moto, slot));
                    }
                }
            }
            return candidates;
        }

        static ChestReward GrantCoins(ChestLootEntry entry)
        {
            int amount = Random.Range(entry.MinAmount, entry.MaxAmount + 1);
            EconomyManager.AddCoins(amount);

            return new ChestReward
            {
                Kind = ChestRewardKind.Coins,
                Rarity = entry.Rarity,
                Title = $"{amount} pièces",
                Detail = "Ajoutées à ton solde.",
                Swatch = CoinColor,
                IconPath = "Rewards/coins",
            };
        }

        static ChestReward GrantMoto(ChestLootEntry entry)
        {
            var candidates = LockedMotos();

            if (candidates.Count == 0) return GrantConsolation(entry, "Toutes les motos sont déjà débloquées.");

            var won = candidates[Random.Range(0, candidates.Count)];
            GarageOwnership.SetOwned(won.Name);

            return new ChestReward
            {
                Kind = ChestRewardKind.Moto,
                Rarity = entry.Rarity,
                Title = won.Name,
                Detail = $"{won.Category} · débloquée dans ton garage.",
                Swatch = ChestCatalog.RarityColor(entry.Rarity),
                IconPath = won.ModelResourcePath,
            };
        }

        static ChestReward GrantUpgrade(ChestLootEntry entry)
        {
            var candidates = UpgradableSlots();

            if (candidates.Count == 0) return GrantConsolation(entry, "Toutes tes motos sont au maximum.");

            var pick = candidates[Random.Range(0, candidates.Count)];
            int current = GarageOwnership.GetUpgradeLevel(pick.Key.Name, pick.Value.Id);
            int gain = Mathf.Clamp(Random.Range(entry.MinAmount, entry.MaxAmount + 1), 1, MotoUpgrades.MaxLevel - current);
            GarageOwnership.SetUpgradeLevel(pick.Key.Name, pick.Value.Id, current + gain);

            return new ChestReward
            {
                Kind = ChestRewardKind.Upgrade,
                Rarity = entry.Rarity,
                Title = $"{pick.Value.Label} +{gain}",
                Detail = $"{pick.Key.Name} · niveau {current + gain} / {MotoUpgrades.MaxLevel}",
                Swatch = UpgradeColor,
                IconPath = "Rewards/" + pick.Value.Id,
            };
        }

        static ChestReward GrantPaint(ChestLootEntry entry)
        {
            var locked = MotoCustomization.LockedColorIndices();
            if (locked.Count == 0) return GrantConsolation(entry, "Toutes les peintures sont déjà débloquées.");

            int index = locked[Random.Range(0, locked.Count)];
            MotoCustomization.UnlockColor(index);

            return new ChestReward
            {
                Kind = ChestRewardKind.Paint,
                Rarity = entry.Rarity,
                Title = $"Peinture {MotoCustomization.Palette[index].Label}",
                Detail = "Disponible sur toutes tes motos.",
                Swatch = MotoCustomization.Palette[index].Color,
                IconPath = "Rewards/paint",
            };
        }

        /// <summary>Repli en pièces quand le gain tiré n'a plus rien à donner.</summary>
        static ChestReward GrantConsolation(ChestLootEntry entry, string reason)
        {
            int amount = Mathf.Max(300, entry.MaxAmount * 150);
            EconomyManager.AddCoins(amount);

            return new ChestReward
            {
                Kind = ChestRewardKind.Coins,
                Rarity = ChestRarity.Commun,
                Title = $"{amount} pièces",
                Detail = reason,
                Swatch = CoinColor,
                IconPath = "Rewards/coins",
            };
        }
    }
}
