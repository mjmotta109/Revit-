using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LinkWorksetInspector
{
    /// <summary>
    /// Dibuja los iconos de los botones en memoria con WPF, para no depender de
    /// archivos de imagen embebidos en el proyecto.
    /// </summary>
    internal static class RibbonIconFactory
    {
        /// <summary>Icono de vínculos: dos eslabones de cadena sobre fondo azul.</summary>
        public static ImageSource CreateLink(int size)
        {
            double s = size;
            var visual = new DrawingVisual();

            using (DrawingContext dc = visual.RenderOpen())
            {
                var background = new LinearGradientBrush(
                    Color.FromRgb(0x2E, 0x7D, 0xF0),
                    Color.FromRgb(0x0B, 0x3D, 0x91),
                    90.0);
                dc.DrawRoundedRectangle(background, null, new Rect(0, 0, s, s), s * 0.18, s * 0.18);

                var pen = new Pen(Brushes.White, Math.Max(1.5, s / 9.0))
                {
                    StartLineCap = PenLineCap.Round,
                    EndLineCap = PenLineCap.Round,
                };
                double radius = s * 0.14;

                // Dos eslabones entrelazados en diagonal.
                dc.PushTransform(new RotateTransform(-45.0, s / 2.0, s / 2.0));
                dc.DrawRoundedRectangle(null, pen, new Rect(s * 0.10, s * 0.34, s * 0.46, s * 0.32), radius, radius);
                dc.DrawRoundedRectangle(null, pen, new Rect(s * 0.44, s * 0.34, s * 0.46, s * 0.32), radius, radius);
                dc.Pop();
            }

            var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Freeze();
            return bitmap;
        }

        /// <summary>Icono del asistente IA: destello de cuatro puntas sobre fondo violeta.</summary>
        public static ImageSource CreateSpark(int size)
        {
            double s = size;
            var visual = new DrawingVisual();

            using (DrawingContext dc = visual.RenderOpen())
            {
                var background = new LinearGradientBrush(
                    Color.FromRgb(0x6A, 0x3D, 0xC8),
                    Color.FromRgb(0x35, 0x1B, 0x6E),
                    90.0);
                dc.DrawRoundedRectangle(background, null, new Rect(0, 0, s, s), s * 0.18, s * 0.18);

                dc.DrawGeometry(Brushes.White, null, SparkGeometry(s * 0.50, s * 0.52, s * 0.34));
                dc.DrawGeometry(Brushes.White, null, SparkGeometry(s * 0.76, s * 0.24, s * 0.12));
            }

            var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Freeze();
            return bitmap;
        }

        /// <summary>
        /// Icono de alzados por habitación: la planta de una estancia con cuatro
        /// puntas de mira apuntando a sus muros.
        /// </summary>
        public static ImageSource CreateRoomElevation(int size)
        {
            double s = size;
            var visual = new DrawingVisual();

            using (DrawingContext dc = visual.RenderOpen())
            {
                var background = new LinearGradientBrush(
                    Color.FromRgb(0x1F, 0x9E, 0x74),
                    Color.FromRgb(0x0C, 0x51, 0x3D),
                    90.0);
                dc.DrawRoundedRectangle(background, null, new Rect(0, 0, s, s), s * 0.18, s * 0.18);

                // Planta de la habitación.
                var pen = new Pen(Brushes.White, Math.Max(1.4, s / 11.0));
                dc.DrawRectangle(null, pen, new Rect(s * 0.20, s * 0.20, s * 0.60, s * 0.60));

                // Cuatro puntas de mira desde el centro hacia cada muro.
                double c = s * 0.50;
                double near = s * 0.13;
                double far = s * 0.28;
                double half = s * 0.09;
                dc.DrawGeometry(Brushes.White, null, Arrow(c, c - far, c - half, c - near, c + half, c - near));
                dc.DrawGeometry(Brushes.White, null, Arrow(c, c + far, c - half, c + near, c + half, c + near));
                dc.DrawGeometry(Brushes.White, null, Arrow(c - far, c, c - near, c - half, c - near, c + half));
                dc.DrawGeometry(Brushes.White, null, Arrow(c + far, c, c + near, c - half, c + near, c + half));
            }

            var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Freeze();
            return bitmap;
        }

        private static Geometry Arrow(double tipX, double tipY, double aX, double aY, double bX, double bY)
        {
            var geometry = new StreamGeometry();
            using (StreamGeometryContext ctx = geometry.Open())
            {
                ctx.BeginFigure(new Point(tipX, tipY), true, true);
                ctx.LineTo(new Point(aX, aY), true, false);
                ctx.LineTo(new Point(bX, bY), true, false);
            }
            geometry.Freeze();
            return geometry;
        }

        private static Geometry SparkGeometry(double cx, double cy, double radius)
        {
            double waist = radius * 0.22;
            var geometry = new StreamGeometry();
            using (StreamGeometryContext ctx = geometry.Open())
            {
                ctx.BeginFigure(new Point(cx, cy - radius), true, true);
                ctx.QuadraticBezierTo(new Point(cx + waist, cy - waist), new Point(cx + radius, cy), true, false);
                ctx.QuadraticBezierTo(new Point(cx + waist, cy + waist), new Point(cx, cy + radius), true, false);
                ctx.QuadraticBezierTo(new Point(cx - waist, cy + waist), new Point(cx - radius, cy), true, false);
                ctx.QuadraticBezierTo(new Point(cx - waist, cy - waist), new Point(cx, cy - radius), true, false);
            }
            geometry.Freeze();
            return geometry;
        }
    }
}
