using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;

namespace LinkWorksetInspector.Services.RoomElevations
{
    /// <summary>Resultado del proceso de una habitación.</summary>
    public class RoomElevationResult
    {
        public string RoomLabel { get; set; }
        public List<string> CreatedViews { get; } = new List<string>();
        public List<string> Notes { get; } = new List<string>();
        public bool Failed { get; set; }
        public bool Skipped { get; set; }
    }

    /// <summary>
    /// Crea un alzado interior por cada dirección de muro de cada habitación.
    /// Cada vista se recorta al ancho de la habitación y, en vertical, entre el
    /// piso y el cielo raso o la losa más cercana por encima; el corte lejano se
    /// ajusta para que solo aparezcan los muros de esa habitación.
    /// Debe ejecutarse dentro del contexto de la API de Revit.
    /// </summary>
    public class RoomElevationGenerator
    {
        private class Overhead
        {
            public BoundingBoxXYZ Box;
            public string Kind;
        }

        private readonly Document _doc;
        private readonly RoomElevationOptions _opt;
        private readonly HashSet<string> _usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly List<Overhead> _overheads = new List<Overhead>();

        private static readonly string[] CompassNames =
        {
            "Norte", "Noroeste", "Oeste", "Suroeste", "Sur", "Sureste", "Este", "Noreste",
        };

        public RoomElevationGenerator(Document doc, RoomElevationOptions options)
        {
            _doc = doc;
            _opt = options;

            foreach (View v in new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>())
            {
                try { _usedNames.Add(v.Name); } catch { }
            }

            if (_opt.UseCeilings) CollectOverheads(BuiltInCategory.OST_Ceilings, "cielo raso");
            if (_opt.UseSlabs) CollectOverheads(BuiltInCategory.OST_Floors, "losa");
        }

        // ================================================================== entrada

        public IList<RoomElevationResult> Run(IList<Room> rooms, Action<string> progress)
        {
            var results = new List<RoomElevationResult>();

            using (var group = new TransactionGroup(_doc, "Crear alzados por habitación"))
            {
                group.Start();

                int index = 0;
                foreach (Room room in rooms)
                {
                    index++;
                    string label = Label(room);
                    progress?.Invoke("Procesando " + index + "/" + rooms.Count + ": " + label);

                    var result = new RoomElevationResult { RoomLabel = label };
                    using (var tx = new Transaction(_doc, "Alzados de " + label))
                    {
                        tx.Start();
                        try
                        {
                            ProcessRoom(room, result);
                            if (result.CreatedViews.Count > 0) tx.Commit();
                            else SafeRollBack(tx);
                        }
                        catch (Exception ex)
                        {
                            // Si la excepción vino del propio Commit, la transacción ya
                            // terminó y deshacerla otra vez tumbaría todo el lote.
                            SafeRollBack(tx);

                            // Lo deshecho no existe: ni se informa ni reserva su nombre.
                            foreach (string reverted in result.CreatedViews) _usedNames.Remove(reverted);
                            result.CreatedViews.Clear();

                            result.Failed = true;
                            result.Notes.Add("Error: " + ex.Message);
                        }
                    }
                    results.Add(result);
                }

                group.Assimilate();
            }

            return results;
        }

        private static void SafeRollBack(Transaction tx)
        {
            try
            {
                if (tx.HasStarted() && !tx.HasEnded()) tx.RollBack();
            }
            catch { }
        }

        // ================================================================== una habitación

