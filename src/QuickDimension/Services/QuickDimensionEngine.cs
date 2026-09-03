using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using QuickDimension.Model;

namespace QuickDimension.Services
{
    /// <summary>
    /// Convierte la línea trazada por el usuario en un plan de cota: qué referencias
    /// entran, en qué dirección exacta se mide y dónde se dibuja la línea de cota.
    /// </summary>
    /// <remarks>
    /// La idea es sencilla: la línea trazada define una dirección de medida. Solo sirven
    /// las referencias cuyo plano sea perpendicular a esa dirección, porque son las únicas
    /// que Revit sabe acotar en ella. De ahí salen los dos modos de forma natural:
    /// un muro que la línea cruza aporta sus caras laterales, y un muro al que la línea
    /// va paralela aporta sus extremos y las jambas de sus huecos.
    /// </remarks>
    public static class QuickDimensionEngine
    {
        /// <summary>Margen para el primer filtrado, unos 2,5°. Después se afina.</summary>
        private const double CoarseAlignment = 0.999;

        /// <summary>Mismo margen expresado como desviación respecto a la perpendicular.</summary>
        private const double CoarsePerpendicular = 0.0447;

        /// <summary>Dos normales más próximas que esto se consideran el mismo plano.</summary>
        private const double NormalTolerance = 1e-8;

        /// <summary>Margen de perpendicularidad para las referencias lineales, en el filtrado fino.</summary>
        private const double FinePerpendicular = 1e-6;

        /// <summary>Desviación admitida entre la referencia y el plano de la vista.</summary>
        private const double InPlaneTolerance = 1e-6;

        /// <summary>Vistas en las que la herramienta puede trabajar.</summary>
        public static bool IsSupportedView(View view)
        {
            if (view == null || view.IsTemplate) return false;

            switch (view.ViewType)
            {
                case ViewType.FloorPlan:
                case ViewType.CeilingPlan:
                case ViewType.EngineeringPlan:
                case ViewType.AreaPlan:
                case ViewType.Section:
                case ViewType.Elevation:
                case ViewType.Detail:
                    return true;
                default:
                    return false;
            }
        }

        public static DimensionPlan Build(Document doc, View view, XYZ start, XYZ end, QuickDimensionOptions options)
        {
            var plan = new DimensionPlan();

            if (doc == null || view == null || start == null || end == null || options == null)
            {
                plan.FailureReason = "Faltan datos para calcular la cota.";
                return plan;
            }

            XYZ viewNormal = SafeNormalize(view.ViewDirection);
            XYZ right = SafeNormalize(view.RightDirection);
            XYZ up = SafeNormalize(view.UpDirection);

            if (viewNormal == null || right == null || up == null)
            {
                plan.FailureReason = "No se puede determinar el plano de esta vista.";
                return plan;
            }

            // Aplanamos la línea sobre el plano de la vista: solo importa su dirección dentro de él.
            XYZ raw = end - start;
            raw = raw - viewNormal.Multiply(raw.DotProduct(viewNormal));
            XYZ drawnDirection = SafeNormalize(raw);

            if (drawnDirection == null)
            {
                plan.FailureReason = "Los dos puntos están demasiado juntos. Traza una línea más larga.";
                return plan;
            }

            UV a = PlanarGeometry.ToPlane(start, right, up);
            UV b = PlanarGeometry.ToPlane(end, right, up);
            double tolerance = Math.Max(options.SearchToleranceFeet, 0.0);

            var candidates = new List<ReferenceCandidate>();
            int touchedElements = 0;

            var walls = new List<Wall>();
            foreach (Element element in Collect(doc, view, ArchitecturalCategories.Crossable(options.IncludeColumns)))
            {
                var wall = element as Wall;
                if (wall != null)
                {
                    if (!WallTouches(wall, right, up, a, b, tolerance)) continue;
                    walls.Add(wall);
                    touchedElements++;
                    continue;
                }

                if (!BoundsTouch(element, view, right, up, a, b, tolerance)) continue;
                touchedElements++;
                AddPlanarFaces(element, ModelGeometryOptions(), candidates, ReferenceTier.Geometry, "columna");
            }

            bool wantOpenings = options.Scope == DimensionScope.IncludeOpenings;
            Dictionary<ElementId, List<Element>> openingsByHost = null;

            if (wantOpenings && options.OpeningCenters)
            {
                openingsByHost = MapOpeningsToHosts(doc, view);
            }

            foreach (Wall wall in walls)
            {
                AddWallSideFaces(wall, candidates);

                if (options.WallCenterlines) AddWallCenterline(wall, candidates);

                if (wantOpenings && options.OpeningEdges)
                {
                    // La geometría del muro ya viene con los huecos recortados: sus caras
                    // perpendiculares a la línea son los extremos del muro y las jambas.
                    AddPlanarFaces(wall, ModelGeometryOptions(), candidates, ReferenceTier.Geometry, "extremo o jamba");
                }

                if (wantOpenings && options.OpeningCenters && openingsByHost != null)
                {
                    List<Element> hosted;
                    if (openingsByHost.TryGetValue(wall.Id, out hosted)) AddOpeningCenters(hosted, candidates);
                }
            }

            if (touchedElements == 0)
            {
                plan.FailureReason =
                    "La línea no toca ningún muro ni columna visible en esta vista.\n\n" +
                    "Trázala atravesando los elementos que quieras acotar, o a lo largo del muro " +
                    "cuyas aberturas quieras medir.";
                return plan;
            }

            return Resolve(plan, candidates, start, end, drawnDirection, viewNormal, options, touchedElements);
        }

