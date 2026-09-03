using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace QuickDimension
{
    /// <summary>
    /// Dibuja el icono del botón (una línea de cota con sus marcas) en memoria con WPF,
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

                var pen = new Pen(Brushes.White, Math.Max(1.2, s / 13.0))
                {
                    StartLineCap = PenLineCap.Round,
                    EndLineCap = PenLineCap.Round,
                };

                double axis = s * 0.62;
                double left = s * 0.14;
                double right = s * 0.86;

                // Línea de cota.
                dc.DrawLine(pen, new Point(left, axis), new Point(right, axis));

                // Líneas de referencia y marcas de los extremos y del punto intermedio.
                double[] ticks = { left, s * 0.46, right };
                foreach (double x in ticks)
                {
                    dc.DrawLine(pen, new Point(x, s * 0.30), new Point(x, s * 0.74));
                }

                // Trazo diagonal que representa la línea que dibuja el usuario.
                var stroke = new Pen(new SolidColorBrush(Color.FromRgb(0xFF, 0xD5, 0x4F)), Math.Max(1.2, s / 13.0))
                {
                    StartLineCap = PenLineCap.Round,
                    EndLineCap = PenLineCap.Round,
                };
                dc.DrawLine(stroke, new Point(s * 0.18, s * 0.24), new Point(s * 0.82, s * 0.24));
            }

            var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Freeze();
            return bitmap;
        }
    }
}
