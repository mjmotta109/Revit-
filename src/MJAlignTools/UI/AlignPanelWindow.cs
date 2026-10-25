using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using MJAlignTools.Core;
using MJAlignTools.Revit;
using ExternalEvent = Autodesk.Revit.UI.ExternalEvent;
using ExternalEventRequest = Autodesk.Revit.UI.ExternalEventRequest;

namespace MJAlignTools.UI
{
    /// <summary>
    /// Panel flotante "Alinear", con la misma distribución que el de Illustrator:
    /// Alinear objetos, Distribuir objetos, Distribuir espaciado y Alinear a.
    /// Es no modal: seleccionas en Revit, pulsas un botón, y el panel sigue abierto.
    /// </summary>
    internal sealed class AlignPanelWindow : Window
    {
        private const string AutoText = "Auto";

        private readonly ExternalEvent _event;
        private readonly AlignRequestHandler _handler;
        private readonly IntPtr _revitWindow;
        private readonly Dictionary<ReferenceMode, IconButton> _modeButtons = new Dictionary<ReferenceMode, IconButton>();
        private readonly TextBox _spacingBox;
        private readonly TextBlock _keyLabel;
        private readonly FrameworkElement _keyRow;
        private readonly TextBlock _status;

        private ReferenceMode _mode = ReferenceMode.Selection;
        private bool _busy;

        public AlignPanelWindow(ExternalEvent externalEvent, AlignRequestHandler handler, IntPtr revitWindow)
        {
            _event = externalEvent;
            _handler = handler;
            _revitWindow = revitWindow;

            Title = "Alinear";
            Width = 318;
            SizeToContent = SizeToContent.Height;
            ResizeMode = ResizeMode.NoResize;
            WindowStyle = WindowStyle.ToolWindow;
            ShowInTaskbar = false;
            Background = Palette.Panel;
            Foreground = Palette.Text;
            FontFamily = new FontFamily("Segoe UI");
            FontSize = 12;
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;
            WindowStartupLocation = WindowStartupLocation.Manual;
            Rect workArea = SystemParameters.WorkArea;
            Left = workArea.Right - Width - 60;
            Top = workArea.Top + 160;

            var root = new StackPanel { Margin = new Thickness(10, 6, 10, 10) };

            // --- Alinear objetos ---
            root.Children.Add(SectionTitle("Alinear objetos:"));
            root.Children.Add(Row(
                CommandButton(AlignCommand.AlignLeft, "Alinear a la izquierda (bordes izquierdos)"),
                CommandButton(AlignCommand.AlignHorizontalCenter, "Alinear al centro horizontal"),
                CommandButton(AlignCommand.AlignRight, "Alinear a la derecha (bordes derechos)"),
                null,
                CommandButton(AlignCommand.AlignTop, "Alinear arriba (bordes superiores)"),
                CommandButton(AlignCommand.AlignVerticalCenter, "Alinear al centro vertical"),
                CommandButton(AlignCommand.AlignBottom, "Alinear abajo (bordes inferiores)")));
            root.Children.Add(HorizontalRule());

            // --- Distribuir objetos ---
            root.Children.Add(SectionTitle("Distribuir objetos:"));
            root.Children.Add(Row(
                CommandButton(AlignCommand.DistributeTop, "Distribuir bordes superiores: mismo paso vertical entre los bordes de arriba"),
                CommandButton(AlignCommand.DistributeVerticalCenter, "Distribuir centros verticales: mismo paso vertical entre centros"),
                CommandButton(AlignCommand.DistributeBottom, "Distribuir bordes inferiores: mismo paso vertical entre los bordes de abajo"),
                null,
                CommandButton(AlignCommand.DistributeLeft, "Distribuir bordes izquierdos: mismo paso horizontal entre los bordes izquierdos"),
                CommandButton(AlignCommand.DistributeHorizontalCenter, "Distribuir centros horizontales: mismo paso horizontal entre centros"),
                CommandButton(AlignCommand.DistributeRight, "Distribuir bordes derechos: mismo paso horizontal entre los bordes derechos")));
            root.Children.Add(HorizontalRule());

            // --- Distribuir espaciado | Alinear a ---
            var bottom = new Grid();
            bottom.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            bottom.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            bottom.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _spacingBox = new TextBox
            {
                Text = AutoText,
                Width = 70,
                Height = 26,
                Margin = new Thickness(4, 1, 0, 1),
                Padding = new Thickness(3, 0, 3, 0),
                VerticalContentAlignment = VerticalAlignment.Center,
                Background = Palette.InputBackground,
                Foreground = Palette.Text,
                CaretBrush = Palette.Text,
                BorderBrush = Palette.InputBorder,
                ToolTip = "Separación entre elementos para «Distribuir espaciado».\n" +
                          "Auto (o vacío): el mismo hueco entre todos, calculado automáticamente.\n" +
                          "Una distancia (p. ej. 600 mm, 0.6 m, 2' 0\"): separa exactamente esa medida,\n" +
                          "a partir del objeto clave, del elemento de la izquierda / de arriba, o centrado en la habitación.",
            };
            _spacingBox.GotKeyboardFocus += (s, e) => _spacingBox.SelectAll();
            _spacingBox.LostKeyboardFocus += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(_spacingBox.Text)) _spacingBox.Text = AutoText;
            };

