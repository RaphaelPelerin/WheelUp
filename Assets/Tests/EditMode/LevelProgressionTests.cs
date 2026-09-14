using System.Collections.Generic;
using NUnit.Framework;
using WheelingMoto.Core;
using WheelingMoto.Data;

namespace WheelingMoto.Tests
{
    /// <summary>
    /// Tests de la progression par niveaux.
    ///
    /// Tous portent sur des fonctions pures : la courbe, la table des récompenses et la cohérence
    /// des paliers avec le catalogue. Aucun ne touche aux PlayerPrefs — un test qui écrirait dans la
    /// sauvegarde réelle effacerait la progression de celui qui le lance, et un test destructeur
    /// finit toujours par ne plus être lancé.
    ///
    /// C'est aussi pourquoi <see cref="LevelManager.AddXp"/> n'est pas couvert directement : il écrit
    /// dans les PlayerPrefs. Ce qu'il a de délicat — franchir plusieurs niveaux d'un coup sans en
    /// oublier — repose entièrement sur la monotonie de la courbe, elle, vérifiée ici. Le couvrir
    /// vraiment demanderait de rendre le stockage injectable, ce qui n'est pas le sujet du jour.
    /// </summary>
    public class LevelProgressionTests
    {
        // --------------------------------------------------------------------------------- courbe

        [Test]
        public void LeNiveauUnNeCouteRien()
        {
            Assert.AreEqual(0, LevelManager.XpToReach(1));
        }

        [Test]
        public void LaCourbeSuitLesJalonsDeLaConception()
        {
            // Ces cinq nombres sont le contrat de rythme : le niveau 25 à 7 020 XP, c'est la
            // promesse des deux semaines. Les changer doit être un geste conscient.
            Assert.AreEqual(120, LevelManager.XpToReach(2), "niveau 2");
            Assert.AreEqual(570, LevelManager.XpToReach(5), "niveau 5 — 1er palier moto");
            Assert.AreEqual(1620, LevelManager.XpToReach(10), "niveau 10");
            Assert.AreEqual(3045, LevelManager.XpToReach(15), "niveau 15");
            Assert.AreEqual(4845, LevelManager.XpToReach(20), "niveau 20");
            Assert.AreEqual(7020, LevelManager.XpToReach(25), "niveau 25 — dernier palier moto");
        }

        [Test]
        public void LaCourbeEstStrictementCroissante()
        {
            // C'est ce qui garantit que franchir plusieurs niveaux d'un coup reste une plage
            // contiguë : sans monotonie, la boucle d'AddXp sauterait ou répéterait des paliers.
            for (int level = 1; level < 200; level++)
            {
                Assert.Less(LevelManager.XpToReach(level), LevelManager.XpToReach(level + 1),
                    $"le niveau {level + 1} doit coûter plus que le niveau {level}");
            }
        }

        [Test]
        public void ChaqueNiveauCouteDavantageQueLePrecedent()
        {
            int previous = 0;
            for (int level = 1; level < 100; level++)
            {
                int cost = LevelManager.XpToReach(level + 1) - LevelManager.XpToReach(level);
                Assert.GreaterOrEqual(cost, previous, $"le coût du niveau {level + 1} a reculé");
                previous = cost;
            }
        }

        // ---------------------------------------------------------------------- table des paliers

        [Test]
        public void LeNiveauUnNeDonneRien()
        {
            Assert.IsNull(LevelRewardCatalog.RewardFor(1), "le niveau 1 est l'état de départ");
        }

        [Test]
        public void LesCinqPaliersDeMotoTombentTousLesCinqNiveaux()
        {
            for (int tier = 1; tier <= LevelRewardCatalog.MotoTierCount; tier++)
            {
                int level = tier * LevelRewardCatalog.LevelsPerTier;
                var reward = LevelRewardCatalog.RewardFor(level);

                Assert.IsNotNull(reward, $"niveau {level}");
                Assert.IsTrue(reward.IsMotoChoice, $"le niveau {level} doit proposer une moto");
                Assert.AreEqual(tier, reward.MotoTier, $"niveau {level}");
            }
        }

        [Test]
        public void LeNiveauCinqDonneUneMotoEtNonUnCoffreBronze()
        {
            // Le niveau 5 est à la fois impair et multiple de cinq : l'ordre des tests de RewardFor
            // décide lequel l'emporte, et c'est précisément le piège que ce test garde fermé.
            var reward = LevelRewardCatalog.RewardFor(5);

            Assert.IsTrue(reward.IsMotoChoice);
            Assert.IsNull(reward.ChestId);
        }

        [Test]
        public void LeCreneauDesMultiplesDeCinqPasseALOrApresLeDernierPalier()
        {
            foreach (int level in new[] { 30, 35, 40, 100 })
            {
                var reward = LevelRewardCatalog.RewardFor(level);

                Assert.IsFalse(reward.IsMotoChoice, $"niveau {level}");
                Assert.AreEqual(LevelRewardCatalog.GoldChest, reward.ChestId, $"niveau {level}");
            }
        }

