using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WheelingMoto.Core;
using WheelingMoto.Data;
using WheelingMoto.Gameplay;

namespace WheelingMoto.UI
{
    /// <summary>
    /// Onglet Garage & Customisation, en trois colonnes : catalogue défilant, aperçu 3D et peinture,
    /// puis caractéristiques, améliorations et achat.
    /// </summary>
    public class GarageMenu
    {
        public GameObject Root { get; private set; }

        /// <summary>Ligne d'amélioration : le libellé du bouton porte le coût, ou "MAX" au niveau maximum.</summary>
        class UpgradeRow
        {
            public UpgradeSlot Slot;
            public TextMeshProUGUI LevelText;
            public Button Button;
            public TextMeshProUGUI ButtonLabel;
        }

        UITheme theme;
        readonly Dictionary<string, Button> motoButtons = new Dictionary<string, Button>();
        string selectedMoto;

        TextMeshProUGUI detailNameText;
        TextMeshProUGUI ownedStateText;
        Button buyButton;

        MotoPreview preview;
        TextMeshProUGUI previewMissingText;

        string selectedPartId = MotoCustomization.Parts[0].Id;
        Button[] partButtons;
        Image[] swatchFrames;
        Button[] swatchButtons;

        Image powerBar;
        Image handlingBar;
        Image wheelieBar;
        readonly List<UpgradeRow> upgradeRows = new List<UpgradeRow>();

        const float RowHeight = 110f;
        const float RowGap = 14f;

        // Section peinture, empilée depuis le bas de la colonne centrale : nuancier, onglets de zone,
        // puis l'intitulé. L'aperçu 3D démarre juste au-dessus. Les hauteurs se déduisent du nombre
        // de teintes, pour qu'ajouter une couleur pousse la section sans chevaucher les onglets.
        const int SwatchesPerRow = 8;
        const float SwatchCellHeight = 62f;
        const float SwatchInset = 4f;    // gouttière entre deux cellules
        const float SwatchBorder = 4f;   // liseré laissé visible par le cadre de sélection
        const float SwatchGap = 3f;
        const float TabHeight = 46f;
        const float LabelHeight = 24f;
        const float SwatchGridBottom = 24f;

        static int SwatchRowCount => Mathf.CeilToInt(MotoCustomization.Palette.Length / (float)SwatchesPerRow);
        static float TabsRowBottomBottom => SwatchGridBottom + SwatchRowCount * SwatchCellHeight + 18f;
        static float TabsRowTopBottom => TabsRowBottomBottom + TabHeight + 8f;
        static float LabelBottom => TabsRowTopBottom + TabHeight + 12f;
        static float PaintSectionHeight => LabelBottom + LabelHeight + 16f;

        public void Build(Transform parent, UITheme t)
        {
            theme = t;

            var panel = UIFactory.AddPanel(parent, "GaragePanel", Color.clear, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Root = panel.gameObject;

            // Le solde n'est affiché qu'une fois, dans le badge permanent en haut à droite du menu.
            UIFactory.AddText(Root.transform, "Title", "GARAGE & CUSTOMISATION", 30, theme.Text, TextAnchor.MiddleLeft,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(30, -50), new Vector2(-30, -6));

            BuildMotoList(Root.transform);

            var showcase = UIFactory.AddPanel(Root.transform, "Showcase", theme.Panel,
                new Vector2(0.26f, 0), new Vector2(0.66f, 1), new Vector2(10, 10), new Vector2(-10, -60));
            BuildShowcase(showcase.transform);

            var detail = UIFactory.AddPanel(Root.transform, "Detail", theme.Panel,
                new Vector2(0.66f, 0), new Vector2(1, 1), new Vector2(10, 10), new Vector2(-20, -60));
            BuildDetail(detail.transform);

            SelectMoto(MotoCatalog.Default.Name);
        }

        void BuildMotoList(Transform parent)
        {
            float contentHeight = MotoCatalog.All.Length * (RowHeight + RowGap);
            var content = UIFactory.AddScrollView(parent, "MotoList",
                new Vector2(0, 0), new Vector2(0.26f, 1), new Vector2(20, 10), new Vector2(-10, -60), contentHeight);

            for (int i = 0; i < MotoCatalog.All.Length; i++)
            {
                var moto = MotoCatalog.All[i];
                float yTop = -i * (RowHeight + RowGap);

                var btn = UIFactory.AddButton(content, "Moto_" + moto.Name, "", theme.PanelAlt, theme.Text, 20,
                    new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, yTop - RowHeight), new Vector2(0, yTop),
                    () => SelectMoto(moto.Name));

                UIFactory.AddText(btn.transform, "Name", moto.Name, 20, theme.Text, TextAnchor.UpperLeft,
                    Vector2.zero, Vector2.one, new Vector2(18, -50), new Vector2(-18, -10));

                UIFactory.AddText(btn.transform, "Category", moto.Category, 15, theme.TextMuted, TextAnchor.LowerLeft,
                    Vector2.zero, Vector2.one, new Vector2(18, 10), new Vector2(-18, 46));

                motoButtons[moto.Name] = btn;
            }
        }