        private void ProcessRoom(Room room, RoomElevationResult result)
        {
            if (room.Area <= 0)
            {
                result.Skipped = true;
                result.Notes.Add("La habitación no está colocada o no está cerrada (área 0).");
                return;
            }

            BoundingBoxXYZ roomBox = room.get_BoundingBox(null);
            var level = _doc.GetElement(room.LevelId) as Level;
            double floorZ = roomBox != null ? roomBox.Min.Z : (level != null ? level.Elevation : 0.0);

            XYZ center = (room.Location as LocationPoint)?.Point;
            if (center == null && roomBox != null) center = (roomBox.Min + roomBox.Max) * 0.5;
            if (center == null)
            {
                result.Failed = true;
                result.Notes.Add("No se pudo determinar el centro de la habitación.");
                return;
            }

            // --- bordes de la habitación ---
            var boundaryOptions = new SpatialElementBoundaryOptions
            {
                SpatialElementBoundaryLocation = SpatialElementBoundaryLocation.Finish,
            };
            IList<IList<BoundarySegment>> loops = room.GetBoundarySegments(boundaryOptions);
            if (loops == null || loops.Count == 0)
            {
                result.Failed = true;
                result.Notes.Add("La habitación no tiene bordes calculables (¿no está cerrada?).");
                return;
            }

            var curves = new List<Curve>();
            var boundaryPoints = new List<XYZ>();
            foreach (IList<BoundarySegment> loop in loops)
            {
                foreach (BoundarySegment segment in loop)
                {
                    Curve curve;
                    try { curve = segment.GetCurve(); } catch { continue; }
                    if (curve == null) continue;
                    curves.Add(curve);
                    try { boundaryPoints.AddRange(curve.Tessellate()); }
                    catch { boundaryPoints.Add(curve.GetEndPoint(0)); boundaryPoints.Add(curve.GetEndPoint(1)); }
                }
            }
            if (boundaryPoints.Count == 0)
            {
                result.Failed = true;
                result.Notes.Add("No se pudieron leer los bordes de la habitación.");
                return;
            }

            // --- direcciones de mirada (una por cara de muro) ---
            List<XYZ> looks = LookDirections(room, curves, center, floorZ);
            if (looks.Count == 0)
            {
                result.Failed = true;
                result.Notes.Add("No se encontraron muros de al menos " +
                                 _opt.MinWallLengthMm.ToString("0") + " mm. Baja la longitud mínima de muro.");
                return;
            }

            // Se comprueba contra los nombres que tendrían estos alzados, no contra un
            // prefijo, para que funcione con cualquier patrón de nombre.
            if (_opt.SkipRoomsWithElevations && looks.Any(l => _usedNames.Contains(BuildName(room, l))))
            {
                result.Skipped = true;
                result.Notes.Add("Ya existen alzados con estos nombres; se omite.");
                return;
            }

            // --- techo: cielo raso o losa más cercana por encima ---
            string topSource;
            double topZ = FindTopZ(center, floorZ, out topSource);
            result.Notes.Add("Altura del recorte: " +
                             FeetToMm(topZ - floorZ).ToString("0") + " mm (" + topSource + ").");

            // --- vista de planta anfitriona del marcador ---
            ElementId planViewId = FindPlanView(room.LevelId);
            if (planViewId == ElementId.InvalidElementId)
            {
                result.Failed = true;
                result.Notes.Add("No hay ninguna vista de planta asociada al nivel de la habitación; " +
                                 "Revit la necesita para colocar el marcador de alzado.");
                return;
            }

            double margin = Mm(_opt.VerticalMarginMm);
            CreateViewsForRoom(room, looks, planViewId, center, floorZ,
                floorZ - margin, topZ + margin, boundaryPoints, result);
        }

        // ================================================================== direcciones

        /// <summary>
        /// Una dirección de mirada por cada orientación distinta de muro: apunta desde
        /// el interior de la habitación hacia el muro que se va a dibujar.
        /// </summary>
        private List<XYZ> LookDirections(Room room, IList<Curve> curves, XYZ center, double floorZ)
        {
            double minLength = Mm(_opt.MinWallLengthMm);
            double tolerance = _opt.DirectionToleranceDeg * Math.PI / 180.0;
            var directions = new List<XYZ>();

            foreach (Curve curve in curves)
            {
                double length;
                try { length = curve.Length; } catch { continue; }
                if (length < minLength) continue;

                XYZ mid;
                XYZ tangent;
                try
                {
                    mid = curve.Evaluate(0.5, true);
                    tangent = curve.ComputeDerivatives(0.5, true).BasisX;
                }
                catch
                {
                    XYZ a = curve.GetEndPoint(0), b = curve.GetEndPoint(1);
                    mid = (a + b) * 0.5;
                    tangent = b - a;
                }

                tangent = Flatten(tangent);
                if (tangent == null) continue;

                // En el bucle exterior de Revit (antihorario) el interior queda a la izquierda.
                XYZ inward = XYZ.BasisZ.CrossProduct(tangent).Normalize();
                inward = OrientInward(room, mid, floorZ, inward, center);

                XYZ look = inward.Negate();
                if (!directions.Any(d => d.AngleTo(look) < tolerance)) directions.Add(look);
            }

            directions.Sort((a, b) => CompassAngle(a).CompareTo(CompassAngle(b)));
            return directions;
        }

