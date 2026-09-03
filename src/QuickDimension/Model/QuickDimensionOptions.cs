namespace QuickDimension.Model
{
    /// <summary>Qué elementos entran en la cota.</summary>
    public enum DimensionScope
    {
        /// <summary>Solo lo que la línea atraviesa: caras de muros y columnas.</summary>
        CrossedOnly = 0,

        /// <summary>Además, las aberturas proyectadas sobre la línea (puertas, ventanas y vanos).</summary>
        IncludeOpenings = 1,
    }

    /// <summary>
    /// Opciones de la herramienta. Se recuerdan mientras dure la sesión de Revit
    /// para no volver a rellenarlas en cada cota.
    /// </summary>
    public class QuickDimensionOptions
    {
        /// <summary>Un metro expresado en pies, la unidad interna de Revit.</summary>
        public const double MetersToFeet = 1.0 / 0.3048;

        public DimensionScope Scope { get; set; }

        /// <summary>Jambas de puertas y ventanas, contornos de vanos y extremos de muro.</summary>
        public bool OpeningEdges { get; set; }

        /// <summary>Ejes (centros) de puertas y ventanas.</summary>
        public bool OpeningCenters { get; set; }

        /// <summary>Incluir columnas arquitectónicas y estructurales atravesadas.</summary>
        public bool IncludeColumns { get; set; }

        /// <summary>Añadir el eje del muro además de sus caras.</summary>
        public bool WallCenterlines { get; set; }

        /// <summary>Desplazamiento perpendicular de la cota respecto a la línea trazada, en metros.</summary>
        public double OffsetMeters { get; set; }

        /// <summary>Holgura de búsqueda alrededor de la línea, en metros.</summary>
        public double SearchToleranceMeters { get; set; }

        public QuickDimensionOptions()
        {
            Scope = DimensionScope.CrossedOnly;
            OpeningEdges = true;
            OpeningCenters = false;
            IncludeColumns = true;
            WallCenterlines = false;
            OffsetMeters = 0.0;
            SearchToleranceMeters = 0.05;
        }

        public QuickDimensionOptions Clone()
        {
            return new QuickDimensionOptions
            {
                Scope = Scope,
                OpeningEdges = OpeningEdges,
                OpeningCenters = OpeningCenters,
                IncludeColumns = IncludeColumns,
                WallCenterlines = WallCenterlines,
                OffsetMeters = OffsetMeters,
                SearchToleranceMeters = SearchToleranceMeters,
            };
        }

        public double OffsetFeet { get { return OffsetMeters * MetersToFeet; } }

        public double SearchToleranceFeet { get { return SearchToleranceMeters * MetersToFeet; } }
    }
}
