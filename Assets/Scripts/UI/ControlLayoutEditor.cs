using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WheelingMoto.Core;

namespace WheelingMoto.UI
{
    /// <summary>
    /// Éditeur de disposition des commandes, ouvert depuis les Paramètres, au menu principal comme en pause.
    /// Il montre les commandes de conduite à leur taille et à leur place réelles, sur la même zone sûre que le
    /// HUD : le joueur fait glisser chacune où il veut, et la retrouve exactement là en roulant.
    ///
    /// Seule la commande de direction choisie est affichée (flèches ou joystick). Les deux règles de placement
    /// sont celles du gabarit (<see cref="ControlLayout"/>) : jamais contre le bord, jamais dans la bande du
    /// haut où vivent VUE et PAUSE. Une commande lâchée sur une autre revient d'où elle est partie, puisque
    /// l'une des deux ne pourrait plus être touchée.
    ///
    /// Rien n'est enregistré avant VALIDER : ANNULER, ou le retour arrière du téléphone, rend la disposition
    /// telle qu'elle était. Un seul éditeur est ouvert à la fois ; il est détruit à la fermeture.
    /// </summary>
    public class ControlLayoutEditor
    {
        const float ToolbarButtonWidth = 230f;

        static readonly Color ItemColor = new Color(1f, 1f, 1f, 0.12f);
        static readonly Color ItemHeldColor = new Color(1f, 1f, 1f, 0.28f);
        static readonly Color ItemBlockedColor = new Color(UITheme.Brand.r, UITheme.Brand.g, UITheme.Brand.b, 0.6f);
        static readonly Color ItemTextColor = new Color(1f, 1f, 1f, 0.9f);
        static readonly Color GhostColor = new Color(1f, 1f, 1f, 0.06f);
        static readonly Color GhostTextColor = new Color(1f, 1f, 1f, 0.35f);
        static readonly Color SeparatorColor = new Color(1f, 1f, 1f, 0.12f);

        static ControlLayoutEditor current;

        class Item
        {
            public DrivingControl Control;
            public RectTransform Rect;
            public Image Background;
            public Vector2 Center;
            public Vector2 PressCenter;
            public Vector2 GrabOffset;
            /// <summary>Déplacée pendant cette ouverture : sa place sera enregistrée à la validation.</summary>
            public bool Moved;
        }

        readonly List<Item> items = new List<Item>();
        ControlLayout layout;
        GameObject root;
        Action closed;
        /// <summary>RÉINITIALISER a été touché : la validation efface toutes les places enregistrées.</summary>
        bool resetAll;

        /// <summary>Vrai tant que l'éditeur est affiché. Redevient faux si la scène qui le portait se ferme.</summary>
        public static bool Showing => current != null && current.root != null;

        /// <param name="canvasRoot">Racine du canvas : l'éditeur recouvre tout l'écran.</param>
        /// <param name="onClosed">Appelé à la fermeture, que la disposition ait été validée ou non.</param>
        public static void Show(Transform canvasRoot, UITheme theme, Action onClosed = null)
        {
            if (canvasRoot == null || theme == null) return;
            if (Showing) current.Close();

            current = new ControlLayoutEditor();
            current.Build(canvasRoot, theme, onClosed);
        }

        /// <summary>Referme sans rien enregistrer : retour arrière du téléphone.</summary>
        public static void Cancel()
        {
            if (Showing) current.Close();
        }