        void BuildShowcase(Transform showcase)
        {
            var previewImage = UIFactory.AddRawImage(showcase, "MotoPreview",
                new Vector2(0, 0), new Vector2(1, 1), new Vector2(20, PaintSectionHeight), new Vector2(-20, -16));

            previewMissingText = UIFactory.AddText(showcase, "PreviewMissing", "Modèle 3D à venir", 16,
                theme.NavTextInactive, TextAnchor.MiddleCenter,
                new Vector2(0, 0), new Vector2(1, 1), new Vector2(20, PaintSectionHeight), new Vector2(-20, -16));

            preview = MotoPreview.Create(previewImage);

            BuildPaintSection(showcase);
        }

        /// <summary>Sélecteur de zone à peindre et nuancier, appliqués en direct sur l'aperçu 3D.</summary>
        void BuildPaintSection(Transform showcase)
        {
            UIFactory.AddText(showcase, "PaintLabel", "Peinture", 18, theme.TextMuted, TextAnchor.MiddleLeft,
                new Vector2(0, 0), new Vector2(1, 0), new Vector2(20, LabelBottom), new Vector2(-20, LabelBottom + LabelHeight));

            var parts = MotoCustomization.Parts;
            partButtons = new Button[parts.Length];

            // Deux rangées : cinq onglets côte à côte seraient illisibles dans cette colonne.
            BuildPartTabs(showcase, 0, 3, TabsRowTopBottom, TabsRowTopBottom + TabHeight);
            BuildPartTabs(showcase, 3, parts.Length, TabsRowBottomBottom, TabsRowBottomBottom + TabHeight);

            BuildSwatchGrid(showcase);
        }

        /// <summary>
        /// Nuancier en grille. Sur une seule rangée, les vingt-quatre teintes se réduisaient à des
        /// bandes de quelques pixels de large : la couleur devenait illisible et la pastille, étirée
        /// par son sprite arrondi, ressemblait à une ellipse. On répartit donc sur plusieurs rangées
        /// pour que chaque pastille reste à peu près carrée.
        /// </summary>
        void BuildSwatchGrid(Transform showcase)
        {
            var palette = MotoCustomization.Palette;
            swatchFrames = new Image[palette.Length];
            swatchButtons = new Button[palette.Length];

            int rows = SwatchRowCount;

            var grid = UIFactory.AddPanel(showcase, "PaintSwatches", Color.clear,
                new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(20, SwatchGridBottom), new Vector2(-20, SwatchGridBottom + rows * SwatchCellHeight));

            for (int i = 0; i < palette.Length; i++)
            {
                int colorIndex = i;
                int column = i % SwatchesPerRow;
                int row = i / SwatchesPerRow;

                float xMin = (float)column / SwatchesPerRow;
                float xMax = (float)(column + 1) / SwatchesPerRow;
                // La grille se remplit du haut vers le bas ; les décalages verticaux sont donc pris
                // depuis le haut du panneau, d'où les ancres à 1 et les valeurs négatives.
                float yTop = -row * SwatchCellHeight;
                float yBottom = yTop - SwatchCellHeight;

                // Le cadre est créé avant la pastille : il passe derrière et déborde en liseré de sélection.
                swatchFrames[i] = UIFactory.AddPanel(grid.transform, "SwatchFrame_" + i, theme.Accent,
                    new Vector2(xMin, 1), new Vector2(xMax, 1),
                    new Vector2(SwatchInset, yBottom + SwatchGap), new Vector2(-SwatchInset, yTop - SwatchGap),
                    rounded: true);
                swatchFrames[i].raycastTarget = false;
                swatchFrames[i].gameObject.SetActive(false);

                swatchButtons[i] = UIFactory.AddButton(grid.transform, "Swatch_" + i, "", palette[i].Color, theme.Text, 12,
                    new Vector2(xMin, 1), new Vector2(xMax, 1),
                    new Vector2(SwatchInset + SwatchBorder, yBottom + SwatchGap + SwatchBorder),
                    new Vector2(-SwatchInset - SwatchBorder, yTop - SwatchGap - SwatchBorder),
                    () => SelectPaintColor(colorIndex));
            }
        }

