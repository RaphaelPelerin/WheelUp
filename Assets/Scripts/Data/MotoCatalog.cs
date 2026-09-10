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

        /// <summary>Statistiques de base sur 10, avant améliorations. Voir MotoStats pour le calcul final.</summary>
        public readonly int Power;
        public readonly int Handling;
        public readonly int Wheelie;

        public MotoInfo(string name, string category, int price, int power, int handling, int wheelie,
            bool ownedByDefault = false, string modelResourcePath = null)
        {
            Name = name;
            Category = category;
            Price = price;
            Power = power;
            Handling = handling;
            Wheelie = wheelie;
            OwnedByDefault = ownedByDefault;
            ModelResourcePath = modelResourcePath;
        }
    }

    /// <summary>Catalogue des motos, du 50cc d'entrée à l'hypersport.</summary>
    public static class MotoCatalog
    {
        public static readonly MotoInfo[] All =
        {
            // Mécaboite : palier d'entrée.
            new MotoInfo("Beta RR 50 Motard", "Mécaboite", 0, 2, 7, 6, true),
            new MotoInfo("Derbi Senda X-Treme", "Mécaboite", 900, 2, 7, 6),
            new MotoInfo("Rieju MRT 50", "Mécaboite", 1400, 2, 6, 6),

            // 125.
            new MotoInfo("Yamaha MT-125", "125", 1800, 3, 7, 7),
            new MotoInfo("KTM 125 Duke", "125", 2200, 3, 8, 7),
            new MotoInfo("KTM 125 EXC", "Supermotard", 2400, 3, 8, 8),
            new MotoInfo("Honda MSX125 Grom", "125", 2600, 2, 8, 7),
            new MotoInfo("Aprilia RS 125", "125", 3100, 4, 7, 5),

            // Roadsters : le cœur du wheeling.
            new MotoInfo("Yamaha MT-07", "Roadster", 3800, 6, 7, 9),
            new MotoInfo("Kawasaki Z650", "Roadster", 4300, 6, 7, 7),
            new MotoInfo("Yamaha MT-09", "Roadster", 5600, 7, 7, 9),
            new MotoInfo("Kawasaki Z900", "Roadster", 6400, 8, 6, 8),
            new MotoInfo("Triumph Street Triple 765", "Roadster", 6900, 7, 8, 8),

            // Supermotards.
            new MotoInfo("Husqvarna 701 Supermoto", "Supermotard", 3500, 5, 9, 8),
            new MotoInfo("KTM 690 SMC R", "Supermotard", 4800, 5, 9, 9),
            new MotoInfo("Yamaha WR450F Supermoté", "Supermotard", 5200, 4, 9, 9),
            new MotoInfo("Ducati Hypermotard 950", "Supermotard", 7400, 6, 8, 8),

            // Sportives.
            new MotoInfo("Yamaha YZF-R6", "Sportive", 6200, 8, 6, 5),
            new MotoInfo("Honda CBR600RR", "Sportive", 6800, 8, 6, 5),
            new MotoInfo("Kawasaki Ninja ZX-10R", "Sportive", 8900, 9, 6, 6),
            new MotoInfo("Yamaha YZF-R1", "Sportive", 9800, 9, 6, 6),
            new MotoInfo("BMW S1000RR", "Sportive", 11000, 10, 6, 6),

            // Hypersport. Offerte pour l'instant : c'est le seul modèle 3D disponible. Une fois les autres
            // modèles intégrés, retirer OwnedByDefault pour qu'elle redevienne la récompense finale.
            new MotoInfo("Kawasaki Ninja H2", "Hypersport", 15000, 10, 5, 7, true, "Motos/KawasakiNinjaH2"),
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
