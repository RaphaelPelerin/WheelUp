namespace WheelingMoto.Data
{
    /// <summary>
    /// Fiche technique d'une moto, prise sur la vraie (chiffres constructeur ou d'essais, arrondis) : c'est elle
    /// que la physique de conduite applique, et elle que le garage affiche. Les améliorations s'y ajoutent
    /// ensuite (voir <see cref="MotoStats"/>).
    /// </summary>
    public sealed class MotoSpecs
    {
        /// <summary>Puissance maximale, en chevaux.</summary>
        public float PowerHp;
        /// <summary>Régime de la puissance maximale, en tr/min.</summary>
        public float PeakPowerRpm;
        /// <summary>Zone rouge : le limiteur coupe au-delà, en tr/min.</summary>
        public float RedlineRpm;

        /// <summary>Poids tous pleins faits, sans pilote, en kg.</summary>
        public float WeightKg;
        /// <summary>Vitesse de pointe, en km/h : atteinte en dernier rapport, au limiteur.</summary>
        public float TopSpeedKmh;

        /// <summary>Nombre de rapports de la boîte.</summary>
        public int Gears;
        /// <summary>Vitesse atteinte en première à la zone rouge, en km/h : fixe l'étagement de la boîte.</summary>
        public float FirstGearKmh;

        /// <summary>Décélération au freinage appuyé, en m/s² (ABS, pneus et empattement compris).</summary>
        public float BrakingMps2;
        /// <summary>
        /// Accélération maximale au démarrage, en m/s² : l'adhérence du pneu arrière et le cabrage bornent ce
        /// que le moteur peut transmettre, tant que la moto n'est pas lancée.
        /// </summary>
        public float LaunchGrip;

        /// <summary>Point d'équilibre du wheeling : angle où la moto tient seule, en degrés.</summary>
        public float BalanceAngle;
        /// <summary>Largeur de la zone d'équilibre autour de ce point, en degrés : plus large, plus facile à tenir.</summary>
        public float BalanceWidth;

        /// <summary>Maniabilité sur 10 : vivacité de la mise sur l'angle et angle maximal.</summary>
        public float Handling;
    }
}
