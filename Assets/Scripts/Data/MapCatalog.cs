using WheelingMoto.Core;

namespace WheelingMoto.Data
{
    public class MapInfo
    {
        public readonly MapId Id;
        public readonly string Name;
        public readonly string Description;

        public MapInfo(MapId id, string name, string description)
        {
            Id = id;
            Name = name;
            Description = description;
        }
    }

    /// <summary>Les trois cartes du GDD.</summary>
    public static class MapCatalog
    {
        public static readonly MapInfo[] All =
        {
            new MapInfo(MapId.Metropole, "Métropole Dense", "Trafic et architecture denses, inspiration New York."),
            new MapInfo(MapId.Montagne, "Montagne", "Routes sinueuses et dénivelé, tracés techniques."),
            new MapInfo(MapId.CoteAzur, "Ville Côtière", "Bord de mer aéré, longues lignes droites."),
        };
    }
}
