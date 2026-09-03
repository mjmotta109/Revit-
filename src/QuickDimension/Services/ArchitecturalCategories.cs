using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace QuickDimension.Services
{
    /// <summary>
    /// Qué se considera "arquitectónico" a efectos de acotar.
    /// </summary>
    /// <remarks>
    /// Es una lista blanca a propósito: el mobiliario y el equipamiento (FFE) quedan fuera
    /// por construcción, no por una lista negra que haya que ir ampliando. Mesas, sillas,
    /// armarios, sanitarios, luminarias, equipos mecánicos y demás nunca entran en la cota
    /// aunque la línea los atraviese.
    /// </remarks>
    internal static class ArchitecturalCategories
    {
        /// <summary>Elementos verticales que la línea puede atravesar.</summary>
        public static IList<BuiltInCategory> Crossable(bool includeColumns)
        {
            var categories = new List<BuiltInCategory> { BuiltInCategory.OST_Walls };

            if (includeColumns)
            {
                categories.Add(BuiltInCategory.OST_Columns);
                categories.Add(BuiltInCategory.OST_StructuralColumns);
            }

            return categories;
        }

        /// <summary>Aberturas que se proyectan sobre la línea de cota.</summary>
        public static IList<BuiltInCategory> Openings()
        {
            return new List<BuiltInCategory>
            {
                BuiltInCategory.OST_Doors,
                BuiltInCategory.OST_Windows,
                BuiltInCategory.OST_SWallRectOpening,
                BuiltInCategory.OST_ArcWallRectOpening,
            };
        }
    }
}