        /// <summary>Comprueba que la normal apunta al interior; si no, la invierte.</summary>
        private XYZ OrientInward(Room room, XYZ mid, double floorZ, XYZ candidate, XYZ center)
        {
            double probeOut = Mm(150);
            double probeUp = Mm(100);
            XYZ basePoint = new XYZ(mid.X, mid.Y, floorZ + probeUp);

            if (PointInRoom(room, basePoint + candidate * probeOut)) return candidate;
            if (PointInRoom(room, basePoint - candidate * probeOut)) return candidate.Negate();

            // Sin volumen de habitación fiable: decide con el centro de la habitación.
            if (center != null && (center - mid).DotProduct(candidate) < 0) return candidate.Negate();
            return candidate;
        }

        private static bool PointInRoom(Room room, XYZ point)
        {
            try { return room.IsPointInRoom(point); }
            catch { return false; }
        }

        // ================================================================== techo

        private void CollectOverheads(BuiltInCategory category, string kind)
        {
            foreach (Element element in new FilteredElementCollector(_doc)
                         .OfCategory(category).WhereElementIsNotElementType())
            {
                BoundingBoxXYZ box;
                try { box = element.get_BoundingBox(null); } catch { continue; }
                if (box != null) _overheads.Add(new Overhead { Box = box, Kind = kind });
            }
        }

        /// <summary>
        /// Cara inferior del cielo raso o la losa más cercana por encima del piso de la
        /// habitación. Si no hay ninguno, devuelve el piso más la altura por defecto.
        /// </summary>
        private double FindTopZ(XYZ center, double floorZ, out string source)
        {
            double minAbove = floorZ + Mm(100);          // evita el propio piso de la habitación
            double maxAbove = floorZ + Mm(_opt.MaxSearchHeightMm);
            double tolXY = Mm(50);

            double best = double.MaxValue;
            string bestKind = null;

            foreach (Overhead overhead in _overheads)
            {
                BoundingBoxXYZ box = overhead.Box;
                double underside = box.Min.Z;
                if (underside <= minAbove || underside >= maxAbove) continue;
                if (underside >= best) continue;
                if (center.X < box.Min.X - tolXY || center.X > box.Max.X + tolXY) continue;
                if (center.Y < box.Min.Y - tolXY || center.Y > box.Max.Y + tolXY) continue;

                best = underside;
                bestKind = overhead.Kind;
            }

            if (bestKind == null)
            {
                source = "altura por defecto, no se encontró cielo raso ni losa";
                return floorZ + Mm(_opt.DefaultHeightMm);
            }

            source = bestKind;
            return best;
        }

        // ================================================================== vistas

        private ElementId FindPlanView(ElementId levelId)
        {
            ViewPlan best = null;
            foreach (ViewPlan plan in new FilteredElementCollector(_doc)
                         .OfClass(typeof(ViewPlan)).Cast<ViewPlan>())
            {
                if (plan.IsTemplate) continue;

                // Un plano de áreas no sirve de anfitrión: CreateElevation lanzaría.
                if (plan.ViewType != ViewType.FloorPlan &&
                    plan.ViewType != ViewType.CeilingPlan &&
                    plan.ViewType != ViewType.EngineeringPlan) continue;

                Level genLevel;
                try { genLevel = plan.GenLevel; } catch { continue; }
                if (genLevel == null || genLevel.Id != levelId) continue;

                if (plan.ViewType == ViewType.FloorPlan) return plan.Id;
                if (best == null) best = plan;
            }
            return best != null ? best.Id : ElementId.InvalidElementId;
        }

