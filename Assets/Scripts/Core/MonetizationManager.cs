using System;
using UnityEngine;
using WheelingMoto.Data;

namespace WheelingMoto.Core
{
    /// <summary>
    /// Achats en argent réel : retrait des publicités (achat unique) et lots de pièces, et publicités à
    /// récompense, que le joueur choisit de regarder pour rattraper une prouesse perdue à la chute.
    ///
    /// Stub sans paiement ni régie : à brancher plus tard sur Unity IAP (com.unity.purchasing) et sur
    /// Unity Ads (com.unity.ads). Les deux points d'entrée uniques sont <see cref="Purchase"/> et
    /// <see cref="ShowRewardedAd"/>, pour que la vraie intégration n'ait que ces endroits à remplacer et
    /// que l'interface n'ait pas à changer.
    /// </summary>
    public static class MonetizationManager
    {
        const string KeyAdsRemoved = "monetization_ads_removed";
        public const string RemoveAdsId = "remove_ads";
        public const string RemoveAdsPrice = "5,00 €";

        /// <summary>Émis après un achat abouti : la boutique et les paramètres s'y accrochent.</summary>
        public static event Action Changed;

        public static bool AdsRemoved
        {
            get => PlayerPrefs.GetInt(KeyAdsRemoved, 0) == 1;
            private set
            {
                PlayerPrefs.SetInt(KeyAdsRemoved, value ? 1 : 0);
                Changed?.Invoke();
            }
        }

        /// <summary>
        /// Débite le joueur auprès du store, puis livre le produit. Tant que le paiement n'est pas
        /// branché, la transaction est acceptée d'office : c'est la seule ligne à remplacer.
        /// </summary>
        static void Purchase(string productId, Action<bool> onComplete)
        {
            bool paid = true;
            onComplete?.Invoke(paid);
        }

        public static void PurchaseRemoveAds(Action<bool> onComplete)
        {
            Purchase(RemoveAdsId, success =>
            {
                if (success) AdsRemoved = true;
                onComplete?.Invoke(success);
            });
        }

        /// <summary>
        /// Publicité à récompense, toujours à la demande du joueur (rattraper une prouesse après une chute).
        /// <paramref name="onComplete"/> reçoit vrai si la pub a été regardée jusqu'au bout, donc si la
        /// récompense est due. Tant que la régie n'est pas branchée, la vidéo est considérée vue ; pour un
        /// joueur qui a acheté le retrait des publicités, la récompense est donnée sans rien lui montrer.
        /// </summary>
        public static void ShowRewardedAd(Action<bool> onComplete)
        {
            bool watched = true;
            onComplete?.Invoke(watched);
        }

        /// <summary>Achète un lot de pièces et crédite le solde dès que le paiement est accepté.</summary>
        public static void PurchaseCoinPack(CoinPack pack, Action<bool> onComplete)
        {
            if (pack == null)
            {
                onComplete?.Invoke(false);
                return;
            }

            Purchase(pack.Id, success =>
            {
                if (success) EconomyManager.AddCoins(pack.Total);
                onComplete?.Invoke(success);
            });
        }
    }
}