        /// <summary>
        /// Se queda con las referencias que comparten exactamente el mismo plano, fija la
        /// dirección de medida y coloca la línea de cota.
        /// </summary>
        private static DimensionPlan Resolve(DimensionPlan plan, List<ReferenceCandidate> candidates,
            XYZ start, XYZ end, XYZ drawnDirection, XYZ viewNormal, QuickDimensionOptions options, int touchedElements)
        {
            var oriented = new List<ReferenceCandidate>();
            foreach (ReferenceCandidate candidate in candidates)
            {
                if (candidate.Normal != null)
                {
                    // La cota se dibuja dentro de la vista, así que su dirección de medida vive
                    // en el plano de la vista. Una cara cuya normal se salga de ese plano no se
                    // puede acotar aquí por mucho que apunte hacia donde va la línea.
                    if (Math.Abs(candidate.Normal.DotProduct(viewNormal)) > InPlaneTolerance) continue;

                    // Las dos caras de un muro miran en sentidos opuestos, pero definen el mismo
                    // plano de medida: orientamos todas las normales hacia el sentido de trazado.
                    double alignment = candidate.Normal.DotProduct(drawnDirection);
                    if (Math.Abs(alignment) < CoarseAlignment) continue;
                    if (alignment < 0) candidate.Normal = candidate.Normal.Negate();
                }
                else if (candidate.LineDirection != null)
                {
                    if (Math.Abs(candidate.LineDirection.DotProduct(viewNormal)) > InPlaneTolerance) continue;
                    if (Math.Abs(candidate.LineDirection.DotProduct(drawnDirection)) > CoarsePerpendicular) continue;
                }
                else
                {
                    continue;
                }

                oriented.Add(candidate);
            }

            List<ReferenceCandidate> best = LargestCoplanarGroup(oriented, drawnDirection);
            if (best == null || best.Count == 0)
            {
                plan.FailureReason =
                    "Se encontraron " + touchedElements + " elemento(s), pero ninguna de sus caras es " +
                    "perpendicular a la línea.\n\n" +
                    "Revit solo puede acotar referencias perpendiculares a la dirección de medida: " +
                    "traza la línea perpendicular a los muros, o a lo largo del muro cuyas aberturas quieras medir.";
                return plan;
            }

            XYZ direction = best[0].Normal;

            // Las referencias lineales (ejes de muro) no tienen normal propia: entran si son
            // perpendiculares a la dirección ya fijada.
            foreach (ReferenceCandidate candidate in oriented)
            {
                if (candidate.Normal != null) continue;
                if (Math.Abs(candidate.LineDirection.DotProduct(direction)) > FinePerpendicular) continue;
                best.Add(candidate);
            }

            XYZ midpoint = (start + end).Multiply(0.5);
            XYZ perpendicular = viewNormal.CrossProduct(direction);
            perpendicular = SafeNormalize(perpendicular) ?? XYZ.Zero;
            XYZ origin = midpoint + perpendicular.Multiply(options.OffsetFeet);

            foreach (ReferenceCandidate candidate in best)
            {
                candidate.Position = (candidate.Point - origin).DotProduct(direction);
                plan.Candidates.Add(candidate);
            }

            plan.Origin = origin;
            plan.Direction = direction;

            int discarded = oriented.Count - best.Count;
            if (discarded > 0)
            {
                plan.AddNote(discarded + " referencia(s) quedaron fuera por no estar en el mismo plano de medida.");
            }

            if (plan.Candidates.Count < 2)
            {
                plan.FailureReason =
                    "Solo se encontró una referencia utilizable y una cota necesita al menos dos.\n\n" +
                    "Alarga la línea para que abarque más elementos.";
            }

            return plan;
        }

