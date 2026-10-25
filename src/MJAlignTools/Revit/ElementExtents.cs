using Autodesk.Revit.DB;
using MJAlignTools.Core;

namespace MJAlignTools.Revit
{
    /// <summary>
    /// Mide el rectángulo que ocupa un elemento en la vista.
    ///
    /// Se usa la geometría visible en la vista (sólidos y líneas simbólicas), no la
    /// caja de Revit (<c>get_BoundingBox</c>): esa caja siempre va alineada a los ejes
    /// del modelo y, según la documentación de la API, puede incluir cosas invisibles
    /// como los controles de giro de la familia. Con la geometría, una silla girada o
    /// una planta con la región de recorte girada se miden por lo que realmente se ve.
    /// La caja solo se usa como último recurso (textos, etiquetas…).
    /// </summary>
    internal static class ElementExtents
    {
        public static bool TryGet(Element element, ViewFrame frame, out Box2 box)
        {
            var builder = new BoxBuilder(frame);

            if (element is Viewport viewport)
            {
                // En planos: el marco de la vista sin su título.
                try
                {
                    Outline outline = viewport.GetBoxOutline();
                    builder.AddBoxCorners(outline.MinimumPoint, outline.MaximumPoint, null);
                }
                catch { }
            }
            else
            {
                AddGeometry(element, frame.View, builder);
                if (builder.IsEmpty) AddBoundingBox(element, frame.View, builder);
            }

            box = builder.IsEmpty ? default : builder.ToBox();
            return !builder.IsEmpty;
        }

        private static void AddGeometry(Element element, View view, BoxBuilder builder)
        {
            GeometryElement geometry = null;
            try
            {
                var options = new Options { ComputeReferences = false, IncludeNonVisibleObjects = false };
                if (view != null) options.View = view;
                geometry = element.get_Geometry(options);
            }
            catch { }

            if (geometry != null) Collect(geometry, builder);
        }

        private static void Collect(GeometryElement geometry, BoxBuilder builder)
        {
            foreach (GeometryObject obj in geometry)
            {
                try
                {
                    switch (obj)
                    {
                        case Solid solid:
                            if (solid.Faces.Size == 0) break;
                            foreach (Edge edge in solid.Edges) builder.AddRange(edge.Tessellate());
                            // Las caras planas quedan contenidas en sus aristas; las curvas
                            // (esferas, superficies libres) pueden sobresalir de ellas.
                            foreach (Face face in solid.Faces)
                            {
                                if (face is PlanarFace) continue;
                                Mesh mesh = face.Triangulate();
                                if (mesh != null) builder.AddRange(mesh.Vertices);
                            }
                            break;

                        case Curve curve:
                            builder.AddRange(curve.Tessellate());
                            break;

                        case PolyLine polyLine:
                            builder.AddRange(polyLine.GetCoordinates());
                            break;

                        case Mesh mesh:
                            builder.AddRange(mesh.Vertices);
                            break;

                        case GeometryInstance instance:
                            GeometryElement nested = instance.GetInstanceGeometry();
                            if (nested != null) Collect(nested, builder);
                            break;

                        case GeometryElement element:
                            Collect(element, builder);
                            break;
                    }
                }
                catch { /* una pieza de geometría problemática no invalida el resto */ }
            }
        }

        private static void AddBoundingBox(Element element, View view, BoxBuilder builder)
        {
            BoundingBoxXYZ box = null;
            try { box = element.get_BoundingBox(view); } catch { }
            if (box == null)
            {
                try { box = element.get_BoundingBox(null); } catch { }
            }
            // La documentación indica que esta caja ya está en ejes del modelo y que no
            // debe aplicársele rotación, así que se ignora su Transform.
            if (box != null) builder.AddBoxCorners(box.Min, box.Max, null);
        }
    }
}
