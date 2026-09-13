using WheelingMoto.Core;

namespace WheelingMoto.Data
{
    public class MapInfo
    {
        public readonly MapId Id;
        public readonly string Name;
        public readonly string Description;

        /// <summary>Prix en pièces. Zéro pour la carte de départ, la seule ouverte d'office.</summary>
        public readonly int Price;

        public MapInfo(MapId id, string name, string description, int price)
        {
            Id = id;
            Name = name;
            Description = description;
            Price = price;
        }

        public bool UnlockedByDefault => Price <= 0;

        /// <summary>
        /// Maquette 3D montrée dans le carrousel du menu Jouer, sous Assets/Resources. Aucune n'est
        /// encore fournie : le carrousel retombe alors sur le nom de la carte.
        /// </summary>
        public string ModelResourcePath => "Maps/" + Id;
    }

    /// <summary>
    /// Les trois cartes du GDD. Seule la Métropole est ouverte au départ ; les deux autres
    /// s'achètent avec les pièces gagnées en mission.
    ///
    /// Les prix sont calés sur ce que rapporte le jeu : autour de 7 000 pièces par semaine pour un
    /// joueur régulier. La Montagne tombe donc en une dizaine de jours et la Ville Côtière dans le
    /// mois, à condition de ne pas tout dépenser au garage — c'est le but, une carte doit être un
    /// cap, pas une formalité.
    /// </summary>
    public static class MapCatalog
    {
        public static readonly MapInfo[] All =
        {
            new MapInfo(MapId.Metropole, "Métropole Dense", "Trafic et architecture denses, inspiration New York.", 0),
            new MapInfo(MapId.Montagne, "Montagne", "Routes sinueuses et dénivelé, tracés techniques.", 12000),
            new MapInfo(MapId.CoteAzur, "Ville Côtière", "Bord de mer aéré, longues lignes droites.", 25000),
        };

        public static MapInfo Find(MapId id)
        {
            foreach (var map in All)
            {
                if (map.Id == id) return map;
            }
            return All[0];
        }

        /// <summary>Carte de départ, celle sur laquelle on retombe quand la carte choisie est verrouillée.</summary>
        public static MapInfo Default
        {
            get
            {
                foreach (var map in All)
                {
                    if (map.UnlockedByDefault) return map;
                }
                return All[0];
            }
        }
    }
}
