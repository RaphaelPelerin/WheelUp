using System;
using UnityEngine;

namespace WheelingMoto.Gameplay
{
    /// <summary>
    /// Gréement d'un modèle de moto : ce que le code ne devine pas seul dans le maillage (pièces qui braquent,
    /// roues, selle, poignées, taille réelle). Les points sont relevés une fois dans le FBX, dans l'espace du
    /// prefab (mètres, +Z vers l'avant, +X à droite), avant la mise à l'échelle Scale.
    /// </summary>
    public sealed class MotoRig
    {
        public string Name;
        /// <summary>Agrandissement du modèle pour qu'il ait la taille d'une vraie moto face au pilote.</summary>
        public float Scale = 1f;
        /// <summary>Contacts des pneus au sol.</summary>
        public Vector3 RearContact;
        public Vector3 FrontContact;
        /// <summary>Haut de la colonne de direction, et son inclinaison vers l'arrière (chasse), en degrés.</summary>
        public Vector3 SteeringPivot;
        public float SteeringRake;
        /// <summary>Suffixes des pièces qui tournent avec le guidon (fourche, roue avant, phare...).</summary>
        public string[] SteeringParts;
        public string FrontWheel;
        public string RearWheel;
        public float WheelRadius;
        /// <summary>Assise du pilote, sur le dessus de la selle.</summary>
        public Vector3 SeatContact;
        /// <summary>Centre de la poignée droite ; la gauche est sa symétrique.</summary>
        public Vector3 RightGrip;
        public float GripRadius;
        /// <summary>Pièces techniques du pack à masquer.</summary>
        public string[] HiddenParts;
        /// <summary>Buste penché d'origine (posture), en degrés.</summary>
        public float RiderForwardLean;
        /// <summary>Levier d'embrayage à gauche : le pilote le couvre de deux doigts, comme le frein à droite.</summary>
        public bool HasClutchLever;
        /// <summary>Optiques : phare, feu arrière et clignotants.</summary>
        public string HeadlightPart;
        public string TailLightPart;
        public string IndicatorLeftPart;
        public string IndicatorRightPart;
    }

    /// <summary>Gréements connus, retrouvés d'après le nom du prefab.</summary>
    public static class MotoRigs
    {
        /// <summary>
        /// Roadster « naked » du Motorcycle Pack Naked Chopper Police (SuperMotors 1 à 6 : un même modèle en six
        /// coloris). Le modèle est petit (1,11 m d'empattement) : agrandi de 1,27 pour un vrai roadster (1,41 m),
        /// avec une selle à 0,78 m et un réservoir à 0,94 m du sol.
        /// Pièces : roues _001 (arrière) et _003 (avant), fourche _016 inclinée de 24°, guidon _017, garde-boue
        /// _013, bloc phare _014 et _018, rétroviseurs _019 / _020. _007 et _008 sont les volumes de collision du pack.
        /// </summary>
        public static readonly MotoRig SuperMotorsNaked = new MotoRig
        {
            Name = "Roadster SuperMotors",
            Scale = 1.27f,
            RearContact = new Vector3(0f, -0.432f, -0.528f),
            FrontContact = new Vector3(0f, -0.432f, 0.58f),
            SteeringPivot = new Vector3(0f, 0.26f, 0.365f),
            SteeringRake = 24f,
            SteeringParts = new[] { "_003", "_013", "_014", "_016", "_017", "_018", "_019", "_020" },
            FrontWheel = "_003",
            RearWheel = "_001",
            WheelRadius = 0.231f,
            SeatContact = new Vector3(0f, 0.17f, -0.28f),
            // Partie caoutchouc de x = 0,22 à 0,26, droite ; les leviers sont 7 cm devant.
            RightGrip = new Vector3(0.24f, 0.235f, 0.262f),
            GripRadius = 0.0145f,
            HiddenParts = new[] { "_007_crash_collider_", "_008_crash_collider_" },
            RiderForwardLean = 25f,
            HasClutchLever = true,
            // Optiques (matériau pc-2) : cabochon du phare, feu arrière, clignotants gauche (x < 0) et droit.
            HeadlightPart = "_018",
            TailLightPart = "_006",
            IndicatorLeftPart = "_005",
            IndicatorRightPart = "_004",
        };

        public static MotoRig Find(GameObject prefab)
        {
            if (prefab == null) return null;
            for (int i = 1; i <= 6; i++)
            {
                if (prefab.name.StartsWith("SuperMotors_" + i + "_", StringComparison.Ordinal)) return SuperMotorsNaked;
            }
            return null;
        }

        /// <summary>Pièce du modèle dont le nom se termine par le suffixe du gréement (ex. « _017 »).</summary>
        public static Transform Part(Transform model, string suffix)
        {
            foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
            {
                if (t != model && t.name.EndsWith(suffix, StringComparison.Ordinal)) return t;
            }
            return null;
        }
    }
}
