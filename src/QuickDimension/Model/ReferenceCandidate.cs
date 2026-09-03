using Autodesk.Revit.DB;

namespace QuickDimension.Model
{
    /// <summary>
    /// Fiabilidad de una referencia. Si Revit rechaza la cota, el comando vuelve a
    /// intentarlo quedándose solo con los niveles más bajos (más seguros).
    /// </summary>
    public enum ReferenceTier
    {
        /// <summary>Caras laterales de muro y caras de columna. Es lo que siempre funciona.</summary>
        Solid = 0,

        /// <summary>Caras obtenidas recorriendo la geometría: extremos de muro y jambas.</summary>
        Geometry = 1,

        /// <summary>Planos de referencia de familia y ejes de muro.</summary>
        Plane = 2,
    }

    /// <summary>Una referencia candidata a formar parte de la cota, con lo necesario para ordenarla y filtrarla.</summary>
    public class ReferenceCandidate
    {
        public Reference Reference { get; set; }

        /// <summary>
        /// Normal unitaria del plano de la referencia, ya orientada hacia el sentido de la línea.
        /// Es <c>null</c> en referencias lineales (el eje de un muro), que no tienen normal propia.
        /// </summary>
        public XYZ Normal { get; set; }

        /// <summary>Dirección de la referencia lineal. Solo se usa cuando <see cref="Normal"/> es <c>null</c>.</summary>
        public XYZ LineDirection { get; set; }

        /// <summary>Un punto cualquiera de la referencia, para proyectarlo sobre la línea de cota.</summary>
        public XYZ Point { get; set; }

        public ReferenceTier Tier { get; set; }

        /// <summary>Descripción legible del origen, para los avisos al usuario.</summary>
        public string Source { get; set; }

        /// <summary>Posición sobre la línea de cota. La calcula el motor al cerrar el plan.</summary>
        public double Position { get; set; }
    }
}
