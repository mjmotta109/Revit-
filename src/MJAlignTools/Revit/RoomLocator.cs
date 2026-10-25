using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using MJAlignTools.Core;

namespace MJAlignTools.Revit
{
    /// <summary>Habitación encontrada y su rectángulo en la vista.</summary>
    internal sealed class RoomInfo
    {
        public string Key { get; set; }
        public string Label { get; set; }
        public Box2 Box { get; set; }
    }

    /// <summary>
    /// Encuentra la habitación en la que está cada elemento, en el propio modelo o en
    /// un vínculo RVT cargado (caso habitual cuando el mobiliario va en un modelo
    /// aparte que vincula la arquitectura). El rectángulo de la habitación se calcula
    /// con sus líneas de contorno (cara de acabado), proyectadas en la vista.
    /// </summary>
    internal sealed class RoomLocator
    {
        private sealed class LinkContext
        {
            public RevitLinkInstance Instance;
            public Document Document;
            public Transform Transform;
            public Transform Inverse;
        }

        private readonly Document _doc;
        private readonly ViewFrame _frame;
        private readonly Phase _phase;
        private readonly Dictionary<string, RoomInfo> _cache = new Dictionary<string, RoomInfo>();
        private readonly SpatialElementBoundaryOptions _boundaryOptions = new SpatialElementBoundaryOptions
        {
            SpatialElementBoundaryLocation = SpatialElementBoundaryLocation.Finish,
        };
        private List<LinkContext> _links;

        public RoomLocator(Document doc, ViewFrame frame)
        {
            _doc = doc;
            _frame = frame;
            _phase = GetViewPhase(doc, frame.View);
        }

        /// <summary>Devuelve null si el elemento no está dentro de ninguna habitación cerrada.</summary>
        public RoomInfo Find(Element element)
        {
            Room room = null;
            if (element is FamilyInstance instance)
            {
                // Usa el punto de cálculo de habitación de la familia, si lo tiene.
                try { room = _phase != null ? instance.get_Room(_phase) : instance.Room; } catch { }
            }

            XYZ probe = null;
            if (room == null)
            {
                probe = GetProbePoint(element);
                if (probe != null)
                {
                    try { room = _doc.GetRoomAtPoint(probe, _phase); } catch { }
                }
            }

            if (room != null) return Describe(room, null, "modelo");
            if (probe == null) return null;

            foreach (LinkContext link in GetLinks())
            {
                Room linked = null;
                try { linked = link.Document.GetRoomAtPoint(link.Inverse.OfPoint(probe)); } catch { }
                if (linked != null) return Describe(linked, link.Transform, "vínculo " + link.Instance.Id);
            }
            return null;
        }

        private RoomInfo Describe(Room room, Transform transform, string scope)
        {
            string key = scope + ":" + room.Id;
            if (_cache.TryGetValue(key, out RoomInfo cached)) return cached;

            RoomInfo info = null;
            IList<IList<BoundarySegment>> loops = null;
            try { loops = room.GetBoundarySegments(_boundaryOptions); } catch { }

            if (loops != null && loops.Count > 0)
            {
                // Altura de la habitación, para que también funcione en alzados y secciones.
                double bottom = 0, top = 0;
                BoundingBoxXYZ volume = null;
                try { volume = room.get_BoundingBox(null); } catch { }
                if (volume != null)
                {
                    bottom = volume.Min.Z;
                    top = volume.Max.Z;
                }

                var builder = new BoxBuilder(_frame);
                foreach (IList<BoundarySegment> loop in loops)
                {
                    foreach (BoundarySegment segment in loop)
                    {
                        Curve curve = null;
                        try { curve = segment.GetCurve(); } catch { }
                        if (curve == null) continue;

                        foreach (XYZ p in curve.Tessellate())
                        {
                            foreach (double z in new[] { bottom, top })
                            {
                                var point = new XYZ(p.X, p.Y, volume != null ? z : p.Z);
                                builder.Add(transform != null ? transform.OfPoint(point) : point);
                            }
                        }
                    }
                }

                if (!builder.IsEmpty)
                {
                    info = new RoomInfo { Key = key, Label = RoomLabel(room), Box = builder.ToBox() };
                }
            }

            _cache[key] = info;
            return info;
        }

        private static string RoomLabel(Room room)
        {
            string name = null;
            try { name = room.get_Parameter(BuiltInParameter.ROOM_NAME)?.AsString(); } catch { }
            string number = null;
            try { number = room.Number; } catch { }
            string label = ((number ?? "") + " " + (name ?? "")).Trim();
            return label.Length > 0 ? label : "Habitación " + room.Id;
        }

        /// <summary>Centro de la caja del elemento (a media altura) o su punto de inserción.</summary>
        private static XYZ GetProbePoint(Element element)
        {
            BoundingBoxXYZ box = null;
            try { box = element.get_BoundingBox(null); } catch { }
            if (box != null) return (box.Min + box.Max) / 2.0;

            if (element.Location is LocationPoint location) return location.Point;
            return null;
        }

        private static Phase GetViewPhase(Document doc, View view)
        {
            try
            {
                Parameter parameter = view?.get_Parameter(BuiltInParameter.VIEW_PHASE);
                if (parameter != null && parameter.HasValue)
                    return doc.GetElement(parameter.AsElementId()) as Phase;
            }
            catch { }
            return null; // sin fase: la API usa la última fase del proyecto
        }

        private List<LinkContext> GetLinks()
        {
            if (_links != null) return _links;

            _links = new List<LinkContext>();
            foreach (Element element in new FilteredElementCollector(_doc).OfClass(typeof(RevitLinkInstance)))
            {
                var instance = (RevitLinkInstance)element;
                Document linkDoc = null;
                Transform transform = null;
                try
                {
                    linkDoc = instance.GetLinkDocument();
                    transform = instance.GetTotalTransform();
                }
                catch { }
                if (linkDoc == null || transform == null) continue;

                _links.Add(new LinkContext
                {
                    Instance = instance,
                    Document = linkDoc,
                    Transform = transform,
                    Inverse = transform.Inverse,
                });
            }
            return _links;
        }
    }
}