        private void CreateViewsForRoom(Room room, List<XYZ> looks, ElementId planViewId, XYZ center,
            double floorZ, double bottomZ, double topZ, List<XYZ> boundaryPoints, RoomElevationResult result)
        {
            var pending = new List<XYZ>(looks);
            double tolerance = _opt.DirectionToleranceDeg * Math.PI / 180.0;
            int markerIndex = 0;

            while (pending.Count > 0 && markerIndex < 8)
            {
                XYZ first = pending[0];
                XYZ offset = MarkerOffset(first, markerIndex);

                // El marcador va a la cota del piso: bottomZ lleva restado el margen de
                // recorte y dejaría el símbolo fuera del rango de la vista de planta.
                var origin = new XYZ(center.X + offset.X, center.Y + offset.Y, floorZ);

                ElevationMarker marker = ElevationMarker.CreateElevationMarker(
                    _doc, _opt.ViewFamilyTypeId, origin, _opt.Scale);

                ViewSection firstView;
                try { firstView = marker.CreateElevation(_doc, planViewId, 0); }
                catch (Exception ex)
                {
                    result.Failed = true;
                    result.Notes.Add("Revit no pudo crear el alzado en la vista de planta elegida: " +
                                     ex.Message);
                    return;
                }
                _doc.Regenerate();

                RotateMarkerToLook(marker, firstView, origin, first);

                FinishView(firstView, room, first, boundaryPoints, bottomZ, topZ, result);
                pending.RemoveAt(0);

                int slots = marker.MaximumViewCount;
                for (int i = 1; i < slots && pending.Count > 0; i++)
                {
                    if (!marker.IsAvailableIndex(i)) continue;

                    ViewSection view;
                    try { view = marker.CreateElevation(_doc, planViewId, i); }
                    catch { continue; }
                    _doc.Regenerate();

                    XYZ look = Flatten(view.ViewDirection.Negate());
                    XYZ match = look == null
                        ? null
                        : pending.FirstOrDefault(d => d.AngleTo(look) < tolerance);

                    if (match != null)
                    {
                        FinishView(view, room, match, boundaryPoints, bottomZ, topZ, result);
                        pending.Remove(match);
                    }
                    else
                    {
                        _doc.Delete(view.Id);
                    }
                }

                markerIndex++;
            }

            if (pending.Count > 0)
                result.Notes.Add("Quedaron " + pending.Count + " direcciones sin alzado (demasiadas " +
                                 "orientaciones de muro distintas).");
        }

        /// <summary>Gira el marcador para que su vista mire hacia la dirección pedida.</summary>
        private void RotateMarkerToLook(ElevationMarker marker, ViewSection view, XYZ origin, XYZ desiredLook)
        {
            XYZ current = Flatten(view.ViewDirection.Negate());
            if (current == null) return;

            double angle = current.AngleOnPlaneTo(desiredLook, XYZ.BasisZ);
            if (angle < 1e-9 || Math.Abs(angle - 2 * Math.PI) < 1e-9) return;

            Line axis = Line.CreateBound(origin, origin + XYZ.BasisZ);
            ElementTransformUtils.RotateElement(_doc, marker.Id, axis, angle);
            _doc.Regenerate();
        }

