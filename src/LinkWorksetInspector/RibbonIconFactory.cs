using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LinkWorksetInspector
{
    /// <summary>
    /// Dibuja el icono del botón (dos eslabones de cadena) en memoria con WPF,
    /// para no depender de archivos de imagen embebidos en el proyecto.
    /// </summary>
    internal static class RibbonIconFactory
    {
        public static ImageSource Create(int size)
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
