using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MJAlignTools.Core;

namespace MJAlignTools.UI
{
    /// <summary>
    /// Iconos del panel dibujados en vectores (24×24), con el mismo lenguaje visual
    /// que el panel Alinear de Illustrator: rectángulos grises y la línea de referencia.
    /// No dependen de archivos de imagen.
    /// </summary>
    internal static class AlignIcons
    {
        private const double Size = 24;

        private static readonly Brush Ink = Frozen(new SolidColorBrush(Color.FromRgb(0xCF, 0xCF, 0xCF)));
        private static readonly Pen Line = Frozen(new Pen(Ink, 1));
        private static readonly Pen Dashed = Frozen(new Pen(Ink, 1) { DashStyle = new DashStyle(new[] { 2.0, 2.0 }, 0) });
        private static readonly Pen Thick = Frozen(new Pen(Ink, 2));

        public static ImageSource For(AlignCommand command)
        {
            switch (command)
            {
                case AlignCommand.AlignLeft:
                    return Build(dc => { VLine(dc, 3, 2, 22); Rect(dc, 4, 5, 9, 5); Rect(dc, 4, 13, 14, 6); });
                case AlignCommand.AlignHorizontalCenter:
                    return Build(dc => { Rect(dc, 8, 5, 7, 5); Rect(dc, 5, 13, 13, 6); VLine(dc, 11, 2, 22); });
                case AlignCommand.AlignRight:
                    return Build(dc => { VLine(dc, 20, 2, 22); Rect(dc, 11, 5, 9, 5); Rect(dc, 6, 13, 14, 6); });
                case AlignCommand.AlignTop:
                    return Build(dc => { HLine(dc, 3, 2, 22); Rect(dc, 5, 4, 6, 14); Rect(dc, 13, 4, 6, 8); });
                case AlignCommand.AlignVerticalCenter:
                    return Build(dc => { Rect(dc, 5, 5, 6, 13); Rect(dc, 13, 8, 6, 7); HLine(dc, 11, 2, 22); });
                case AlignCommand.AlignBottom:
                    return Build(dc => { HLine(dc, 20, 2, 22); Rect(dc, 5, 6, 6, 14); Rect(dc, 13, 12, 6, 8); });

                case AlignCommand.DistributeTop:
                    return Build(dc => { HLine(dc, 4, 3, 21); Rect(dc, 7, 5, 10, 4); HLine(dc, 13, 3, 21); Rect(dc, 5, 14, 14, 5); });
                case AlignCommand.DistributeVerticalCenter:
                    return Build(dc => { Rect(dc, 7, 3, 10, 5); Rect(dc, 5, 14, 14, 5); HLine(dc, 5, 3, 21); HLine(dc, 16, 3, 21); });
                case AlignCommand.DistributeBottom:
                    return Build(dc => { Rect(dc, 7, 4, 10, 5); HLine(dc, 9, 3, 21); Rect(dc, 5, 13, 14, 6); HLine(dc, 19, 3, 21); });
                case AlignCommand.DistributeLeft:
                    return Build(dc => { VLine(dc, 3, 3, 21); Rect(dc, 4, 5, 5, 14); VLine(dc, 12, 3, 21); Rect(dc, 13, 8, 5, 8); });
                case AlignCommand.DistributeHorizontalCenter:
                    return Build(dc => { Rect(dc, 3, 5, 5, 14); Rect(dc, 14, 8, 5, 8); VLine(dc, 5, 3, 21); VLine(dc, 16, 3, 21); });
                case AlignCommand.DistributeRight:
                    return Build(dc => { Rect(dc, 4, 5, 6, 14); VLine(dc, 10, 3, 21); Rect(dc, 13, 8, 5, 8); VLine(dc, 18, 3, 21); });

                case AlignCommand.SpaceVertical:
                    return Build(dc =>
                    {
                        Rect(dc, 9, 3, 11, 6);
                        Rect(dc, 9, 15, 11, 6);
                        VLine(dc, 4, 9, 15);
                        HLine(dc, 9, 2, 7);
                        HLine(dc, 14, 2, 7);
                    });
                case AlignCommand.SpaceHorizontal:
                    return Build(dc =>
                    {
                        Rect(dc, 3, 9, 6, 11);
                        Rect(dc, 15, 9, 6, 11);
                        HLine(dc, 4, 9, 15);
                        VLine(dc, 9, 2, 7);
                        VLine(dc, 14, 2, 7);
                    });
                default:
                    return Build(dc => { });
            }
        }

