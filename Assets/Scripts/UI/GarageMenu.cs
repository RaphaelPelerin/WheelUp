using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using WheelingMoto.Core;
using WheelingMoto.Data;

namespace WheelingMoto.UI
{
    /// <summary>Onglet Garage & Customisation : choix de la moto, préparation moteur, achat.</summary>
    public class GarageMenu
    {
        public GameObject Root { get; private set; }

        UITheme theme;
        readonly Dictionary<string, Button> motoButtons = new Dictionary<string, Button>();
        string selectedMoto;

        Text coinsText;
        Text detailNameText;
        Text ownedStateText;
        Text tuningStatusText;
        Button buyButton;
        StepperWidget tuningStepper;
        int tuningLevel = 1;

        const int MaxTuningLevel = 5;

        public void Build(Transform parent, UITheme t)
        {
            theme = t;

            var panel = UIFactory.AddPanel(parent, "GaragePanel", Color.clear, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Root = panel.gameObject;

            UIFactory.AddText(Root.transform, "Title", "GARAGE & CUSTOMISATION", 30, theme.Text, TextAnchor.MiddleLeft,
                new Vector2(0, 1), new Vector2(0.6f, 1), new Vector2(30, -50), new Vector2(-10, -6));

            coinsText = UIFactory.AddText(Root.transform, "Coins", "", 22, theme.Accent, TextAnchor.MiddleRight,
                new Vector2(0.6f, 1), new Vector2(1, 1), new Vector2(10, -50), new Vector2(-30, -6));

            var listContainer = UIFactory.AddPanel(Root.transform, "MotoList", Color.clear,
                new Vector2(0, 0), new Vector2(0.48f, 1), new Vector2(20, 10), new Vector2(-10, -60));

            float rowH = 110f;
            float gap = 14f;
            for (int i = 0; i < MotoCatalog.All.Length; i++)
            {
                var moto = MotoCatalog.All[i];
                float yTop = -i * (rowH + gap);
                var btn = UIFactory.AddButton(listContainer.transform, "Moto_" + moto.Name, "", theme.PanelAlt, theme.Text, 20,
                    new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, yTop - rowH), new Vector2(0, yTop),
                    () => SelectMoto(moto.Name));

                UIFactory.AddText(btn.transform, "Name", moto.Name, 22, theme.Text, TextAnchor.UpperLeft,
                    Vector2.zero, Vector2.one, new Vector2(18, -46), new Vector2(-18, -10));
                string sub = moto.Category + (moto.OwnedByDefault ? " · possédée" : " · " + moto.Price + " pièces");
                UIFactory.AddText(btn.transform, "Category", sub, 16, theme.TextMuted, TextAnchor.LowerLeft,
                    Vector2.zero, Vector2.one, new Vector2(18, 10), new Vector2(-18, 44));

                motoButtons[moto.Name] = btn;
            }

            var detail = UIFactory.AddPanel(Root.transform, "Detail", theme.Panel,
                new Vector2(0.5f, 0), new Vector2(1, 1), new Vector2(10, 10), new Vector2(-20, -60));

            detailNameText = UIFactory.AddText(detail.transform, "DetailName", "", 26, theme.Text, TextAnchor.MiddleLeft,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(24, -66), new Vector2(-24, -16));

            ownedStateText = UIFactory.AddText(detail.transform, "OwnedState", "", 18, theme.TextMuted, TextAnchor.MiddleLeft,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(24, -104), new Vector2(-24, -72));

            UIFactory.AddText(detail.transform, "TuningLabel", "Préparation moteur", 18, theme.TextMuted, TextAnchor.MiddleLeft,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(24, -168), new Vector2(-24, -138));

            tuningStepper = UIFactory.AddStepper(detail.transform, "TuningStepper", theme,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -230), new Vector2(360, -178),
                OnTuningPrev, OnTuningNext);

            tuningStatusText = UIFactory.AddText(detail.transform, "TuningStatus", "", 15, theme.TextMuted, TextAnchor.UpperLeft,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(24, -260), new Vector2(-24, -234));

            buyButton = UIFactory.AddButton(detail.transform, "BuyButton", "ACHETER LA MOTO", theme.Accent, Color.white, 20,
                new Vector2(0, 0), new Vector2(1, 0), new Vector2(24, 30), new Vector2(-24, 96), OnBuyPressed);

            SelectMoto(MotoCatalog.All[0].Name);
        }

        public void RefreshOnShow()
        {
            RefreshDetail();
        }

        void SelectMoto(string name)
        {
            selectedMoto = name;
            foreach (var kv in motoButtons)
            {
                UIFactory.SetButtonColor(kv.Value, kv.Key == name ? theme.Accent : theme.PanelAlt);
            }
            RefreshDetail();
        }

        MotoInfo CurrentMoto()
        {
            return System.Array.Find(MotoCatalog.All, m => m.Name == selectedMoto);
        }

        void RefreshDetail()
        {
            var moto = CurrentMoto();
            if (moto == null) return;

            bool owned = moto.OwnedByDefault || GarageOwnership.IsOwned(moto.Name);

            detailNameText.text = moto.Name;
            ownedStateText.text = owned ? "Moto possédée" : $"Prix : {moto.Price} pièces";
            buyButton.gameObject.SetActive(!owned);

            tuningLevel = GarageOwnership.GetTuningLevel(moto.Name);
            UpdateTuningLabel();

            coinsText.text = $"{EconomyManager.Coins} pièces";
        }

        void UpdateTuningLabel()
        {
            tuningStepper.Label.text = $"Niveau {tuningLevel} / {MaxTuningLevel}";
            tuningStatusText.text = tuningLevel >= MaxTuningLevel
                ? "Moteur au maximum de sa préparation."
                : "Augmente l'accélération et la stabilité du wheeling.";
        }

        void OnBuyPressed()
        {
            var moto = CurrentMoto();
            if (moto == null) return;

            if (EconomyManager.SpendCoins(moto.Price))
            {
                GarageOwnership.SetOwned(moto.Name);
                RefreshDetail();
            }
        }

        void OnTuningPrev()
        {
            tuningLevel = Mathf.Max(1, tuningLevel - 1);
            GarageOwnership.SetTuningLevel(selectedMoto, tuningLevel);
            UpdateTuningLabel();
        }

        void OnTuningNext()
        {
            tuningLevel = Mathf.Min(MaxTuningLevel, tuningLevel + 1);
            GarageOwnership.SetTuningLevel(selectedMoto, tuningLevel);
            UpdateTuningLabel();
        }
    }
}