        void BuildPartTabs(Transform showcase, int from, int to, float bottom, float top)
        {
            var parts = MotoCustomization.Parts;
            int count = to - from;

            var row = UIFactory.AddPanel(showcase, $"PaintParts_{from}", Color.clear,
                new Vector2(0, 0), new Vector2(1, 0), new Vector2(20, bottom), new Vector2(-20, top));

            for (int i = from; i < to; i++)
            {
                var part = parts[i];
                float xMin = (float)(i - from) / count;
                float xMax = (float)(i - from + 1) / count;

                partButtons[i] = UIFactory.AddButton(row.transform, "Part_" + part.Id, part.Label,
                    theme.PanelAlt, theme.Text, 15,
                    new Vector2(xMin, 0), new Vector2(xMax, 1), new Vector2(4, 0), new Vector2(-4, 0),
                    () => SelectPart(part.Id));
            }
        }

        void BuildDetail(Transform detail)
        {
            detailNameText = UIFactory.AddText(detail, "DetailName", "", 24, theme.Text, TextAnchor.MiddleLeft,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(24, -60), new Vector2(-24, -16));

            ownedStateText = UIFactory.AddText(detail, "OwnedState", "", 17, theme.TextMuted, TextAnchor.MiddleLeft,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(24, -94), new Vector2(-24, -64));

            UIFactory.AddText(detail, "StatsLabel", "Performances", 18, theme.Text, TextAnchor.MiddleLeft,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(24, -130), new Vector2(-24, -106));

            powerBar = UIFactory.AddStatBar(detail, "PowerBar", "Puissance", theme,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(24, -166), new Vector2(-24, -136));
            handlingBar = UIFactory.AddStatBar(detail, "HandlingBar", "Maniabilité", theme,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(24, -202), new Vector2(-24, -172));
            wheelieBar = UIFactory.AddStatBar(detail, "WheelieBar", "Cabrage", theme,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(24, -238), new Vector2(-24, -208));

            UIFactory.AddText(detail, "UpgradesLabel", "Améliorations", 18, theme.Text, TextAnchor.MiddleLeft,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(24, -276), new Vector2(-24, -252));

            for (int i = 0; i < MotoUpgrades.Slots.Length; i++)
            {
                BuildUpgradeRow(detail, MotoUpgrades.Slots[i], -288 - i * 84);
            }

            buyButton = UIFactory.AddButton(detail, "BuyButton", "ACHETER LA MOTO", theme.Accent, Color.white, 20,
                new Vector2(0, 0), new Vector2(1, 0), new Vector2(24, 30), new Vector2(-24, 96), OnBuyPressed,
                ButtonKind.Primary);
        }

