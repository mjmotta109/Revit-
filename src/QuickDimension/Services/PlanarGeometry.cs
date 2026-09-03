using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace QuickDimension.Services
{
    /// <summary>
    /// Geometría 2D en el plano de la vista. Proyectando sobre los ejes derecha/arriba de
    /// la vista, "¿la línea toca este elemento?" se resuelve sin preocuparse de la altura
    /// a la que el usuario haya picado los puntos.
    /// </summary>
    internal static class PlanarGeometry
    {
        /// <summary>Proyecta un punto del modelo sobre el plano de la vista.</summary>
        public static UV ToPlane(XYZ point, XYZ right, XYZ up)
        {
            return new UV(point.DotProduct(right), point.DotProduct(up));
        }

        /// <summary>Distancia entre dos segmentos; cero si se cruzan.</summary>
        public static double SegmentDistance(UV a, UV b, UV c, UV d)
        {
            if (SegmentsIntersect(a, b, c, d)) return 0.0;

            double best = PointSegmentDistance(a, c, d);
            best = Math.Min(best, PointSegmentDistance(b, c, d));
            best = Math.Min(best, PointSegmentDistance(c, a, b));
            best = Math.Min(best, PointSegmentDistance(d, a, b));
            return best;
        }

        public static double PointSegmentDistance(UV p, UV a, UV b)
        {
            double dx = b.U - a.U;
            double dy = b.V - a.V;
            double lengthSquared = dx * dx + dy * dy;

            if (lengthSquared < 1e-18)
            {
                return Length(p.U - a.U, p.V - a.V);
            }

            double t = ((p.U - a.U) * dx + (p.V - a.V) * dy) / lengthSquared;
            if (t < 0.0) t = 0.0;
            else if (t > 1.0) t = 1.0;

            return Length(p.U - (a.U + t * dx), p.V - (a.V + t * dy));
        }

        public static bool SegmentsIntersect(UV a, UV b, UV c, UV d)
        {
            double d1 = Cross(c, d, a);
            double d2 = Cross(c, d, b);
            double d3 = Cross(a, b, c);
            double d4 = Cross(a, b, d);

            if (((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) &&
                ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0)))
            {
                return true;
            }

            // Casos degenerados: un extremo apoyado justo sobre el otro segmento.
            if (IsZero(d1) && OnSegment(c, d, a)) return true;
            if (IsZero(d2) && OnSegment(c, d, b)) return true;
            if (IsZero(d3) && OnSegment(a, b, c)) return true;
            if (IsZero(d4) && OnSegment(a, b, d)) return true;

            return false;
        }

        /// <summary>
        /// ¿El segmento corta el rectángulo? Ejes de separación: X, Y y la normal del segmento.
        /// El rectángulo viene dado en coordenadas del plano de la vista.
        /// </summary>
        public static bool SegmentIntersectsRectangle(UV a, UV b, double minU, double minV, double maxU, double maxV)
        {
            if (Math.Max(a.U, b.U) < minU || Math.Min(a.U, b.U) > maxU) return false;
            if (Math.Max(a.V, b.V) < minV || Math.Min(a.V, b.V) > maxV) return false;

            double nu = -(b.V - a.V);
            double nv = b.U - a.U;
            if (Math.Abs(nu) < 1e-12 && Math.Abs(nv) < 1e-12) return true;

            double onLine = nu * a.U + nv * a.V;
            double lo = double.MaxValue;
            double hi = double.MinValue;

            foreach (UV corner in Corners(minU, minV, maxU, maxV))
            {
                double projected = nu * corner.U + nv * corner.V;
                if (projected < lo) lo = projected;
                if (projected > hi) hi = projected;
            }

            return onLine >= lo && onLine <= hi;
        }

        /// <summary>Rectángulo envolvente, en el plano de la vista, de una caja de contorno del modelo.</summary>
        public static bool TryGetPlanarBounds(BoundingBoxXYZ box, XYZ right, XYZ up,
            out double minU, out double minV, out double maxU, out double maxV)
        {
            minU = minV = maxU = maxV = 0.0;
            if (box == null || box.Min == null || box.Max == null) return false;

            Transform transform = box.Transform;
            bool first = true;

            for (int i = 0; i < 8; i++)
            {
                var corner = new XYZ(
                    (i & 1) == 0 ? box.Min.X : box.Max.X,
                    (i & 2) == 0 ? box.Min.Y : box.Max.Y,
                    (i & 4) == 0 ? box.Min.Z : box.Max.Z);

                if (transform != null) corner = transform.OfPoint(corner);
                UV projected = ToPlane(corner, right, up);

                if (first)
                {
                    minU = maxU = projected.U;
                    minV = maxV = projected.V;
                    first = false;
                    continue;
                }

                if (projected.U < minU) minU = projected.U;
                if (projected.U > maxU) maxU = projected.U;
                if (projected.V < minV) minV = projected.V;
                if (projected.V > maxV) maxV = projected.V;
            }

            return !first;
        }

        private static IEnumerable<UV> Corners(double minU, double minV, double maxU, double maxV)
        {
            yield return new UV(minU, minV);
            yield return new UV(maxU, minV);
            yield return new UV(maxU, maxV);
            yield return new UV(minU, maxV);
        }

        private static double Cross(UV from, UV to, UV point)
        {
            return (to.U - from.U) * (point.V - from.V) - (to.V - from.V) * (point.U - from.U);
        }

        private static bool OnSegment(UV a, UV b, UV p)
        {
            return p.U >= Math.Min(a.U, b.U) - 1e-9 && p.U <= Math.Max(a.U, b.U) + 1e-9
                && p.V >= Math.Min(a.V, b.V) - 1e-9 && p.V <= Math.Max(a.V, b.V) + 1e-9;
        }

        private static bool IsZero(double value)
        {
            return Math.Abs(value) < 1e-12;
        }

        private static double Length(double dx, double dy)
        {
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
