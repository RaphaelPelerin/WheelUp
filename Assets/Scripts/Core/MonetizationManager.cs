using System;
using UnityEngine;

namespace WheelingMoto.Core
{
    /// <summary>
    /// Gère le retrait des publicités (achat unique). Stub sans paiement réel :
    /// à brancher plus tard sur Unity IAP (package com.unity.purchasing) pour
    /// déclencher un vrai achat App Store / Play Store.
    /// </summary>
    public static class MonetizationManager
    {
        const string KeyAdsRemoved = "monetization_ads_removed";
        public const string RemoveAdsPrice = "5,00 €";

        public static bool AdsRemoved
        {
            get => PlayerPrefs.GetInt(KeyAdsRemoved, 0) == 1;
            private set => PlayerPrefs.SetInt(KeyAdsRemoved, value ? 1 : 0);
        }

        public static void PurchaseRemoveAds(Action<bool> onComplete)
        {
            AdsRemoved = true;
            onComplete?.Invoke(true);
        }
    }
}