            var spacingRow = new StackPanel { Orientation = Orientation.Horizontal };
            spacingRow.Children.Add(CommandButton(AlignCommand.SpaceVertical, "Distribuir espaciado vertical: mismo hueco libre entre elementos, de arriba a abajo"));
            spacingRow.Children.Add(CommandButton(AlignCommand.SpaceHorizontal, "Distribuir espaciado horizontal: mismo hueco libre entre elementos, de izquierda a derecha"));
            spacingRow.Children.Add(_spacingBox);

            var spacingColumn = new StackPanel();
            spacingColumn.Children.Add(SectionTitle("Distribuir espaciado:"));
            spacingColumn.Children.Add(spacingRow);
            Grid.SetColumn(spacingColumn, 0);
            bottom.Children.Add(spacingColumn);

            var divider = new Border { Width = 1, Background = Palette.Separator, Margin = new Thickness(8, 6, 8, 2) };
            Grid.SetColumn(divider, 1);
            bottom.Children.Add(divider);

            var modeRow = new StackPanel { Orientation = Orientation.Horizontal };
            modeRow.Children.Add(ModeButton(ReferenceMode.Room,
                "Alinear a la habitación: cada elemento se alinea o reparte dentro de la habitación en la que está\n" +
                "(del modelo o de un vínculo RVT cargado). Funciona también con un solo elemento."));
            modeRow.Children.Add(ModeButton(ReferenceMode.Selection,
                "Alinear a la selección: al rectángulo que envuelve todos los elementos seleccionados."));
            modeRow.Children.Add(ModeButton(ReferenceMode.KeyObject,
                "Alinear a objeto clave: eliges un elemento que no se moverá y el resto se alinea a él.\n" +
                "Puede ser cualquier elemento (otro mueble, un muro, un pilar…), esté o no en la selección."));

            var modeColumn = new StackPanel();
            modeColumn.Children.Add(SectionTitle("Alinear a:"));
            modeColumn.Children.Add(modeRow);
            Grid.SetColumn(modeColumn, 2);
            bottom.Children.Add(modeColumn);
            root.Children.Add(bottom);

