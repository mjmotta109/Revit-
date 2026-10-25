using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace MJAlignTools.UI
{
    /// <summary>
    /// Botón plano al estilo de los paneles de Illustrator: transparente, se ilumina
    /// al pasar el ratón y puede quedar "marcado" (para el grupo "Alinear a").
    /// Se dibuja con un simple Border para no depender de plantillas de estilo.
    /// </summary>
    internal sealed class IconButton : Border
    {
        private bool _hover;
        private bool _pressed;
        private bool _checked;

        public IconButton(UIElement content, string toolTip, double width = 34, double height = 30)
        {
            Width = width;
            Height = height;
            CornerRadius = new CornerRadius(3);
            BorderThickness = new Thickness(1);
            Margin = new Thickness(1);
            Child = content;
            ToolTip = toolTip;
            SnapsToDevicePixels = true;
            Refresh();
        }

        public static IconButton WithIcon(ImageSource icon, string toolTip)
        {
            var image = new Image
            {
                Source = icon,
                Width = 24,
                Height = 24,
                Stretch = Stretch.Uniform,
                SnapsToDevicePixels = true,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
            return new IconButton(image, toolTip);
        }

        public event EventHandler Click;

        public bool IsChecked
        {
            get => _checked;
            set
            {
                _checked = value;
                Refresh();
            }
        }

        protected override void OnMouseEnter(MouseEventArgs e)
        {
            base.OnMouseEnter(e);
            _hover = true;
            Refresh();
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            _hover = false;
            Refresh();
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            _pressed = true;
            CaptureMouse();
            Refresh();
            e.Handled = true;
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            if (!_pressed) return;

            _pressed = false;
            ReleaseMouseCapture();
            Point p = e.GetPosition(this);
            bool inside = p.X >= 0 && p.Y >= 0 && p.X <= ActualWidth && p.Y <= ActualHeight;
            _hover = inside;
            Refresh();
            e.Handled = true;

            if (inside) Click?.Invoke(this, EventArgs.Empty);
        }

        protected override void OnLostMouseCapture(MouseEventArgs e)
        {
            base.OnLostMouseCapture(e);
            if (!_pressed) return;
            _pressed = false; // p. ej. Alt+Tab a mitad de clic: no se ejecuta nada
            Refresh();
        }

        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            if (e.Property == IsEnabledProperty) Refresh();
        }

        private void Refresh()
        {
            Opacity = IsEnabled ? 1.0 : 0.35;

            if (_pressed)
            {
                Background = Palette.Pressed;
                BorderBrush = Palette.Pressed;
            }
            else if (_checked)
            {
                Background = Palette.Checked;
                BorderBrush = _hover ? Palette.HoverBorder : Palette.CheckedBorder;
            }
            else if (_hover && IsEnabled)
            {
                Background = Palette.Hover;
                BorderBrush = Palette.HoverBorder;
            }
            else
            {
                // Transparente pero no null: así el botón entero recibe el ratón.
                Background = Brushes.Transparent;
                BorderBrush = Brushes.Transparent;
            }
        }
    }

    /// <summary>Colores del panel, imitando el tema oscuro de Illustrator.</summary>
    internal static class Palette
    {
        public static readonly Brush Panel = Make(0x53, 0x53, 0x53);
        public static readonly Brush Text = Make(0xE4, 0xE4, 0xE4);
        public static readonly Brush MutedText = Make(0xBD, 0xBD, 0xBD);
        public static readonly Brush Separator = Make(0x44, 0x44, 0x44);
        public static readonly Brush Hover = Make(0x66, 0x66, 0x66);
        public static readonly Brush HoverBorder = Make(0x80, 0x80, 0x80);
        public static readonly Brush Pressed = Make(0x38, 0x38, 0x38);
        public static readonly Brush Checked = Make(0x33, 0x33, 0x33);
        public static readonly Brush CheckedBorder = Make(0x6E, 0x6E, 0x6E);
        public static readonly Brush InputBackground = Make(0x45, 0x45, 0x45);
        public static readonly Brush InputBorder = Make(0x6A, 0x6A, 0x6A);
        public static readonly Brush Success = Make(0xA6, 0xDB, 0xA0);
        public static readonly Brush Error = Make(0xFF, 0xA8, 0x96);

        private static Brush Make(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }
    }
}
