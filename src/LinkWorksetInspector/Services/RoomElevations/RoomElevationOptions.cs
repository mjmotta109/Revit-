using Autodesk.Revit.DB;

namespace LinkWorksetInspector.Services.RoomElevations
{
    /// <summary>De qué habitaciones se generan alzados.</summary>
    public enum RoomScope
    {
        AllInModel,
        ActiveView,
        CurrentSelection,
    }

    /// <summary>Parámetros de generación de alzados interiores por habitación.</summary>
    public class RoomElevationOptions
    {
        public RoomScope Scope { get; set; } = RoomScope.ActiveView;

        /// <summary>Tipo de vista de alzado (ViewFamilyType con ViewFamily.Elevation).</summary>
        public ElementId ViewFamilyTypeId { get; set; } = ElementId.InvalidElementId;

        /// <summary>Plantilla de vista opcional. InvalidElementId = ninguna.</summary>
        public ElementId ViewTemplateId { get; set; } = ElementId.InvalidElementId;

        public int Scale { get; set; } = 50;

        /// <summary>Margen a izquierda y derecha del recorte, en milímetros.</summary>
        public double HorizontalMarginMm { get; set; } = 150;

        /// <summary>Margen por debajo del piso y por encima del cielo raso, en milímetros.</summary>
        public double VerticalMarginMm { get; set; } = 50;

        /// <summary>Cuánto se extiende el corte lejano más allá de la cara del muro, en milímetros.</summary>
        public double BeyondWallMm { get; set; } = 250;

        /// <summary>Se ignoran los tramos de borde más cortos que esto (jambas, quiebres), en milímetros.</summary>
        public double MinWallLengthMm { get; set; } = 400;

        /// <summary>Altura usada cuando no se encuentra cielo raso ni losa por encima, en milímetros.</summary>
        public double DefaultHeightMm { get; set; } = 2700;

        /// <summary>Altura máxima en la que se busca un cielo raso o losa por encima, en milímetros.</summary>
        public double MaxSearchHeightMm { get; set; } = 10000;

        /// <summary>Dos muros se consideran del mismo alzado si sus direcciones difieren menos de esto.</summary>
        public double DirectionToleranceDeg { get; set; } = 5;

        public bool UseCeilings { get; set; } = true;
        public bool UseSlabs { get; set; } = true;

        /// <summary>Omitir habitaciones que ya tienen alzados con este mismo patrón de nombre.</summary>
        public bool SkipRoomsWithElevations { get; set; } = true;

        /// <summary>Admite {numero}, {nombre} y {direccion}.</summary>
        public string NamePattern { get; set; } = "{numero} {nombre} - {direccion}";
    }
}