        public static ImageSource For(ReferenceMode mode)
        {
            switch (mode)
            {
                case ReferenceMode.Room:
                    // Planta de una habitación con un mueble dentro.
                    return Build(dc =>
                    {
                        dc.DrawRectangle(null, Thick, new Rect(3, 3, 18, 18));
                        Rect(dc, 7, 7, 7, 5);
                        Rect(dc, 16, 14, 3, 5);
                    });
                case ReferenceMode.KeyObject:
                    return Build(dc =>
                    {
                        dc.DrawRectangle(null, Dashed, new Rect(2.5, 2.5, 19, 19));
                        dc.DrawEllipse(null, Line, new Point(8.5, 12), 3, 3);
                        dc.DrawLine(Line, new Point(11.5, 12.5), new Point(19, 12.5));
                        VLine(dc, 15, 12, 16);
                        VLine(dc, 17, 12, 15);
                    });
                default:
                    // Selección: rectángulo discontinuo con su centro.
                    return Build(dc =>
                    {
                        dc.DrawRectangle(null, Dashed, new Rect(2.5, 2.5, 19, 19));
                        Rect(dc, 10, 10, 4, 4);
                    });
            }
        }

        /// <summary>Icono del botón del ribbon: "alinear a la izquierda" sobre fondo azul.</summary>
        public static ImageSource Ribbon(int pixels)
        {
            double s = pixels;
            var visual = new DrawingVisual();
            using (DrawingContext dc = visual.RenderOpen())
            {
                var background = new LinearGradientBrush(
                    Color.FromRgb(0x2E, 0x7D, 0xF0),
                    Color.FromRgb(0x0B, 0x3D, 0x91),
                    90.0);
                dc.DrawRoundedRectangle(background, null, new Rect(0, 0, s, s), s * 0.18, s * 0.18);

                double unit = s / 16.0;
                var white = Brushes.White;
                dc.DrawRectangle(white, null, new Rect(3 * unit, 2 * unit, Math.Max(1, unit), 12 * unit));
                dc.DrawRectangle(white, null, new Rect(5 * unit, 4 * unit, 5 * unit, 3 * unit));
                dc.DrawRectangle(white, null, new Rect(5 * unit, 9 * unit, 8 * unit, 3 * unit));
            }

            var bitmap = new RenderTargetBitmap(pixels, pixels, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Freeze();
            return bitmap;
        }

        private static ImageSource Build(Action<DrawingContext> draw)
        {
            var group = new DrawingGroup();
            using (DrawingContext dc = group.Open())
            {
                // Fondo transparente que fija el tamaño del dibujo en 24×24.
                dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, Size, Size));
                draw(dc);
            }
            var image = new DrawingImage(group);
            image.Freeze();
            return image;
        }

        private static void Rect(DrawingContext dc, double x, double y, double w, double h)
        {
            dc.DrawRectangle(Ink, null, new Rect(x, y, w, h));
        }

        /// <summary>Línea vertical de 1 px que ocupa la columna de píxeles x.</summary>
        private static void VLine(DrawingContext dc, double x, double y1, double y2)
        {
            dc.DrawLine(Line, new Point(x + 0.5, y1), new Point(x + 0.5, y2));
        }

        /// <summary>Línea horizontal de 1 px que ocupa la fila de píxeles y.</summary>
        private static void HLine(DrawingContext dc, double y, double x1, double x2)
        {
            dc.DrawLine(Line, new Point(x1, y + 0.5), new Point(x2, y + 0.5));
        }

        private static T Frozen<T>(T freezable) where T : Freezable
        {
            freezable.Freeze();
            return freezable;
        }
    }
}