        [Test]
        public void EntreLesPaliersLesCoffresAlternentBronzeEtArgent()
        {
            for (int level = 2; level <= 60; level++)
            {
                if (level % LevelRewardCatalog.LevelsPerTier == 0) continue;

                var reward = LevelRewardCatalog.RewardFor(level);
                string expected = level % 2 == 0
                    ? LevelRewardCatalog.SilverChest
                    : LevelRewardCatalog.BronzeChest;

                Assert.AreEqual(expected, reward.ChestId, $"niveau {level}");
            }
        }

        [Test]
        public void ChaqueNiveauDonneQuelqueChose()
        {
            for (int level = 2; level <= 120; level++)
            {
                var reward = LevelRewardCatalog.RewardFor(level);

                Assert.IsNotNull(reward, $"niveau {level} : aucune récompense");
                Assert.IsTrue(reward.IsMotoChoice || !string.IsNullOrEmpty(reward.ChestId),
                    $"niveau {level} : récompense vide");
            }
        }

        [Test]
        public void LesCoffresDesPaliersExistentAuCatalogue()
        {
            foreach (string id in new[]
            {
                LevelRewardCatalog.BronzeChest,
                LevelRewardCatalog.SilverChest,
                LevelRewardCatalog.GoldChest,
            })
            {
                Assert.IsNotNull(ChestCatalog.Find(id), $"coffre « {id} » absent du catalogue");
            }
        }

        // ------------------------------------------------------------------- cohérence des motos

        [Test]
        public void ChaquePalierProposeTroisMotosConnuesDuCatalogue()
        {
            for (int tier = 1; tier <= LevelRewardCatalog.MotoTierCount; tier++)
            {
                var motos = LevelRewardCatalog.MotosForTier(tier);

                Assert.AreEqual(3, motos.Count,
                    $"palier {tier} : une moto a été renommée ou retirée du catalogue");
            }
        }

        [Test]
        public void AucuneMotoNApparaitDansDeuxPaliers()
        {
            var seen = new HashSet<string>();
            foreach (string name in LevelRewardCatalog.AllTierMotoNames())
            {
                Assert.IsTrue(seen.Add(name), $"« {name} » apparaît dans deux paliers");
            }
        }

        [Test]
        public void LesTroisMotosDUnPalierSontEquivalentesEnPuissance()
        {
            // Le cœur de la conception : un palier dont une moto domine les deux autres ne propose
            // pas un choix mais un piège. Le seuil est large — le palier le plus dispersé est à
            // 22,5 % — mais il attrape une substitution distraite dans le catalogue.
            const float maxSpread = 0.25f;

            for (int tier = 1; tier <= LevelRewardCatalog.MotoTierCount; tier++)
            {
                var motos = LevelRewardCatalog.MotosForTier(tier);

                float min = float.MaxValue;
                float max = float.MinValue;
                foreach (var moto in motos)
                {
                    min = System.Math.Min(min, moto.Specs.PowerHp);
                    max = System.Math.Max(max, moto.Specs.PowerHp);
                }

                float spread = (max - min) / min;
                Assert.LessOrEqual(spread, maxSpread,
                    $"palier {tier} : {spread:P0} d'écart de puissance entre {min} et {max} ch");
            }
        }

        [Test]
        public void LesPaliersMontentEnPuissance()
        {
            float previous = 0f;
            for (int tier = 1; tier <= LevelRewardCatalog.MotoTierCount; tier++)
            {
                float weakest = float.MaxValue;
                foreach (var moto in LevelRewardCatalog.MotosForTier(tier))
                {
                    weakest = System.Math.Min(weakest, moto.Specs.PowerHp);
                }

                Assert.Greater(weakest, previous,
                    $"le palier {tier} doit dépasser le précédent, sinon monter de niveau régresse");
                previous = weakest;
            }
        }

        [Test]
        public void AucuneMotoDePalierNEstDejaOfferteAuDepart()
        {
            foreach (string name in LevelRewardCatalog.AllTierMotoNames())
            {
                var moto = MotoCatalog.Find(name);

                Assert.IsFalse(moto.OwnedByDefault,
                    $"« {name} » est offerte au départ : son palier ne donnerait rien");
            }
        }

        // ------------------------------------------------------------------------- repli graphique

        [Test]
        public void LeCatalogueGardeAuMoinsUneMotoAvecUnModele()
        {
            // Ce test aurait échoué au moment où la Ninja H2 a cessé d'être offerte : le repli de
            // RideableModelPath cherchait alors une moto à la fois offerte *et* pourvue d'un modèle,
            // et plus aucune moto ne se serait affichée en jeu.
            Assert.IsNotNull(MotoCatalog.FirstWithModel,
                "aucune moto n'a de modèle : plus rien ne s'affiche en conduite");
        }

        [Test]
        public void LaMotoDeDepartExiste()
        {
            Assert.IsNotNull(MotoCatalog.Default, "le joueur doit commencer avec une moto");
        }
    }
}
