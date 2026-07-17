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
    }
}
