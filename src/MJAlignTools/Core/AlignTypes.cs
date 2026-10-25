using System;
using System.Collections.Generic;

namespace MJAlignTools.Core
{
    /// <summary>Eje de la vista sobre el que actúa una operación.</summary>
    public enum Axis
    {
        /// <summary>Izquierda ↔ derecha de la pantalla (U).</summary>
        Horizontal,

        /// <summary>Abajo ↔ arriba de la pantalla (V).</summary>
        Vertical,
    }

    /// <summary>Punto del rectángulo que se toma como referencia en un eje.</summary>
    public enum Anchor
    {
        /// <summary>Borde izquierdo (horizontal) o inferior (vertical).</summary>
        Min,
        Center,

        /// <summary>Borde derecho (horizontal) o superior (vertical).</summary>
        Max,
    }

    /// <summary>A qué se alinea la selección (equivalente a "Alinear a" de Illustrator).</summary>
    public enum ReferenceMode
    {
        /// <summary>Al rectángulo que envuelve toda la selección.</summary>
        Selection,

        /// <summary>A un elemento elegido que no se mueve.</summary>
        KeyObject,

        /// <summary>A la habitación en la que está cada elemento (equivalente a la mesa de trabajo).</summary>
        Room,
    }

    public enum LayoutKind
    {
        Align,
        Distribute,
        Spacing,
    }

    /// <summary>Los 14 botones del panel, en el mismo orden que el panel Alinear de Illustrator.</summary>
    public enum AlignCommand
    {
        AlignLeft,
        AlignHorizontalCenter,
        AlignRight,
        AlignTop,
        AlignVerticalCenter,
        AlignBottom,

        DistributeTop,
        DistributeVerticalCenter,
        DistributeBottom,
        DistributeLeft,
        DistributeHorizontalCenter,
        DistributeRight,

        SpaceVertical,
        SpaceHorizontal,
    }

    /// <summary>Rectángulo en el plano de la vista: U hacia la derecha, V hacia arriba.</summary>
    public struct Box2
    {
        public double MinU { get; }
        public double MinV { get; }
        public double MaxU { get; }
        public double MaxV { get; }

        public Box2(double minU, double minV, double maxU, double maxV)
        {
            MinU = Math.Min(minU, maxU);
            MaxU = Math.Max(minU, maxU);
            MinV = Math.Min(minV, maxV);
            MaxV = Math.Max(minV, maxV);
        }

        public double Width => MaxU - MinU;
        public double Height => MaxV - MinV;
        public double CenterU => (MinU + MaxU) / 2.0;
        public double CenterV => (MinV + MaxV) / 2.0;

        public double Min(Axis axis) => axis == Axis.Horizontal ? MinU : MinV;
        public double Max(Axis axis) => axis == Axis.Horizontal ? MaxU : MaxV;
        public double Center(Axis axis) => axis == Axis.Horizontal ? CenterU : CenterV;
        public double Size(Axis axis) => axis == Axis.Horizontal ? Width : Height;

        public double Get(Axis axis, Anchor anchor)
        {
            switch (anchor)
            {
                case Anchor.Min: return Min(axis);
                case Anchor.Max: return Max(axis);
                default: return Center(axis);
            }
        }

        public Box2 Union(Box2 other)
        {
            return new Box2(
                Math.Min(MinU, other.MinU), Math.Min(MinV, other.MinV),
                Math.Max(MaxU, other.MaxU), Math.Max(MaxV, other.MaxV));
        }

        public static Box2 Union(IEnumerable<Box2> boxes)
        {
            bool any = false;
            Box2 result = default;
            foreach (Box2 box in boxes)
            {
                result = any ? result.Union(box) : box;
                any = true;
            }
            if (!any) throw new ArgumentException("Se necesita al menos un rectángulo.", nameof(boxes));
            return result;
        }

        public override string ToString() => $"[{MinU:0.###}, {MinV:0.###}] – [{MaxU:0.###}, {MaxV:0.###}]";
    }

    /// <summary>Desplazamiento en el plano de la vista.</summary>
    public struct Offset2
    {
        public double U { get; }
        public double V { get; }

        public Offset2(double u, double v)
        {
            U = u;
            V = v;
        }

        public bool IsNegligible => Math.Abs(U) < AlignEngine.Tolerance && Math.Abs(V) < AlignEngine.Tolerance;

        public override string ToString() => $"({U:0.###}, {V:0.###})";
    }

    /// <summary>Clasificación de cada botón: tipo de operación, eje y borde de referencia.</summary>
    public static class AlignCommands
    {
        public static LayoutKind KindOf(AlignCommand command)
        {
            switch (command)
            {
                case AlignCommand.AlignLeft:
                case AlignCommand.AlignHorizontalCenter:
                case AlignCommand.AlignRight:
                case AlignCommand.AlignTop:
                case AlignCommand.AlignVerticalCenter:
                case AlignCommand.AlignBottom:
                    return LayoutKind.Align;
                case AlignCommand.SpaceVertical:
                case AlignCommand.SpaceHorizontal:
                    return LayoutKind.Spacing;
                default:
                    return LayoutKind.Distribute;
            }
        }

        public static Axis AxisOf(AlignCommand command)
        {
            switch (command)
            {
                case AlignCommand.AlignLeft:
                case AlignCommand.AlignHorizontalCenter:
                case AlignCommand.AlignRight:
                case AlignCommand.DistributeLeft:
                case AlignCommand.DistributeHorizontalCenter:
                case AlignCommand.DistributeRight:
                case AlignCommand.SpaceHorizontal:
                    return Axis.Horizontal;
                default:
                    return Axis.Vertical;
            }
        }

        public static Anchor AnchorOf(AlignCommand command)
        {
            switch (command)
            {
                case AlignCommand.AlignRight:
                case AlignCommand.AlignTop:
                case AlignCommand.DistributeTop:
                case AlignCommand.DistributeRight:
                    return Anchor.Max;
                case AlignCommand.AlignHorizontalCenter:
                case AlignCommand.AlignVerticalCenter:
                case AlignCommand.DistributeVerticalCenter:
                case AlignCommand.DistributeHorizontalCenter:
                    return Anchor.Center;
                default:
                    return Anchor.Min;
            }
        }

        /// <summary>Nombre corto, usado en la información sobre herramientas y en Deshacer.</summary>
        public static string Describe(AlignCommand command)
        {
            switch (command)
            {
                case AlignCommand.AlignLeft: return "Alinear a la izquierda";
                case AlignCommand.AlignHorizontalCenter: return "Alinear al centro horizontal";
                case AlignCommand.AlignRight: return "Alinear a la derecha";
                case AlignCommand.AlignTop: return "Alinear arriba";
                case AlignCommand.AlignVerticalCenter: return "Alinear al centro vertical";
                case AlignCommand.AlignBottom: return "Alinear abajo";
                case AlignCommand.DistributeTop: return "Distribuir bordes superiores";
                case AlignCommand.DistributeVerticalCenter: return "Distribuir centros verticales";
                case AlignCommand.DistributeBottom: return "Distribuir bordes inferiores";
                case AlignCommand.DistributeLeft: return "Distribuir bordes izquierdos";
                case AlignCommand.DistributeHorizontalCenter: return "Distribuir centros horizontales";
                case AlignCommand.DistributeRight: return "Distribuir bordes derechos";
                case AlignCommand.SpaceVertical: return "Distribuir espaciado vertical";
                case AlignCommand.SpaceHorizontal: return "Distribuir espaciado horizontal";
                default: return command.ToString();
            }
        }
    }
}