        /// <summary>Agrupa por normal y devuelve el grupo más numeroso; a igualdad, el mejor alineado.</summary>
        private static List<ReferenceCandidate> LargestCoplanarGroup(List<ReferenceCandidate> oriented, XYZ drawnDirection)
        {
            var groups = new List<List<ReferenceCandidate>>();

            foreach (ReferenceCandidate candidate in oriented)
            {
                if (candidate.Normal == null) continue;

                bool placed = false;
                foreach (List<ReferenceCandidate> group in groups)
                {
                    if (group[0].Normal.DotProduct(candidate.Normal) > 1.0 - NormalTolerance)
                    {
                        group.Add(candidate);
                        placed = true;
                        break;
                    }
                }

                if (!placed) groups.Add(new List<ReferenceCandidate> { candidate });
            }

            List<ReferenceCandidate> best = null;
            foreach (List<ReferenceCandidate> group in groups)
            {
                if (best == null)
                {
                    best = group;
                    continue;
                }

                if (group.Count > best.Count) best = group;
                else if (group.Count == best.Count &&
                         group[0].Normal.DotProduct(drawnDirection) > best[0].Normal.DotProduct(drawnDirection))
                {
                    best = group;
                }
            }

            return best;
        }

        private static IEnumerable<Element> Collect(Document doc, View view, IList<BuiltInCategory> categories)
        {
            var filter = new ElementMulticategoryFilter(new List<BuiltInCategory>(categories));
            return new FilteredElementCollector(doc, view.Id)
                .WherePasses(filter)
                .WhereElementIsNotElementType();
        }

        /// <summary>Puertas, ventanas y vanos visibles en la vista, agrupados por el muro que los aloja.</summary>
        private static Dictionary<ElementId, List<Element>> MapOpeningsToHosts(Document doc, View view)
        {
            var map = new Dictionary<ElementId, List<Element>>();

            foreach (Element element in Collect(doc, view, ArchitecturalCategories.Openings()))
            {
                Element host = null;

                var instance = element as FamilyInstance;
                if (instance != null)
                {
                    try { host = instance.Host; }
                    catch { }
                }
                else
                {
                    var opening = element as Opening;
                    if (opening != null)
                    {
                        try { host = opening.Host; }
                        catch { }
                    }
                }

                if (host == null || host.Id == null) continue;

                List<Element> bucket;
                if (!map.TryGetValue(host.Id, out bucket))
                {
                    bucket = new List<Element>();
                    map[host.Id] = bucket;
                }

                bucket.Add(element);
            }

            return map;
        }

        private static bool WallTouches(Wall wall, XYZ right, XYZ up, UV a, UV b, double tolerance)
        {
            var location = wall.Location as LocationCurve;
            if (location == null || location.Curve == null) return false;

            double halfWidth = 0.0;
            try { halfWidth = wall.Width / 2.0; }
            catch { }

            IList<XYZ> points;
            try { points = location.Curve.Tessellate(); }
            catch { return false; }
            if (points == null || points.Count < 2) return false;

            double reach = halfWidth + tolerance;

            for (int i = 0; i + 1 < points.Count; i++)
            {
                UV c = PlanarGeometry.ToPlane(points[i], right, up);
                UV d = PlanarGeometry.ToPlane(points[i + 1], right, up);
                if (PlanarGeometry.SegmentDistance(a, b, c, d) <= reach) return true;
            }

            return false;
        }

        private static bool BoundsTouch(Element element, View view, XYZ right, XYZ up, UV a, UV b, double tolerance)
        {
            BoundingBoxXYZ box = null;
            try { box = element.get_BoundingBox(view); }
            catch { }
            if (box == null)
            {
                try { box = element.get_BoundingBox(null); }
                catch { }
            }

            double minU, minV, maxU, maxV;
            if (!PlanarGeometry.TryGetPlanarBounds(box, right, up, out minU, out minV, out maxU, out maxV)) return false;

            return PlanarGeometry.SegmentIntersectsRectangle(a, b,
                minU - tolerance, minV - tolerance, maxU + tolerance, maxV + tolerance);
        }

        private static Options ModelGeometryOptions()
        {
            return new Options
            {
                ComputeReferences = true,
                IncludeNonVisibleObjects = false,
                DetailLevel = ViewDetailLevel.Fine,
            };
        }

        private static void AddWallSideFaces(Wall wall, List<ReferenceCandidate> sink)
        {
            var layers = new[] { ShellLayerType.Exterior, ShellLayerType.Interior };

            foreach (ShellLayerType layer in layers)
            {
                IList<Reference> references = null;
                try { references = HostObjectUtils.GetSideFaces(wall, layer); }
                catch { }
                if (references == null) continue;

                foreach (Reference reference in references)
                {
                    if (reference == null) continue;

                    PlanarFace face = null;
                    try { face = wall.GetGeometryObjectFromReference(reference) as PlanarFace; }
                    catch { }
                    if (face == null) continue;

                    Add(sink, reference, face.FaceNormal, face.Origin, ReferenceTier.Solid, "cara de muro");
                }
            }
        }

