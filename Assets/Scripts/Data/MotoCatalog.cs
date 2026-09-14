namespace WheelingMoto.Data
{
    public class MotoInfo
    {
        public readonly string Name;
        public readonly string Category;
        public readonly int Price;
        public readonly bool OwnedByDefault;

        /// <summary>Chemin du modèle sous Assets/Resources (sans extension), ou null si le modèle 3D manque encore.</summary>
        public readonly string ModelResourcePath;

        /// <summary>Fiche technique d'origine, avant améliorations. Voir MotoStats pour le calcul final.</summary>
        public readonly MotoSpecs Specs;

        public MotoInfo(string name, string category, int price, MotoSpecs specs,
            bool ownedByDefault = false, string modelResourcePath = null)
        {
            Name = name;
            Category = category;
            Price = price;
            Specs = specs;
            OwnedByDefault = ownedByDefault;
            ModelResourcePath = modelResourcePath;
        }
    }

    /// <summary>
    /// Catalogue des motos, du 50cc d'entrée à l'hypersport. Chaque fiche reprend la vraie moto, chiffres
    /// arrondis ; le point d'équilibre suit la géométrie (centre de gravité haut et empattement court :
    /// il arrive tôt, zone large ; sportive basse et longue : il arrive tard, zone étroite).
    /// </summary>
    public static class MotoCatalog
    {
        public static readonly MotoInfo[] All =
        {
            // Mécaboite : palier d'entrée. Fiches des versions débridées, celles du wheeling de rue ;
            // bridées, elles plafonneraient à 45 km/h.
            new MotoInfo("Beta RR 50 Motard", "Mécaboite", 0, new MotoSpecs
            {
                PowerHp = 9f, PeakPowerRpm = 10500f, RedlineRpm = 11500f, WeightKg = 95f, TopSpeedKmh = 85f,
                Gears = 6, FirstGearKmh = 30f, BrakingMps2 = 8f, LaunchGrip = 7f,
                BalanceAngle = 40f, BalanceWidth = 8.2f, Handling = 7f,
            }, true),
            new MotoInfo("Derbi Senda X-Treme", "Mécaboite", 900, new MotoSpecs
            {
                PowerHp = 8.5f, PeakPowerRpm = 10000f, RedlineRpm = 11000f, WeightKg = 98f, TopSpeedKmh = 80f,
                Gears = 6, FirstGearKmh = 28f, BrakingMps2 = 7.8f, LaunchGrip = 7f,
                BalanceAngle = 40f, BalanceWidth = 8.2f, Handling = 7f,
            }),
            new MotoInfo("Rieju MRT 50", "Mécaboite", 1400, new MotoSpecs
            {
                PowerHp = 8f, PeakPowerRpm = 10000f, RedlineRpm = 11000f, WeightKg = 94f, TopSpeedKmh = 80f,
                Gears = 6, FirstGearKmh = 28f, BrakingMps2 = 7.8f, LaunchGrip = 7f,
                BalanceAngle = 40f, BalanceWidth = 8.2f, Handling = 6f,
            }),

            // 125.
            new MotoInfo("Yamaha MT-125", "125", 1800, new MotoSpecs
            {
                PowerHp = 15f, PeakPowerRpm = 10000f, RedlineRpm = 11500f, WeightKg = 142f, TopSpeedKmh = 120f,
                Gears = 6, FirstGearKmh = 45f, BrakingMps2 = 8.5f, LaunchGrip = 7.5f,
                BalanceAngle = 42f, BalanceWidth = 8.9f, Handling = 7f,
            }),
            new MotoInfo("KTM 125 Duke", "125", 2200, new MotoSpecs
            {
                PowerHp = 15f, PeakPowerRpm = 10000f, RedlineRpm = 11000f, WeightKg = 147f, TopSpeedKmh = 118f,
                Gears = 6, FirstGearKmh = 45f, BrakingMps2 = 8.8f, LaunchGrip = 7.5f,
                BalanceAngle = 42f, BalanceWidth = 8.9f, Handling = 8f,
            }),
            new MotoInfo("KTM 125 EXC", "Supermotard", 2400, new MotoSpecs
            {
                // Deux-temps : léger et pointu, tout se passe en haut du compte-tours.
                PowerHp = 30f, PeakPowerRpm = 11000f, RedlineRpm = 12000f, WeightKg = 101f, TopSpeedKmh = 120f,
                Gears = 6, FirstGearKmh = 45f, BrakingMps2 = 8.5f, LaunchGrip = 8f,
                BalanceAngle = 40f, BalanceWidth = 9.6f, Handling = 8f,
            }),
            new MotoInfo("Honda MSX125 Grom", "125", 2600, new MotoSpecs
            {
                // Petites roues et empattement de poche : le point d'équilibre arrive très tôt.
                PowerHp = 9.8f, PeakPowerRpm = 7250f, RedlineRpm = 8500f, WeightKg = 103f, TopSpeedKmh = 95f,
                Gears = 5, FirstGearKmh = 40f, BrakingMps2 = 8f, LaunchGrip = 7f,
                BalanceAngle = 38f, BalanceWidth = 8.9f, Handling = 8f,
            }),
            new MotoInfo("Aprilia RS 125", "125", 3100, new MotoSpecs
            {
                PowerHp = 15f, PeakPowerRpm = 10500f, RedlineRpm = 11500f, WeightKg = 144f, TopSpeedKmh = 130f,
                Gears = 6, FirstGearKmh = 50f, BrakingMps2 = 9f, LaunchGrip = 7.5f,
                BalanceAngle = 44f, BalanceWidth = 7.5f, Handling = 7f,
            }),

            // Roadsters : le cœur du wheeling.
            new MotoInfo("Yamaha MT-07", "Roadster", 3800, new MotoSpecs
            {
                PowerHp = 73f, PeakPowerRpm = 8750f, RedlineRpm = 10000f, WeightKg = 184f, TopSpeedKmh = 214f,
                Gears = 6, FirstGearKmh = 85f, BrakingMps2 = 9.5f, LaunchGrip = 9f,
                BalanceAngle = 44f, BalanceWidth = 10.3f, Handling = 7f,
            }),
            new MotoInfo("Kawasaki Z650", "Roadster", 4300, new MotoSpecs
            {
                PowerHp = 68f, PeakPowerRpm = 8000f, RedlineRpm = 10000f, WeightKg = 187f, TopSpeedKmh = 200f,
                Gears = 6, FirstGearKmh = 80f, BrakingMps2 = 9.3f, LaunchGrip = 9f,
                BalanceAngle = 44f, BalanceWidth = 8.9f, Handling = 7f,
            }),
            new MotoInfo("Yamaha MT-09", "Roadster", 5600, new MotoSpecs
            {
                PowerHp = 119f, PeakPowerRpm = 10000f, RedlineRpm = 11000f, WeightKg = 189f, TopSpeedKmh = 230f,
                Gears = 6, FirstGearKmh = 95f, BrakingMps2 = 9.8f, LaunchGrip = 9.3f,
                BalanceAngle = 45f, BalanceWidth = 10.3f, Handling = 7f,
            }),
            new MotoInfo("Kawasaki Z900", "Roadster", 6400, new MotoSpecs
            {
                PowerHp = 125f, PeakPowerRpm = 9500f, RedlineRpm = 11000f, WeightKg = 212f, TopSpeedKmh = 240f,
                Gears = 6, FirstGearKmh = 100f, BrakingMps2 = 9.6f, LaunchGrip = 9.3f,
                BalanceAngle = 46f, BalanceWidth = 9.6f, Handling = 6f,
            }),
            new MotoInfo("Triumph Street Triple 765", "Roadster", 6900, new MotoSpecs
            {
                PowerHp = 123f, PeakPowerRpm = 11750f, RedlineRpm = 12500f, WeightKg = 188f, TopSpeedKmh = 250f,
                Gears = 6, FirstGearKmh = 105f, BrakingMps2 = 10.2f, LaunchGrip = 9.3f,
                BalanceAngle = 46f, BalanceWidth = 9.6f, Handling = 8f,
            }),

            // Supermotards : centre de gravité haut, grand débattement, maniabilité de vélo.
            new MotoInfo("Husqvarna 701 Supermoto", "Supermotard", 3500, new MotoSpecs
            {
                PowerHp = 74f, PeakPowerRpm = 8000f, RedlineRpm = 8800f, WeightKg = 165f, TopSpeedKmh = 190f,
                Gears = 6, FirstGearKmh = 75f, BrakingMps2 = 9.3f, LaunchGrip = 8.5f,
                BalanceAngle = 41f, BalanceWidth = 9.6f, Handling = 9f,
            }),
            new MotoInfo("KTM 690 SMC R", "Supermotard", 4800, new MotoSpecs
            {
                PowerHp = 74f, PeakPowerRpm = 8000f, RedlineRpm = 9000f, WeightKg = 157f, TopSpeedKmh = 185f,
                Gears = 6, FirstGearKmh = 72f, BrakingMps2 = 9.3f, LaunchGrip = 8.5f,
                BalanceAngle = 41f, BalanceWidth = 10.3f, Handling = 9f,
            }),
            new MotoInfo("Yamaha WR450F Supermoté", "Supermotard", 5200, new MotoSpecs
            {
                PowerHp = 55f, PeakPowerRpm = 9500f, RedlineRpm = 12000f, WeightKg = 123f, TopSpeedKmh = 150f,
                Gears = 5, FirstGearKmh = 60f, BrakingMps2 = 9f, LaunchGrip = 8.5f,
                BalanceAngle = 40f, BalanceWidth = 10.3f, Handling = 9f,
            }),
            new MotoInfo("Ducati Hypermotard 950", "Supermotard", 7400, new MotoSpecs
            {
                PowerHp = 114f, PeakPowerRpm = 9000f, RedlineRpm = 10500f, WeightKg = 200f, TopSpeedKmh = 225f,
                Gears = 6, FirstGearKmh = 90f, BrakingMps2 = 9.8f, LaunchGrip = 9f,
                BalanceAngle = 43f, BalanceWidth = 9.6f, Handling = 8f,
            }),

            // Sportives : basses, longues et rapides, le point d'équilibre arrive tard et se tient au degré près.
            new MotoInfo("Yamaha YZF-R6", "Sportive", 6200, new MotoSpecs
            {
                PowerHp = 118f, PeakPowerRpm = 14500f, RedlineRpm = 16000f, WeightKg = 190f, TopSpeedKmh = 260f,
                Gears = 6, FirstGearKmh = 110f, BrakingMps2 = 10.3f, LaunchGrip = 9.8f,
                BalanceAngle = 50f, BalanceWidth = 7.5f, Handling = 6f,
            }),
            new MotoInfo("Honda CBR600RR", "Sportive", 6800, new MotoSpecs
            {
                PowerHp = 119f, PeakPowerRpm = 14000f, RedlineRpm = 15000f, WeightKg = 193f, TopSpeedKmh = 255f,
                Gears = 6, FirstGearKmh = 110f, BrakingMps2 = 10.3f, LaunchGrip = 9.8f,
                BalanceAngle = 50f, BalanceWidth = 7.5f, Handling = 6f,
            }),
            // Les litres sont bridées électroniquement à 299 km/h.
            new MotoInfo("Kawasaki Ninja ZX-10R", "Sportive", 8900, new MotoSpecs
            {
                PowerHp = 203f, PeakPowerRpm = 13200f, RedlineRpm = 14000f, WeightKg = 207f, TopSpeedKmh = 299f,
                Gears = 6, FirstGearKmh = 145f, BrakingMps2 = 10.5f, LaunchGrip = 9.8f,
                BalanceAngle = 51f, BalanceWidth = 8.2f, Handling = 6f,
            }),
            new MotoInfo("Yamaha YZF-R1", "Sportive", 9800, new MotoSpecs
            {
                PowerHp = 200f, PeakPowerRpm = 13500f, RedlineRpm = 14500f, WeightKg = 201f, TopSpeedKmh = 299f,
                Gears = 6, FirstGearKmh = 150f, BrakingMps2 = 10.5f, LaunchGrip = 9.8f,
                BalanceAngle = 51f, BalanceWidth = 8.2f, Handling = 6f,
            }),
            new MotoInfo("BMW S1000RR", "Sportive", 11000, new MotoSpecs
            {
                PowerHp = 207f, PeakPowerRpm = 13500f, RedlineRpm = 14600f, WeightKg = 197f, TopSpeedKmh = 299f,
                Gears = 6, FirstGearKmh = 140f, BrakingMps2 = 10.6f, LaunchGrip = 9.8f,
                BalanceAngle = 51f, BalanceWidth = 8.2f, Handling = 6f,
            }),

            // Hypersport. Offerte pour l'instant : c'est le seul modèle 3D disponible. Une fois les autres
            // modèles intégrés, retirer OwnedByDefault pour qu'elle redevienne la récompense finale.
            // Compresseur : 231 ch avec l'admission forcée, mais 238 kg et la maniabilité d'une routière.
            new MotoInfo("Kawasaki Ninja H2", "Hypersport", 15000, new MotoSpecs
            {
                PowerHp = 231f, PeakPowerRpm = 11000f, RedlineRpm = 12500f, WeightKg = 238f, TopSpeedKmh = 320f,
                Gears = 6, FirstGearKmh = 140f, BrakingMps2 = 10.2f, LaunchGrip = 9.5f,
                BalanceAngle = 48f, BalanceWidth = 8.9f, Handling = 5f,
            }, true, "Motos/KawasakiNinjaH2"),
        };

        /// <summary>Moto de départ : la première du catalogue possédée d'office.</summary>
        public static MotoInfo Default
        {
            get
            {
                foreach (var moto in All)
                {
                    if (moto.OwnedByDefault && !string.IsNullOrEmpty(moto.ModelResourcePath)) return moto;
                }
                return All[0];
            }
        }

        public static MotoInfo Find(string name)
        {
            return System.Array.Find(All, m => m.Name == name);
        }
    }
}