            // --- Objeto clave elegido ---
            _keyLabel = new TextBlock
            {
                Foreground = Palette.MutedText,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 6, 0),
            };
            var pickKey = new IconButton(new TextBlock
            {
                Text = "Elegir…",
                Foreground = Palette.Text,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            }, "Haz clic en el elemento de la vista que servirá de referencia (no se moverá).", width: 62, height: 24);
            pickKey.Click += (s, e) => PickKey();

            var keyGrid = new Grid { Margin = new Thickness(0, 6, 0, 0) };
            keyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            keyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            keyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var keyCaption = new TextBlock { Text = "Objeto clave:", VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(keyCaption, 0);
            Grid.SetColumn(_keyLabel, 1);
            Grid.SetColumn(pickKey, 2);
            keyGrid.Children.Add(keyCaption);
            keyGrid.Children.Add(_keyLabel);
            keyGrid.Children.Add(pickKey);
            _keyRow = keyGrid;
            root.Children.Add(_keyRow);

            // --- Estado ---
            root.Children.Add(HorizontalRule());
            _status = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Foreground = Palette.MutedText,
                MinHeight = 34,
                Text = "Selecciona elementos en la vista y pulsa un botón. Ctrl+Z deshace cada operación.",
            };
            root.Children.Add(_status);

            Content = root;

            _handler.Completed += OnCompleted;
            Closed += (s, e) => _handler.Completed -= OnCompleted;

            SetMode(ReferenceMode.Selection);
            UpdateKeyLabel();
        }

        private void Run(AlignCommand command)
        {
            if (_busy) return;
            _handler.RequestAlign(new AlignRequest
            {
                Command = command,
                Mode = _mode,
                SpacingText = _spacingBox.Text,
            });
            Raise(AlignCommands.Describe(command) + "…");
        }

        private void PickKey()
        {
            if (_busy) return;
            _handler.RequestPickKey();
            if (Raise("Haz clic en el objeto clave en la vista de Revit (Esc para cancelar)."))
            {
                // Lleva el foco a Revit para que el primer clic (o Esc) llegue a la vista.
                try { if (_revitWindow != IntPtr.Zero) NativeMethods.SetForegroundWindow(_revitWindow); } catch { }
            }
        }

        private bool Raise(string workingMessage)
        {
            ExternalEventRequest result = _event.Raise();
            if (result == ExternalEventRequest.Accepted)
            {
                _busy = true;
                ShowStatus(workingMessage, OutcomeKind.Info);
                return true;
            }

            ShowStatus("Revit está ocupado. Termina o cancela el comando activo (Esc) y vuelve a intentarlo.", OutcomeKind.Error);
            return false;
        }

        private void OnCompleted(AlignOutcome outcome)
        {
            _busy = false;
            UpdateKeyLabel();
            if (outcome != null && !string.IsNullOrEmpty(outcome.Message)) ShowStatus(outcome.Message, outcome.Kind);
        }

        private void SetMode(ReferenceMode mode)
        {
            _mode = mode;
            foreach (KeyValuePair<ReferenceMode, IconButton> pair in _modeButtons)
                pair.Value.IsChecked = pair.Key == mode;
            _keyRow.Visibility = mode == ReferenceMode.KeyObject ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateKeyLabel()
        {
            KeyObjectRef key = _handler.Key;
            _keyLabel.Text = key != null ? key.Label : "(ninguno)";
            _keyLabel.ToolTip = key?.Label;
        }

        private void ShowStatus(string message, OutcomeKind kind)
        {
            _status.Text = message;
            switch (kind)
            {
                case OutcomeKind.Success: _status.Foreground = Palette.Success; break;
                case OutcomeKind.Error: _status.Foreground = Palette.Error; break;
                default: _status.Foreground = Palette.MutedText; break;
            }
        }

        private IconButton CommandButton(AlignCommand command, string toolTip)
        {
            IconButton button = IconButton.WithIcon(AlignIcons.For(command), toolTip);
            button.Click += (s, e) => Run(command);
            return button;
        }

        private IconButton ModeButton(ReferenceMode mode, string toolTip)
        {
            IconButton button = IconButton.WithIcon(AlignIcons.For(mode), toolTip);
            button.Click += (s, e) =>
            {
                SetMode(mode);
                if (mode == ReferenceMode.KeyObject && _handler.Key == null) PickKey();
            };
            _modeButtons[mode] = button;
            return button;
        }

        private static TextBlock SectionTitle(string text)
        {
            return new TextBlock { Text = text, Margin = new Thickness(0, 6, 0, 4) };
        }

        private static Border HorizontalRule()
        {
            return new Border { Height = 1, Background = Palette.Separator, Margin = new Thickness(0, 8, 0, 2) };
        }

        /// <summary>Fila de 7 celdas iguales: 3 botones, un hueco y 3 botones (como en Illustrator).</summary>
        private static UniformGrid Row(params UIElement[] cells)
        {
            var grid = new UniformGrid { Rows = 1, Columns = cells.Length };
            foreach (UIElement cell in cells)
            {
                if (cell is FrameworkElement element) element.HorizontalAlignment = HorizontalAlignment.Center;
                grid.Children.Add(cell ?? new Border());
            }
            return grid;
        }

        private static class NativeMethods
        {
            [DllImport("user32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool SetForegroundWindow(IntPtr hWnd);
        }
    }
}
