using System;
using System.Collections.Generic;
using System.Linq;

namespace MJAlignTools.Core
{
    /// <summary>
    /// Cálculo puro (sin Revit) de cuánto hay que mover cada rectángulo para
    /// alinear, distribuir o espaciar. Trabaja en el plano de la vista (U, V).
    ///
    /// Reglas según la referencia:
    /// <list type="bullet">
    /// <item>Selección: alinea al rectángulo de toda la selección; al distribuir, los
    /// elementos de los extremos no se mueven.</item>
    /// <item>Objeto clave (<c>keyIndex</c>): nunca se mueve; alinea a él y, al distribuir
    /// o espaciar, el resto se coloca a partir de él con paso o separación iguales.</item>
    /// <item>Habitación (<c>container</c>): alinea a los bordes del contenedor; al distribuir,
    /// los extremos se apoyan en sus bordes; al espaciar en automático deja el mismo
    /// hueco entre elementos y contra los bordes, y con una distancia fija centra el
    /// conjunto en el contenedor.</item>
    /// </list>
    /// </summary>
    public static class AlignEngine
    {
        /// <summary>Desplazamientos menores que esto (en pies, unidad interna de Revit) se ignoran.</summary>
        public const double Tolerance = 1e-9;

        /// <summary>
        /// Número mínimo de rectángulos (incluido el objeto clave, si lo hay) para que
        /// la operación tenga efecto.
        /// </summary>
        public static int MinimumItems(AlignCommand command, ReferenceMode mode, bool hasFixedSpacing)
        {
            if (mode == ReferenceMode.Room) return 1;

            switch (AlignCommands.KindOf(command))
            {
                case LayoutKind.Align: return 2;
                case LayoutKind.Distribute: return 3;
                default: return hasFixedSpacing ? 2 : 3;
            }
        }

        /// <param name="command">Botón pulsado.</param>
        /// <param name="boxes">Rectángulos de los elementos, en coordenadas de la vista.</param>
        /// <param name="mode">Referencia de alineación.</param>
        /// <param name="keyIndex">Índice del objeto clave dentro de <paramref name="boxes"/> (modo objeto clave).</param>
        /// <param name="container">Rectángulo de la habitación (modo habitación).</param>
        /// <param name="spacing">Separación fija entre elementos; null = automática.</param>
        /// <returns>Un desplazamiento por rectángulo, en el mismo orden.</returns>
        public static Offset2[] Compute(AlignCommand command, IReadOnlyList<Box2> boxes, ReferenceMode mode,
            int keyIndex = -1, Box2? container = null, double? spacing = null)
        {
            if (boxes == null) throw new ArgumentNullException(nameof(boxes));
            if (mode == ReferenceMode.KeyObject && (keyIndex < 0 || keyIndex >= boxes.Count))
                throw new ArgumentOutOfRangeException(nameof(keyIndex), "El modo objeto clave necesita un índice válido.");
            if (mode == ReferenceMode.Room && !container.HasValue)
                throw new ArgumentNullException(nameof(container), "El modo habitación necesita el rectángulo de la habitación.");
            if (mode != ReferenceMode.KeyObject) keyIndex = -1;

            var result = new Offset2[boxes.Count];
            if (boxes.Count < MinimumItems(command, mode, spacing.HasValue)) return result;

            Axis axis = AlignCommands.AxisOf(command);
            Anchor anchor = AlignCommands.AnchorOf(command);

            double[] deltas;
            switch (AlignCommands.KindOf(command))
            {
                case LayoutKind.Align:
                    deltas = Align(boxes, axis, anchor, mode, keyIndex, container);
                    break;
                case LayoutKind.Distribute:
                    deltas = Distribute(boxes, axis, anchor, mode, keyIndex, container);
                    break;
                default:
                    deltas = Space(boxes, axis, mode, keyIndex, container, spacing);
                    break;
            }

            for (int i = 0; i < boxes.Count; i++)
            {
                if (i == keyIndex) continue;
                result[i] = axis == Axis.Horizontal ? new Offset2(deltas[i], 0) : new Offset2(0, deltas[i]);
            }
            return result;
        }

        private static double[] Align(IReadOnlyList<Box2> boxes, Axis axis, Anchor anchor,
            ReferenceMode mode, int keyIndex, Box2? container)
        {
            Box2 reference;
            switch (mode)
            {
                case ReferenceMode.KeyObject: reference = boxes[keyIndex]; break;
                case ReferenceMode.Room: reference = container.Value; break;
                default: reference = Box2.Union(boxes); break;
            }

            double target = reference.Get(axis, anchor);
            return boxes.Select(b => target - b.Get(axis, anchor)).ToArray();
        }

