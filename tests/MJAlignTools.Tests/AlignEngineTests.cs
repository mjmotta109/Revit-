using System.Linq;
using MJAlignTools.Core;
using Xunit;

namespace MJAlignTools.Tests
{
    public class AlignEngineTests
    {
        private const int Precision = 9;

        private static Box2 B(double minU, double minV, double maxU, double maxV) => new Box2(minU, minV, maxU, maxV);

        private static void AssertU(Offset2[] offsets, params double[] expected)
        {
            Assert.Equal(expected.Length, offsets.Length);
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.Equal(expected[i], offsets[i].U, Precision);
                Assert.Equal(0.0, offsets[i].V, Precision);
            }
        }

        private static void AssertV(Offset2[] offsets, params double[] expected)
        {
            Assert.Equal(expected.Length, offsets.Length);
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.Equal(0.0, offsets[i].U, Precision);
                Assert.Equal(expected[i], offsets[i].V, Precision);
            }
        }

        // ---------- Alinear ----------

        [Fact]
        public void AlignLeft_ToSelection_UsesLeftmostEdge()
        {
            var boxes = new[] { B(0, 0, 2, 1), B(3, 5, 4, 6), B(1, 2, 5, 3) };
            AssertU(AlignEngine.Compute(AlignCommand.AlignLeft, boxes, ReferenceMode.Selection), 0, -3, -1);
        }

        [Fact]
        public void AlignRight_ToSelection_UsesRightmostEdge()
        {
            var boxes = new[] { B(0, 0, 2, 1), B(3, 5, 4, 6), B(1, 2, 5, 3) };
            AssertU(AlignEngine.Compute(AlignCommand.AlignRight, boxes, ReferenceMode.Selection), 3, 1, 0);
        }

        [Fact]
        public void AlignHorizontalCenter_ToSelection_UsesCenterOfWholeSelection()
        {
            var boxes = new[] { B(0, 0, 2, 1), B(8, 0, 10, 1) };
            AssertU(AlignEngine.Compute(AlignCommand.AlignHorizontalCenter, boxes, ReferenceMode.Selection), 4, -4);
        }

        [Fact]
        public void AlignTop_ToKeyObject_KeyNeverMoves()
        {
            var boxes = new[] { B(0, 0, 1, 2), B(5, 4, 6, 6), B(2, 7, 3, 9) };
            AssertV(AlignEngine.Compute(AlignCommand.AlignTop, boxes, ReferenceMode.KeyObject, keyIndex: 1), 4, 0, -3);
        }

        [Fact]
        public void AlignBottom_ToKeyObject_KeyNeedNotBeExtreme()
        {
            var boxes = new[] { B(0, 0, 1, 2), B(5, 4, 6, 6), B(2, 7, 3, 9) };
            AssertV(AlignEngine.Compute(AlignCommand.AlignBottom, boxes, ReferenceMode.KeyObject, keyIndex: 1), 4, 0, -3);
        }

        [Fact]
        public void AlignVerticalCenter_ToRoom_CentersEachElementInRoom()
        {
            var boxes = new[] { B(1, 1, 3, 2) };
            AssertV(AlignEngine.Compute(AlignCommand.AlignVerticalCenter, boxes, ReferenceMode.Room,
                container: B(0, 0, 10, 10)), 3.5);
        }

        [Fact]
        public void AlignLeft_ToRoom_WorksWithSingleElement()
        {
            var boxes = new[] { B(4, 4, 6, 5) };
            AssertU(AlignEngine.Compute(AlignCommand.AlignLeft, boxes, ReferenceMode.Room,
                container: B(1, 0, 10, 10)), -3);
        }

        [Fact]
        public void AlignToSelection_WithSingleElement_DoesNothing()
        {
            var boxes = new[] { B(4, 4, 6, 5) };
            AssertU(AlignEngine.Compute(AlignCommand.AlignLeft, boxes, ReferenceMode.Selection), 0);
        }

        // ---------- Distribuir objetos ----------

        [Fact]
        public void DistributeLeft_ToSelection_EqualPitchAndExtremesStay()
        {
            // Desordenados a propósito: el orden lo decide la posición, no la selección.
            var boxes = new[] { B(10, 0, 11, 1), B(0, 0, 3, 1), B(1, 0, 2, 1) };
            AssertU(AlignEngine.Compute(AlignCommand.DistributeLeft, boxes, ReferenceMode.Selection), 0, 0, 4);
        }

        [Fact]
        public void DistributeRight_UsesRightEdges()
        {
            var boxes = new[] { B(0, 0, 2, 1), B(5, 0, 6, 1), B(8, 0, 12, 1) };
            // Bordes derechos 2, 6, 12 → 2, 7, 12
            AssertU(AlignEngine.Compute(AlignCommand.DistributeRight, boxes, ReferenceMode.Selection), 0, 1, 0);
        }

        [Fact]
        public void DistributeTop_UsesTopEdgesOnVerticalAxis()
        {
            var boxes = new[] { B(0, 0, 1, 1), B(0, 1, 1, 2), B(0, 9, 1, 10) };
            // Bordes superiores 1, 2, 10 → 1, 5.5, 10
            AssertV(AlignEngine.Compute(AlignCommand.DistributeTop, boxes, ReferenceMode.Selection), 0, 3.5, 0);
        }

        [Fact]
        public void DistributeObjects_ToSelection_NeedsThreeElements()
        {
            var boxes = new[] { B(0, 0, 1, 1), B(5, 0, 6, 1) };
            AssertU(AlignEngine.Compute(AlignCommand.DistributeLeft, boxes, ReferenceMode.Selection), 0, 0);
        }

        [Fact]
        public void DistributeHorizontalCenter_KeyInTheMiddle_KeepsKeyAndPitch()
        {
            // Centros 0, 3, 10 → paso 5; la clave (centro 3) no se mueve → -2, 3, 8
            var boxes = new[] { B(-1, 0, 1, 1), B(2, 0, 4, 1), B(9, 0, 11, 1) };
            AssertU(AlignEngine.Compute(AlignCommand.DistributeHorizontalCenter, boxes, ReferenceMode.KeyObject, keyIndex: 1),
                -2, 0, -2);
        }

        [Fact]
        public void DistributeLeft_InRoom_ExtremesTouchWalls()
        {
            var boxes = new[] { B(1, 0, 3, 1), B(2, 0, 4, 1), B(3, 0, 5, 1) };
            // Habitación 0..12, anchos 2 → izquierdas 0, 5, 10
            AssertU(AlignEngine.Compute(AlignCommand.DistributeLeft, boxes, ReferenceMode.Room, container: B(0, 0, 12, 5)),
                -1, 3, 7);
        }

        [Fact]
        public void DistributeHorizontalCenter_InRoom_SingleElementIsCentered()
        {
            var boxes = new[] { B(1, 0, 3, 1) };
            AssertU(AlignEngine.Compute(AlignCommand.DistributeHorizontalCenter, boxes, ReferenceMode.Room,
                container: B(0, 0, 10, 5)), 3);
        }

        // ---------- Distribuir espaciado ----------

        [Fact]
        public void SpaceHorizontal_Auto_ToSelection_EqualGapsExtremesStay()
        {
            // Tamaños 2, 1, 1 en un tramo de 10 → hueco 3 → mínimos 0, 5, 9
            var boxes = new[] { B(0, 0, 2, 1), B(3, 0, 4, 1), B(9, 0, 10, 1) };
            AssertU(AlignEngine.Compute(AlignCommand.SpaceHorizontal, boxes, ReferenceMode.Selection), 0, 2, 0);
        }

        [Fact]
        public void SpaceHorizontal_FixedValue_ToSelection_LeftmostStays()
        {
            var boxes = new[] { B(0, 0, 2, 1), B(3, 0, 4, 1), B(9, 0, 10, 1) };
            // Hueco 0.5 → mínimos 0, 2.5, 4
            AssertU(AlignEngine.Compute(AlignCommand.SpaceHorizontal, boxes, ReferenceMode.Selection, spacing: 0.5),
                0, -0.5, -5);
        }

        [Fact]
        public void SpaceVertical_FixedValue_ToSelection_TopmostStays()
        {
            var boxes = new[] { B(0, 0, 1, 1), B(0, 2, 1, 4), B(0, 10, 1, 11) };
            // El de arriba (10..11) se queda; hueco 1 → el central baja a 7..9 y el inferior a 5..6
            AssertV(AlignEngine.Compute(AlignCommand.SpaceVertical, boxes, ReferenceMode.Selection, spacing: 1),
                5, 5, 0);
        }

        [Fact]
        public void SpaceHorizontal_FixedValue_WorksWithTwoElements()
        {
            var boxes = new[] { B(0, 0, 2, 1), B(7, 0, 8, 1) };
            AssertU(AlignEngine.Compute(AlignCommand.SpaceHorizontal, boxes, ReferenceMode.Selection, spacing: 1), 0, -4);
        }

        [Fact]
        public void SpaceHorizontal_FixedValue_FromKeyInTheMiddle()
        {
            var boxes = new[] { B(0, 0, 2, 1), B(5, 0, 6, 1), B(9, 0, 10, 1) };
            // Clave en 5..6, hueco 0.5 → izquierdo a 2.5..4.5, derecho a 6.5..7.5
            AssertU(AlignEngine.Compute(AlignCommand.SpaceHorizontal, boxes, ReferenceMode.KeyObject, keyIndex: 1, spacing: 0.5),
                2.5, 0, -2.5);
        }

        [Fact]
        public void SpaceHorizontal_Auto_FromKey_UsesEqualGapAroundKey()
        {
            // Tramo 0..12, tamaños 2+2+2 → hueco 3; la clave (8..10) queda fija
            var boxes = new[] { B(0, 0, 2, 1), B(8, 0, 10, 1), B(10, 0, 12, 1) };
            var offsets = AlignEngine.Compute(AlignCommand.SpaceHorizontal, boxes, ReferenceMode.KeyObject, keyIndex: 1);
            // Izquierdo: 8 - 3 - 2 = 3 → +3 ; derecho: 10 + 3 = 13 → +3
            AssertU(offsets, 3, 0, 3);
        }

        [Fact]
        public void SpaceHorizontal_Auto_InRoom_SameGapAgainstWalls()
        {
            var boxes = new[] { B(0, 0, 2, 1), B(3, 0, 5, 1) };
            // Habitación 0..10, tamaños 2+2 → hueco (10-4)/3 = 2 → mínimos 2 y 6
            AssertU(AlignEngine.Compute(AlignCommand.SpaceHorizontal, boxes, ReferenceMode.Room, container: B(0, 0, 10, 10)),
                2, 3);
        }

        [Fact]
        public void SpaceHorizontal_FixedValue_InRoom_CentersTheRow()
        {
            var boxes = new[] { B(0, 0, 2, 1), B(3, 0, 5, 1) };
            // Total 2 + 1 + 2 = 5 centrado en 0..10 → mínimos 2.5 y 5.5
            AssertU(AlignEngine.Compute(AlignCommand.SpaceHorizontal, boxes, ReferenceMode.Room, container: B(0, 0, 10, 10),
                spacing: 1), 2.5, 2.5);
        }

        [Fact]
        public void Spacing_PreservesSizesAndProducesExactGaps()
        {
            var boxes = new[] { B(0, 0, 1.3, 1), B(2, 0, 2.2, 1), B(2.5, 0, 4, 1), B(9, 0, 9.7, 1) };
            var offsets = AlignEngine.Compute(AlignCommand.SpaceHorizontal, boxes, ReferenceMode.Selection);
            var moved = boxes.Select((b, i) => B(b.MinU + offsets[i].U, b.MinV, b.MaxU + offsets[i].U, b.MaxV))
                .OrderBy(b => b.MinU).ToArray();
            double gap = moved[1].MinU - moved[0].MaxU;
            for (int i = 1; i < moved.Length; i++)
                Assert.Equal(gap, moved[i].MinU - moved[i - 1].MaxU, Precision);
            Assert.Equal(0.0, moved[0].MinU, Precision);
            Assert.Equal(9.7, moved[3].MaxU, Precision);
        }

        // ---------- Clasificación de comandos ----------

        [Theory]
        [InlineData(AlignCommand.AlignLeft, Axis.Horizontal, Anchor.Min)]
        [InlineData(AlignCommand.AlignTop, Axis.Vertical, Anchor.Max)]
        [InlineData(AlignCommand.AlignBottom, Axis.Vertical, Anchor.Min)]
        [InlineData(AlignCommand.DistributeTop, Axis.Vertical, Anchor.Max)]
        [InlineData(AlignCommand.DistributeRight, Axis.Horizontal, Anchor.Max)]
        [InlineData(AlignCommand.DistributeHorizontalCenter, Axis.Horizontal, Anchor.Center)]
        public void Commands_MapToExpectedAxisAndAnchor(AlignCommand command, Axis axis, Anchor anchor)
        {
            Assert.Equal(axis, AlignCommands.AxisOf(command));
            Assert.Equal(anchor, AlignCommands.AnchorOf(command));
        }

        [Fact]
        public void KeyObjectMode_RequiresValidIndex()
        {
            var boxes = new[] { B(0, 0, 1, 1), B(2, 0, 3, 1) };
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                AlignEngine.Compute(AlignCommand.AlignLeft, boxes, ReferenceMode.KeyObject, keyIndex: 5));
        }
    }
}