        void BuildUpgradeRow(Transform detail, UpgradeSlot slot, float yTop)
        {
            var row = UIFactory.AddPanel(detail, "Upgrade_" + slot.Id, theme.PanelAlt,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(24, yTop - 76), new Vector2(-24, yTop), rounded: true);
            row.raycastTarget = false;

            UIFactory.AddText(row.transform, "Label", slot.Label, 17, theme.Text, TextAnchor.MiddleLeft,
                new Vector2(0, 0.5f), new Vector2(0.6f, 1), new Vector2(16, 0), new Vector2(0, -8));

            var levelText = UIFactory.AddText(row.transform, "Level", "", 14, theme.TextMuted, TextAnchor.MiddleLeft,
                new Vector2(0, 0), new Vector2(0.6f, 0.5f), new Vector2(16, 8), new Vector2(0, 0));

            // Libellé non vide obligatoire : AddButton ne crée l'enfant texte que dans ce cas,
            // et c'est lui qui portera ensuite le coût de l'amélioration.
            var button = UIFactory.AddButton(row.transform, "Buy", "-", theme.Accent, Color.white, 16,
                new Vector2(0.6f, 0), new Vector2(1, 1), new Vector2(8, 10), new Vector2(-12, -10),
                () => OnUpgradePressed(slot));

            upgradeRows.Add(new UpgradeRow
            {
                Slot = slot,
                LevelText = levelText,
                Button = button,
                ButtonLabel = button.GetComponentInChildren<TextMeshProUGUI>(),
            });
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

        MotoInfo CurrentMoto() => MotoCatalog.Find(selectedMoto);

        bool IsOwned(MotoInfo moto) => moto.OwnedByDefault || GarageOwnership.IsOwned(moto.Name);

        void RefreshDetail()
        {
            var moto = CurrentMoto();
            if (moto == null) return;

            bool owned = IsOwned(moto);

            detailNameText.text = moto.Name;
            ownedStateText.text = owned ? "Moto possédée" : $"Prix : {moto.Price} pièces";
            buyButton.gameObject.SetActive(!owned);

            var stats = MotoStats.For(moto);
            powerBar.fillAmount = stats.Power / MotoStats.MaxValue;
            handlingBar.fillAmount = stats.Handling / MotoStats.MaxValue;
            wheelieBar.fillAmount = stats.Wheelie / MotoStats.MaxValue;

            RefreshUpgrades(moto, owned);

            bool modelShown = preview != null && preview.Show(moto.ModelResourcePath);
            previewMissingText.gameObject.SetActive(!modelShown);
            if (modelShown) MotoPainter.Apply(preview.CurrentModel, moto.Name);
            RefreshPaintUI();
        }

        void RefreshUpgrades(MotoInfo moto, bool owned)
        {
            foreach (var row in upgradeRows)
            {
                int level = GarageOwnership.GetUpgradeLevel(moto.Name, row.Slot.Id);
                row.LevelText.text = $"Niveau {level} / {MotoUpgrades.MaxLevel}";

                bool maxed = level >= MotoUpgrades.MaxLevel;
                int cost = MotoUpgrades.CostForLevel(moto, level + 1);

                row.ButtonLabel.text = maxed ? "MAX" : $"{cost}";
                row.Button.interactable = owned && !maxed && EconomyManager.Coins >= cost;
                UIFactory.SetButtonColor(row.Button, maxed ? theme.PanelAlt : theme.Accent);
            }
        }

        void SelectPart(string partId)
        {
            selectedPartId = partId;
            RefreshPaintUI();
        }

        void SelectPaintColor(int colorIndex)
        {
            if (!MotoCustomization.IsColorUnlocked(colorIndex)) return;

            MotoCustomization.SetColorIndex(selectedMoto, selectedPartId, colorIndex);
            MotoPainter.Apply(preview.CurrentModel, selectedMoto);
            RefreshPaintUI();
        }

        void RefreshPaintUI()
        {
            var parts = MotoCustomization.Parts;
            for (int i = 0; i < parts.Length; i++)
            {
                UIFactory.SetButtonColor(partButtons[i], parts[i].Id == selectedPartId ? theme.Accent : theme.PanelAlt);
            }

            int selectedColor = MotoCustomization.GetColorIndex(selectedMoto, selectedPartId);
            for (int i = 0; i < swatchFrames.Length; i++)
            {
                bool unlocked = MotoCustomization.IsColorUnlocked(i);
                swatchFrames[i].gameObject.SetActive(unlocked && i == selectedColor);

                // Une couleur verrouillée reste visible mais éteinte : on voit ce qu'on peut gagner.
                var color = MotoCustomization.Palette[i].Color;
                var dimmed = new Color(color.r * 0.25f, color.g * 0.25f, color.b * 0.25f, 1f);
                UIFactory.SetButtonColor(swatchButtons[i], unlocked ? color : dimmed);
                swatchButtons[i].interactable = unlocked;
            }
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

        void OnUpgradePressed(UpgradeSlot slot)
        {
            var moto = CurrentMoto();
            if (moto == null || !IsOwned(moto)) return;

            int level = GarageOwnership.GetUpgradeLevel(moto.Name, slot.Id);
            if (level >= MotoUpgrades.MaxLevel) return;

            if (!EconomyManager.SpendCoins(MotoUpgrades.CostForLevel(moto, level + 1))) return;

            GarageOwnership.SetUpgradeLevel(moto.Name, slot.Id, level + 1);
            RefreshDetail();
        }
    }
}