        private void FinishView(ViewSection view, Room room, XYZ look, List<XYZ> boundaryPoints,
            double bottomZ, double topZ, RoomElevationResult result)
        {
            string name = UniqueName(BuildName(room, look));
            try { view.Name = name; }
            catch (Exception ex) { result.Notes.Add("No se pudo renombrar una vista: " + ex.Message); }

            // Revit puede haber rechazado el nombre: se informa el que la vista tiene.
            try { name = view.Name; } catch { }
            _usedNames.Add(name);

            if (_opt.ViewTemplateId != ElementId.InvalidElementId)
            {
                try
                {
                    view.ViewTemplateId = _opt.ViewTemplateId;
                    _doc.Regenerate();
                }
                catch (Exception ex) { result.Notes.Add("No se pudo aplicar la plantilla: " + ex.Message); }
            }

            try { view.Scale = _opt.Scale; }
            catch { result.Notes.Add("La escala la controla la plantilla de vista."); }

            ApplyCrop(view, boundaryPoints, bottomZ, topZ, result);
            ApplyFarClip(view, look, boundaryPoints, result);

            result.CreatedViews.Add(name);
        }

        /// <summary>
        /// Recorta la vista al ancho de la habitación y, en vertical, entre el piso y el
        /// cielo raso o losa. Solo se tocan los ejes horizontal y vertical del recorte;
        /// la profundidad se controla con el corte lejano.
        /// </summary>
        private void ApplyCrop(ViewSection view, List<XYZ> boundaryPoints,
            double bottomZ, double topZ, RoomElevationResult result)
        {
            BoundingBoxXYZ crop;
            try
            {
                crop = view.CropBox;
                Transform toLocal = crop.Transform.Inverse;

                double minX = double.MaxValue, maxX = double.MinValue;
                double minY = double.MaxValue, maxY = double.MinValue;

                foreach (XYZ point in boundaryPoints)
                {
                    foreach (double z in new[] { bottomZ, topZ })
                    {
                        XYZ local = toLocal.OfPoint(new XYZ(point.X, point.Y, z));
                        if (local.X < minX) minX = local.X;
                        if (local.X > maxX) maxX = local.X;
                        if (local.Y < minY) minY = local.Y;
                        if (local.Y > maxY) maxY = local.Y;
                    }
                }

                double margin = Mm(_opt.HorizontalMarginMm);
                crop.Min = new XYZ(minX - margin, minY, crop.Min.Z);
                crop.Max = new XYZ(maxX + margin, maxY, crop.Max.Z);
            }
            catch (Exception ex)
            {
                result.Notes.Add("No se pudo calcular el recorte: " + ex.Message);
                return;
            }

            // Cada ajuste va por separado: si la plantilla de vista controla el
            // interruptor de recorte, las dimensiones sí se pueden seguir aplicando.
            try { view.CropBoxActive = true; }
            catch { result.Notes.Add("El recorte lo activa la plantilla de vista."); }

            try { view.CropBox = crop; }
            catch (Exception ex) { result.Notes.Add("No se pudo dimensionar el recorte: " + ex.Message); }

            try { view.CropBoxVisible = false; } catch { }
        }

        /// <summary>
        /// Sitúa el corte lejano justo detrás del muro más lejano de la habitación, para
        /// que el alzado no muestre las estancias contiguas.
        /// </summary>
        private void ApplyFarClip(ViewSection view, XYZ look, List<XYZ> boundaryPoints,
            RoomElevationResult result)
        {
            try
            {
                XYZ origin = view.Origin;
                double depth = 0;
                foreach (XYZ point in boundaryPoints)
                {
                    var flat = new XYZ(point.X - origin.X, point.Y - origin.Y, 0);
                    double d = flat.DotProduct(look);
                    if (d > depth) depth = d;
                }
                depth += Mm(_opt.BeyondWallMm);
                if (depth <= 0) return;

                // 0 = sin recorte; cualquier otro valor activa el corte lejano.
                Parameter mode = view.get_Parameter(BuiltInParameter.VIEWER_BOUND_FAR_CLIPPING);
                Parameter offset = view.get_Parameter(BuiltInParameter.VIEWER_BOUND_OFFSET_FAR);

                if (mode == null || mode.IsReadOnly || offset == null || offset.IsReadOnly)
                {
                    result.Notes.Add("El corte lejano lo controla la plantilla de vista; sin ajustar, " +
                                     "el alzado puede mostrar las estancias contiguas.");
                    return;
                }

                mode.Set(1);
                offset.Set(depth);
            }
            catch (Exception ex)
            {
                result.Notes.Add("No se pudo ajustar el corte lejano: " + ex.Message);
            }
        }

