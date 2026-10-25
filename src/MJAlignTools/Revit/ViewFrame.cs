using System.Collections.Generic;
using Autodesk.Revit.DB;
using MJAlignTools.Core;

namespace MJAlignTools.Revit
{
    /// <summary>
    /// Sistema de coordenadas 2D de la vista activa: U = derecha de la pantalla,
    /// V = arriba. Así "izquierda" y "arriba" significan lo mismo que en Illustrator:
    /// lo que ves en pantalla, también en plantas con la región de recorte girada y
    /// en alzados y secciones (donde "arriba" es la cota Z).
    /// </summary>
    internal sealed class ViewFrame
    {
        public View View { get; }
        public XYZ Right { get; }
        public XYZ Up { get; }

        /// <summary>true en vistas 3D: se alinea en planta, con los ejes X/Y del proyecto.</summary>
        public bool UsesPlanAxes { get; }

        private ViewFrame(View view, XYZ right, XYZ up, bool usesPlanAxes)
        {
            View = view;
            Right = right;
            Up = up;
            UsesPlanAxes = usesPlanAxes;
        }

        public static ViewFrame For(View view)
        {
            XYZ right = null;
            XYZ up = null;
            if (!(view is View3D))
            {
                try
                {
                    right = view.RightDirection;
                    up = view.UpDirection;
                }
                catch { }
            }

            if (right == null || up == null || right.IsZeroLength() || up.IsZeroLength())
                return new ViewFrame(view, XYZ.BasisX, XYZ.BasisY, usesPlanAxes: true);

            return new ViewFrame(view, right.Normalize(), up.Normalize(), usesPlanAxes: false);
        }

        public double U(XYZ point) => point.DotProduct(Right);
        public double V(XYZ point) => point.DotProduct(Up);

        /// <summary>Convierte un desplazamiento de la vista en un vector del modelo.</summary>
        public XYZ ToModel(Offset2 offset) => Right.Multiply(offset.U).Add(Up.Multiply(offset.V));
    }

    /// <summary>Acumula puntos del modelo y devuelve su rectángulo en coordenadas de la vista.</summary>
    internal sealed class BoxBuilder
    {
        private readonly ViewFrame _frame;
        private double _minU = double.MaxValue, _minV = double.MaxValue;
        private double _maxU = double.MinValue, _maxV = double.MinValue;

        public BoxBuilder(ViewFrame frame)
        {
            _frame = frame;
        }

        public bool IsEmpty { get; private set; } = true;

        public void Add(XYZ point)
        {
            if (point == null) return;
            double u = _frame.U(point);
            double v = _frame.V(point);
            if (double.IsNaN(u) || double.IsNaN(v) || double.IsInfinity(u) || double.IsInfinity(v)) return;

            if (u < _minU) _minU = u;
            if (u > _maxU) _maxU = u;
            if (v < _minV) _minV = v;
            if (v > _maxV) _maxV = v;
            IsEmpty = false;
        }

        public void AddRange(IEnumerable<XYZ> points)
        {
            if (points == null) return;
            foreach (XYZ point in points) Add(point);
        }

        /// <summary>Añade las 8 esquinas de una caja, aplicando su transformación si la tiene.</summary>
        public void AddBoxCorners(XYZ min, XYZ max, Transform transform)
        {
            if (min == null || max == null) return;
            foreach (double x in new[] { min.X, max.X })
            foreach (double y in new[] { min.Y, max.Y })
            foreach (double z in new[] { min.Z, max.Z })
            {
                var corner = new XYZ(x, y, z);
                Add(transform != null ? transform.OfPoint(corner) : corner);
            }
        }

        public Box2 ToBox() => new Box2(_minU, _minV, _maxU, _maxV);
    }
}
