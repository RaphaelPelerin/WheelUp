using System;
using UnityEngine;
using WheelingMoto.Data;

namespace WheelingMoto.Core
{
    /// <summary>
    /// Achats en argent réel : retrait des publicités (achat unique) et lots de pièces.
    ///
    /// Stub sans paiement : à brancher plus tard sur Unity IAP (package com.unity.purchasing).
    /// Le point d'entrée unique est <see cref="Purchase"/>, pour que l'intégration du vrai store
    /// n'ait qu'un seul endroit à remplacer et que l'interface n'ait pas à changer.
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