        // ================================================================== nombres

        /// <summary>Nombre que tendrá el alzado de esta habitación hacia esta dirección.</summary>
        private string BuildName(Room room, XYZ look)
        {
            string number = GetParam(room, BuiltInParameter.ROOM_NUMBER);
            string name = GetParam(room, BuiltInParameter.ROOM_NAME);
            string direction = "Muro " + CompassName(look);

            string result = (_opt.NamePattern ?? "{numero} {nombre} - {direccion}")
                .Replace("{numero}", number)
                .Replace("{nombre}", name)
                .Replace("{direccion}", direction);

            return Sanitize(result).Trim();
        }

        private static string GetParam(Element element, BuiltInParameter bip)
        {
            try
            {
                Parameter p = element.get_Parameter(bip);
                return p != null ? (p.AsString() ?? "") : "";
            }
            catch { return ""; }
        }

        /// <summary>Quita los caracteres que Revit no admite en nombres de vista.</summary>
        private static string Sanitize(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            foreach (char c in new[] { '\\', ':', '{', '}', '[', ']', '|', ';', '<', '>', '?', '`', '~' })
                value = value.Replace(c, ' ');
            while (value.Contains("  ")) value = value.Replace("  ", " ");
            return value;
        }

        private string UniqueName(string baseName)
        {
            if (string.IsNullOrWhiteSpace(baseName)) baseName = "Alzado";
            if (!_usedNames.Contains(baseName)) return baseName;

            for (int i = 2; i < 1000; i++)
            {
                string candidate = baseName + " (" + i + ")";
                if (!_usedNames.Contains(candidate)) return candidate;
            }
            return baseName + " " + Guid.NewGuid().ToString("N").Substring(0, 6);
        }

        private static string Label(Room room)
        {
            string number = GetParam(room, BuiltInParameter.ROOM_NUMBER);
            string name = GetParam(room, BuiltInParameter.ROOM_NAME);
            string label = (number + " " + name).Trim();
            return label.Length > 0 ? label : "Habitación " + room.Id;
        }

        /// <summary>Nombre del punto cardinal hacia el que mira la vista.</summary>
        private static string CompassName(XYZ look)
        {
            double degrees = CompassAngle(look) * 180.0 / Math.PI;
            int index = (int)Math.Round(degrees / 45.0) % 8;
            if (index < 0) index += 8;
            return CompassNames[index];
        }

        /// <summary>Ángulo antihorario desde el norte, en radianes [0, 2π).</summary>
        private static double CompassAngle(XYZ direction)
        {
            XYZ flat = Flatten(direction);
            if (flat == null) return 0;
            return XYZ.BasisY.AngleOnPlaneTo(flat, XYZ.BasisZ);
        }

        // ================================================================== auxiliares

        private XYZ MarkerOffset(XYZ look, int markerIndex)
        {
            if (markerIndex == 0) return XYZ.Zero;
            XYZ flat = Flatten(look);
            if (flat == null) return XYZ.Zero;

            XYZ perpendicular = XYZ.BasisZ.CrossProduct(flat).Normalize();
            double step = Mm(200) * ((markerIndex + 1) / 2);
            if (markerIndex % 2 == 0) step = -step;
            return perpendicular * step;
        }

        /// <summary>Proyecta al plano XY y normaliza; null si el vector es vertical.</summary>
        private static XYZ Flatten(XYZ vector)
        {
            if (vector == null) return null;
            var flat = new XYZ(vector.X, vector.Y, 0);
            return flat.GetLength() < 1e-9 ? null : flat.Normalize();
        }

        private static double Mm(double millimeters)
        {
            return UnitUtils.ConvertToInternalUnits(millimeters, UnitTypeId.Millimeters);
        }

        private static double FeetToMm(double feet)
        {
            return UnitUtils.ConvertFromInternalUnits(feet, UnitTypeId.Millimeters);
        }
    }
}