        private static void AddWallCenterline(Wall wall, List<ReferenceCandidate> sink)
        {
            // El muro expone su eje como una línea no visible, pero puede devolver más de una
            // línea: nos quedamos con la que coincide en planta con su curva de posición.
            var location = wall.Location as LocationCurve;
            if (location == null || location.Curve == null) return;

            XYZ from = location.Curve.GetEndPoint(0);
            XYZ to = location.Curve.GetEndPoint(1);

            var options = new Options
            {
                ComputeReferences = true,
                IncludeNonVisibleObjects = true,
                DetailLevel = ViewDetailLevel.Fine,
            };

            GeometryElement geometry = null;
            try { geometry = wall.get_Geometry(options); }
            catch { }
            if (geometry == null) return;

            foreach (GeometryObject item in geometry)
            {
                var line = item as Line;
                if (line == null || line.Reference == null) continue;

                XYZ direction = SafeNormalize(line.Direction);
                if (direction == null) continue;

                XYZ head = line.GetEndPoint(0);
                XYZ tail = line.GetEndPoint(1);

                bool matchesLocation =
                    (SamePlanPosition(head, from) && SamePlanPosition(tail, to)) ||
                    (SamePlanPosition(head, to) && SamePlanPosition(tail, from));

                if (!matchesLocation) continue;

                sink.Add(new ReferenceCandidate
                {
                    Reference = line.Reference,
                    Normal = null,
                    LineDirection = direction,
                    Point = head,
                    Tier = ReferenceTier.Plane,
                    Source = "eje de muro",
                });

                return;
            }
        }

        /// <summary>Compara dos puntos ignorando la altura: el eje del muro y su curva de posición pueden estar a cotas distintas.</summary>
        private static bool SamePlanPosition(XYZ left, XYZ right)
        {
            return Math.Abs(left.X - right.X) < 1e-6 && Math.Abs(left.Y - right.Y) < 1e-6;
        }

        private static void AddOpeningCenters(List<Element> hosted, List<ReferenceCandidate> sink)
        {
            foreach (Element element in hosted)
            {
                var instance = element as FamilyInstance;
                if (instance == null) continue;

                IList<Reference> references = null;
                try { references = instance.GetReferences(FamilyInstanceReferenceType.CenterLeftRight); }
                catch { }
                if (references == null || references.Count == 0) continue;

                XYZ normal = null;
                try { normal = SafeNormalize(instance.HandOrientation); }
                catch { }
                if (normal == null) continue;

                XYZ point = null;
                var location = instance.Location as LocationPoint;
                if (location != null) point = location.Point;
                if (point == null) continue;

                foreach (Reference reference in references)
                {
                    if (reference == null) continue;
                    Add(sink, reference, normal, point, ReferenceTier.Plane, "eje de abertura");
                }
            }
        }

        private static void AddPlanarFaces(Element element, Options options, List<ReferenceCandidate> sink,
            ReferenceTier tier, string source)
        {
            GeometryElement geometry = null;
            try { geometry = element.get_Geometry(options); }
            catch { }
            if (geometry == null) return;

            CollectFaces(geometry, sink, tier, source, 0);
        }

        private static void CollectFaces(GeometryElement geometry, List<ReferenceCandidate> sink,
            ReferenceTier tier, string source, int depth)
        {
            if (depth > 4) return;

            foreach (GeometryObject item in geometry)
            {
                var solid = item as Solid;
                if (solid != null)
                {
                    if (solid.Faces == null || solid.Faces.Size == 0) continue;

                    foreach (Face face in solid.Faces)
                    {
                        var planar = face as PlanarFace;
                        if (planar == null || planar.Reference == null) continue;

                        Add(sink, planar.Reference, planar.FaceNormal, planar.Origin, tier, source);
                    }

                    continue;
                }

                var instance = item as GeometryInstance;
                if (instance == null) continue;

                GeometryElement inner = null;
                try { inner = instance.GetInstanceGeometry(); }
                catch { }
                if (inner != null) CollectFaces(inner, sink, tier, source, depth + 1);
            }
        }

        private static void Add(List<ReferenceCandidate> sink, Reference reference, XYZ normal, XYZ point,
            ReferenceTier tier, string source)
        {
            XYZ unit = SafeNormalize(normal);
            if (unit == null || point == null) return;

            sink.Add(new ReferenceCandidate
            {
                Reference = reference,
                Normal = unit,
                Point = point,
                Tier = tier,
                Source = source,
            });
        }

        private static XYZ SafeNormalize(XYZ vector)
        {
            if (vector == null) return null;
            double length = vector.GetLength();
            if (length < 1e-9) return null;
            return vector.Divide(length);
        }
    }
}
