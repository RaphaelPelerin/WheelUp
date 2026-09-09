namespace WheelingMoto.Data
{
    public class MotoInfo
    {
        public readonly string Name;
        public readonly string Category;
        public readonly int Price;
        public readonly bool OwnedByDefault;

        public MotoInfo(string name, string category, int price, bool ownedByDefault = false)
        {
            Name = name;
            Category = category;
            Price = price;
            OwnedByDefault = ownedByDefault;
        }
    }

    /// <summary>Catalogue de motos de départ (placeholder de contenu, à étoffer plus tard).</summary>
    public static class MotoCatalog
    {
        public static readonly MotoInfo[] All =
        {
            new MotoInfo("KTM 125 EXC", "Supermotard", 0, true),
            new MotoInfo("Husqvarna 701", "Supermotard", 3500),
            new MotoInfo("Ducati Hypermotard", "Supermotard", 7400),
            new MotoInfo("Yamaha YZF-R6", "Sportive", 6200),
            new MotoInfo("Kawasaki Ninja ZX-10R", "Sportive", 8900),
        };
    }
}