        void Build(Transform canvasRoot, UITheme theme, Action onClosed)
        {
            closed = onClosed;
            layout = ControlLayout.Measure();

            var container = UIFactory.CreateUIObject("ControlLayoutEditor", canvasRoot);
            UIFactory.SetRect(container, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            container.SetAsLastSibling();
            root = container.gameObject;

            // Fond opaque jusqu'aux bords de la dalle, qui avale les touchers destinés à l'écran du dessous.
            var backdrop = UIFactory.AddPanel(container, "Backdrop", theme.Background,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            backdrop.raycastTarget = true;

            // Même zone sûre que le HUD : ce qui est posé ici tombe là où le joueur le retrouvera en roulant.
            var area = UIFactory.AddSafeArea(container, "SafeArea").transform;

            BuildToolbar(area, theme);
            BuildGaugeGhost(area);

            if (SettingsManager.Steering == SteeringControl.Joystick)
            {
                AddItem(area, DrivingControl.Joystick);
            }
            else
            {
                AddItem(area, DrivingControl.SteerLeft);
                AddItem(area, DrivingControl.SteerRight);
            }
            AddItem(area, DrivingControl.Throttle);
            AddItem(area, DrivingControl.Lift);
            AddItem(area, DrivingControl.FrontBrake);
            AddItem(area, DrivingControl.RearBrake);
        }

        /// <summary>
        /// Titre, consigne et actions dans la bande du haut, celle que VUE et PAUSE occupent en jeu : les commandes
        /// n'y montent jamais, les boutons de l'éditeur ne gênent donc aucun placement.
        /// </summary>
        void BuildToolbar(Transform area, UITheme theme)
        {
            float margin = layout.Margin;
            float height = layout.S(ControlLayout.TopButtonHeight);
            float width = layout.S(ToolbarButtonWidth);
            float gap = layout.S(ControlLayout.Gap);
            int buttonFont = Mathf.RoundToInt(layout.S(UITheme.FontLabel));
            Vector2 topRight = new Vector2(1f, 1f);

            // De droite à gauche, rentrés du bord comme VUE et PAUSE : l'action principale au plus près du pouce.
            float right = margin + gap;
            UIFactory.AddButton(area, "SaveButton", "VALIDER", theme.Accent, theme.Text, buttonFont, topRight, topRight,
                new Vector2(-right - width, -margin - height), new Vector2(-right, -margin), Save, ButtonKind.Primary);
            right += width + gap;
            UIFactory.AddButton(area, "CancelButton", "ANNULER", theme.PanelAlt, theme.Text, buttonFont, topRight, topRight,
                new Vector2(-right - width, -margin - height), new Vector2(-right, -margin), Close);
            right += width + gap;
            UIFactory.AddButton(area, "ResetButton", "RÉINITIALISER", theme.PanelAlt, theme.TextMuted, buttonFont, topRight, topRight,
                new Vector2(-right - width, -margin - height), new Vector2(-right, -margin), ResetToDefaults);
            right += width + gap;

            float half = height * 0.5f;
            UIFactory.AddText(area, "Title", "DISPOSITION DES COMMANDES", Mathf.RoundToInt(layout.S(UITheme.FontHeading)), theme.Text,
                TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(margin, -margin - half), new Vector2(-right, -margin), FontStyles.Bold);
            UIFactory.AddText(area, "Hint", "Fais glisser chaque commande à sa place.", buttonFont, theme.TextMuted,
                TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(margin, -margin - height), new Vector2(-right, -margin - half));

            // Limite de la bande : aucune commande ne monte au-dessus de ce trait.
            var line = UIFactory.AddPanel(area, "TopBandLine", SeparatorColor, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(margin, -layout.TopBand), new Vector2(-margin, -layout.TopBand + 2f));
            line.raycastTarget = false;
        }

        /// <summary>
        /// Emplacement de la jauge d'angle, en filigrane : elle ne se déplace pas, mais le joueur doit voir ce
        /// qu'il recouvrirait en y posant une commande.
        /// </summary>
        void BuildGaugeGhost(Transform area)
        {
            float left = layout.Margin;
            float right = left + WheelieGauge.Width + 16f;
            float top = layout.GaugeTop;
            float bottom = layout.GaugeBottom(SettingsManager.Steering);

            var ghost = UIFactory.AddPanel(area, "GaugeGhost", GhostColor, new Vector2(0f, 0f), new Vector2(0f, 1f),
                new Vector2(left, bottom), new Vector2(right, -top), rounded: true);
            ghost.raycastTarget = false;

            UIFactory.AddText(area, "GaugeGhostLabel", "JAUGE", Mathf.RoundToInt(layout.S(UITheme.FontLabel)), GhostTextColor,
                TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(right + 12f, -top - 40f), new Vector2(right + 212f, -top), FontStyles.Bold);
        }

        void AddItem(Transform area, DrivingControl control)
        {
            var item = new Item { Control = control, Center = layout.Center(control) };

            item.Background = UIFactory.AddPanel(area, control.ToString(), ItemColor,
                Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            item.Background.raycastTarget = true;
            item.Rect = item.Background.rectTransform;
            layout.Place(item.Rect, control, item.Center);
            UIFactory.ApplyScreenCorners(item.Background);

            UIFactory.AddText(item.Rect, "Label", ControlLayout.Label(control), Mathf.RoundToInt(layout.S(ControlLayout.FontSize(control))),
                ItemTextColor, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, FontStyles.Bold);

            item.Rect.gameObject.AddComponent<ControlLayoutHandle>().Init(
                fraction => Press(item, fraction),
                fraction => Drag(item, fraction),
                () => Release(item));

            items.Add(item);
        }

        void Press(Item item, Vector2 fraction)
        {
            item.PressCenter = item.Center;
            // Le bouton garde sa prise sous le doigt, au lieu de sauter pour se centrer dessus.
            item.GrabOffset = Vector2.Scale(fraction, layout.SafeSize) - item.Center;
            // Tenue au-dessus des autres pendant le glissement.
            item.Rect.SetAsLastSibling();
            item.Background.color = ItemHeldColor;
        }

        void Drag(Item item, Vector2 fraction)
        {
            item.Center = layout.Clamp(item.Control, Vector2.Scale(fraction, layout.SafeSize) - item.GrabOffset);
            layout.Place(item.Rect, item.Control, item.Center);
            // En rouge tant qu'elle chevauche une autre commande : lâchée là, elle reviendrait en arrière.
            item.Background.color = Overlaps(item) ? ItemBlockedColor : ItemHeldColor;
        }

        void Release(Item item)
        {
            if (Overlaps(item))
            {
                item.Center = item.PressCenter;
                layout.Place(item.Rect, item.Control, item.Center);
            }
            else if (item.Center != item.PressCenter)
            {
                item.Moved = true;
            }
            item.Background.color = ItemColor;
        }

        bool Overlaps(Item item)
        {
            Rect rect = layout.RectOf(item.Control, item.Center);
            foreach (var other in items)
            {
                if (other != item && rect.Overlaps(layout.RectOf(other.Control, other.Center))) return true;
            }
            return false;
        }

        /// <summary>Remet les commandes à leur place d'origine à l'écran ; rien n'est effacé avant VALIDER.</summary>
        void ResetToDefaults()
        {
            resetAll = true;
            foreach (var item in items)
            {
                item.Center = layout.DefaultCenter(item.Control);
                item.Moved = false;
                layout.Place(item.Rect, item.Control, item.Center);
                item.Background.color = ItemColor;
            }
        }

        void Save()
        {
            // La réinitialisation vaut aussi pour la direction qui n'est pas affichée : flèches et joystick
            // reviennent tous deux à leur place.
            if (resetAll) SettingsManager.ResetControlPositions();

            // Seules les commandes déplacées sont écrites : une commande laissée à sa place d'origine suit le
            // gabarit, et profitera donc de ses retouches.
            foreach (var item in items)
            {
                if (item.Moved) SettingsManager.SetControlPosition(item.Control, layout.ToFraction(item.Center));
            }
            Close();
        }

        void Close()
        {
            if (current == this) current = null;
            if (root != null)
            {
                // Masqué tout de suite, détruit en fin de frame : un doigt encore posé lâche sa commande avant.
                root.SetActive(false);
                UnityEngine.Object.Destroy(root);
                root = null;
            }
            closed?.Invoke();
        }
    }
}
