using UnityEngine;
using UnityEngine.UI;
using WheelingMoto.Gameplay;

namespace WheelingMoto.UI
{
    /// <summary>
    /// Icône 3D d'un lot de coffre (pile de pièces, moteur, amortisseur, roue, treillis, bombe de
    /// peinture), posée sur la carte de gain et tournant lentement sur elle-même.
    /// Les rigs sont recyclés d'une ouverture à l'autre via <see cref="Rebind"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public class RewardIcon : ModelPreviewRig
    {
        /// <summary>Matériau de la bombe de peinture, teinté avec la couleur réellement gagnée.</summary>
        const string TintableMaterial = "PaintBody";

        public float spinSpeed = 38f;

        GameObject model;

        public static RewardIcon Create(RawImage output)
        {
            var go = new GameObject("RewardIcon");
            var icon = go.AddComponent<RewardIcon>();
            icon.SetupRig(output, AllocateRigPosition());
            return icon;
        }

        /// <summary>Affiche un modèle et le rebranche sur cette carte. Retourne false s'il est absent.</summary>
        public bool Show(RawImage output, string resourcePath, Color tint)
        {
            Rebind(output);

            if (model != null) Destroy(model);

            model = LoadModel(resourcePath, warnIfMissing: false);
            if (model == null)
            {
                this.output.enabled = false;
                return false;
            }

            MotoPainter.Tint(model, TintableMaterial, tint);
            Frame(model, heightFactor: 0.16f, margin: 1.12f);
            stage.localRotation = Quaternion.identity;

            this.output.enabled = true;
            return true;
        }

        public void Hide()
        {
            if (model != null) Destroy(model);
            model = null;
            if (output != null) output.enabled = false;
        }

        protected override void OnPreviewUpdate()
        {
            if (model == null) return;

            stage.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.Self);
        }
    }
}