        /// <summary>Iguala el paso entre los bordes (o centros) elegidos.</summary>
        private static double[] Distribute(IReadOnlyList<Box2> boxes, Axis axis, Anchor anchor,
            ReferenceMode mode, int keyIndex, Box2? container)
        {
            int[] order = SortedIndices(boxes, axis, b => b.Get(axis, anchor));
            int n = order.Length;
            double[] current = order.Select(i => boxes[i].Get(axis, anchor)).ToArray();
            var target = new double[n];

            if (mode == ReferenceMode.Room)
            {
                // Los extremos se apoyan en los bordes de la habitación.
                Box2 first = boxes[order[0]];
                Box2 last = boxes[order[n - 1]];
                Box2 room = container.Value;
                double low = room.Min(axis) + (current[0] - first.Min(axis));
                double high = room.Max(axis) - (last.Max(axis) - current[n - 1]);

                if (n == 1)
                {
                    target[0] = (low + high) / 2.0;
                }
                else
                {
                    double pitch = (high - low) / (n - 1);
                    for (int k = 0; k < n; k++) target[k] = low + k * pitch;
                }
            }
            else
            {
                double pitch = (current[n - 1] - current[0]) / (n - 1);
                int fixedPos = mode == ReferenceMode.KeyObject ? Array.IndexOf(order, keyIndex) : 0;
                for (int k = 0; k < n; k++) target[k] = current[fixedPos] + (k - fixedPos) * pitch;
            }

            var deltas = new double[boxes.Count];
            for (int k = 0; k < n; k++) deltas[order[k]] = target[k] - current[k];
            return deltas;
        }

        /// <summary>Iguala el hueco libre entre elementos consecutivos.</summary>
        private static double[] Space(IReadOnlyList<Box2> boxes, Axis axis, ReferenceMode mode,
            int keyIndex, Box2? container, double? spacing)
        {
            int[] order = SortedIndices(boxes, axis, b => b.Center(axis));
            int n = order.Length;
            double[] sizes = order.Select(i => boxes[i].Size(axis)).ToArray();
            double[] mins = order.Select(i => boxes[i].Min(axis)).ToArray();
            double sum = sizes.Sum();
            double low = boxes.Min(b => b.Min(axis));
            double high = boxes.Max(b => b.Max(axis));

            double gap;
            int anchorPos;
            double anchorMin;

            switch (mode)
            {
                case ReferenceMode.KeyObject:
                    gap = spacing ?? (high - low - sum) / (n - 1);
                    anchorPos = Array.IndexOf(order, keyIndex);
                    anchorMin = mins[anchorPos];
                    break;

                case ReferenceMode.Room:
                    Box2 room = container.Value;
                    anchorPos = 0;
                    if (spacing.HasValue)
                    {
                        gap = spacing.Value;
                        anchorMin = room.Center(axis) - (sum + (n - 1) * gap) / 2.0;
                    }
                    else
                    {
                        gap = (room.Size(axis) - sum) / (n + 1);
                        anchorMin = room.Min(axis) + gap;
                    }
                    break;

                default:
                    if (spacing.HasValue)
                    {
                        // Con distancia fija se queda quieto el primero en orden de lectura:
                        // el de la izquierda (horizontal) o el de arriba (vertical).
                        gap = spacing.Value;
                        anchorPos = axis == Axis.Vertical ? n - 1 : 0;
                        anchorMin = mins[anchorPos];
                    }
                    else
                    {
                        gap = (high - low - sum) / (n - 1);
                        anchorPos = 0;
                        anchorMin = low;
                    }
                    break;
            }

            var newMins = new double[n];
            newMins[anchorPos] = anchorMin;
            for (int k = anchorPos + 1; k < n; k++) newMins[k] = newMins[k - 1] + sizes[k - 1] + gap;
            for (int k = anchorPos - 1; k >= 0; k--) newMins[k] = newMins[k + 1] - gap - sizes[k];

            var deltas = new double[boxes.Count];
            for (int k = 0; k < n; k++) deltas[order[k]] = newMins[k] - mins[k];
            return deltas;
        }

        /// <summary>
        /// Ordena por la coordenada indicada; a igualdad, por la posición en el otro eje
        /// y por último por el orden original, para que el resultado sea estable.
        /// </summary>
        private static int[] SortedIndices(IReadOnlyList<Box2> boxes, Axis axis, Func<Box2, double> key)
        {
            Axis other = axis == Axis.Horizontal ? Axis.Vertical : Axis.Horizontal;
            return Enumerable.Range(0, boxes.Count)
                .OrderBy(i => key(boxes[i]))
                .ThenBy(i => boxes[i].Center(other))
                .ThenBy(i => i)
                .ToArray();
        }
    }
}
