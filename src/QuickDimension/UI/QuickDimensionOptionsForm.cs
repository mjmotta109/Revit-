using System;
using System.Drawing;
using System.Windows.Forms;
using QuickDimension.Model;

namespace QuickDimension.UI
{
    /// <summary>
    /// Diálogo previo al trazado: decide qué entra en la cota. Las opciones se
    /// recuerdan durante la sesión, así que normalmente basta con pulsar Aceptar.
    /// </summary>
    public class QuickDimensionOptionsForm : Form
    {
        private readonly RadioButton _crossedOnly;
        private readonly RadioButton _includeOpenings;
        private readonly CheckBox _openingEdges;
        private readonly CheckBox _openingCenters;
        private readonly CheckBox _includeColumns;
        private readonly CheckBox _wallCenterlines;
        private readonly NumericUpDown _offset;
        private readonly NumericUpDown _tolerance;
        private readonly GroupBox _openingsBox;

        public QuickDimensionOptions Options { get; private set; }

        public QuickDimensionOptionsForm(QuickDimensionOptions previous)
        {
            QuickDimensionOptions initial = previous != null ? previous.Clone() : new QuickDimensionOptions();
            Options = initial;

            Text = "Cota rápida";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(470, 452);
            Font = new Font("Segoe UI", 9f);
            ShowIcon = false;
            MaximizeBox = false;
            MinimizeBox = false;

            var scopeBox = new GroupBox
            {
                Text = "Qué acotar",
                Location = new Point(12, 10),
                Size = new Size(446, 122),
            };

            _crossedOnly = new RadioButton
            {
                Text = "Solo lo que atraviesa la línea",
                Location = new Point(14, 24),
                Size = new Size(420, 20),
                Checked = initial.Scope == DimensionScope.CrossedOnly,
            };

            var crossedHint = new Label
            {
                Text = "Caras de los muros y columnas que la línea cruza.",
                Location = new Point(34, 44),
                Size = new Size(400, 18),
                ForeColor = SystemColors.GrayText,
            };

            _includeOpenings = new RadioButton
            {
                Text = "También las aberturas proyectadas",
                Location = new Point(14, 66),
                Size = new Size(420, 20),
                Checked = initial.Scope == DimensionScope.IncludeOpenings,
            };

            var openingsHint = new Label
            {
                Text = "Añade puertas, ventanas y vanos de los muros que la línea recorre.",
                Location = new Point(34, 86),
                Size = new Size(400, 18),
                ForeColor = SystemColors.GrayText,
            };

            scopeBox.Controls.Add(_crossedOnly);
            scopeBox.Controls.Add(crossedHint);
            scopeBox.Controls.Add(_includeOpenings);
            scopeBox.Controls.Add(openingsHint);

            _openingsBox = new GroupBox
            {
                Text = "Referencias de las aberturas",
                Location = new Point(12, 140),
                Size = new Size(446, 82),
            };

            _openingEdges = new CheckBox
            {
                Text = "Bordes: jambas de huecos y extremos de muro",
                Location = new Point(14, 24),
                Size = new Size(420, 20),
                Checked = initial.OpeningEdges,
            };

            _openingCenters = new CheckBox
            {
                Text = "Ejes: centro de puertas y ventanas",
                Location = new Point(14, 48),
                Size = new Size(420, 20),
                Checked = initial.OpeningCenters,
            };

            _openingsBox.Controls.Add(_openingEdges);
            _openingsBox.Controls.Add(_openingCenters);

            _includeColumns = new CheckBox
            {
                Text = "Incluir columnas arquitectónicas y estructurales",
                Location = new Point(26, 232),
                Size = new Size(430, 20),
                Checked = initial.IncludeColumns,
            };

            _wallCenterlines = new CheckBox
            {
                Text = "Acotar también al eje de los muros",
                Location = new Point(26, 256),
                Size = new Size(430, 20),
                Checked = initial.WallCenterlines,
            };

            var offsetLabel = new Label
            {
                Text = "Desplazar la cota respecto a la línea:",
                Location = new Point(26, 288),
                Size = new Size(250, 20),
                TextAlign = ContentAlignment.MiddleLeft,
            };

            _offset = new NumericUpDown
            {
                Location = new Point(286, 285),
                Size = new Size(90, 24),
                DecimalPlaces = 2,
                Increment = 0.10m,
                Minimum = -100m,
                Maximum = 100m,
                Value = Clamp(initial.OffsetMeters, -100m, 100m),
            };

            var offsetUnit = new Label
            {
                Text = "m",
                Location = new Point(382, 288),
                Size = new Size(40, 20),
                TextAlign = ContentAlignment.MiddleLeft,
            };

            var toleranceLabel = new Label
            {
                Text = "Holgura de búsqueda alrededor de la línea:",
                Location = new Point(26, 318),
                Size = new Size(250, 20),
                TextAlign = ContentAlignment.MiddleLeft,
            };

            _tolerance = new NumericUpDown
            {
                Location = new Point(286, 315),
                Size = new Size(90, 24),
                DecimalPlaces = 3,
                Increment = 0.010m,
                Minimum = 0m,
                Maximum = 2m,
                Value = Clamp(initial.SearchToleranceMeters, 0m, 2m),
            };

            var toleranceUnit = new Label
            {
                Text = "m",
                Location = new Point(382, 318),
                Size = new Size(40, 20),
                TextAlign = ContentAlignment.MiddleLeft,
            };

            var note = new Label
            {
                Text = "El mobiliario y el equipamiento (FFE) nunca entran en la cota: " +
                       "muebles, armarios, sanitarios, luminarias y equipos se ignoran " +
                       "aunque la línea los atraviese.",
                Location = new Point(14, 348),
                Size = new Size(444, 48),
                ForeColor = SystemColors.GrayText,
            };

            var accept = new Button
            {
                Text = "Acotar",
                DialogResult = DialogResult.OK,
                Location = new Point(268, 408),
                Size = new Size(92, 30),
            };

            var cancel = new Button
            {
                Text = "Cancelar",
                DialogResult = DialogResult.Cancel,
                Location = new Point(366, 408),
                Size = new Size(92, 30),
            };

            accept.Click += OnAccept;

            Controls.Add(scopeBox);
            Controls.Add(_openingsBox);
            Controls.Add(_includeColumns);
            Controls.Add(_wallCenterlines);
            Controls.Add(offsetLabel);
            Controls.Add(_offset);
            Controls.Add(offsetUnit);
            Controls.Add(toleranceLabel);
            Controls.Add(_tolerance);
            Controls.Add(toleranceUnit);
            Controls.Add(note);
            Controls.Add(accept);
            Controls.Add(cancel);

            AcceptButton = accept;
            CancelButton = cancel;

            _includeOpenings.CheckedChanged += (sender, e) => SyncOpeningsBox();
            SyncOpeningsBox();
        }

        private void SyncOpeningsBox()
        {
            _openingsBox.Enabled = _includeOpenings.Checked;
        }

        private void OnAccept(object sender, EventArgs e)
        {
            if (_includeOpenings.Checked && !_openingEdges.Checked && !_openingCenters.Checked)
            {
                MessageBox.Show(this,
                    "Elige al menos un tipo de referencia para las aberturas: bordes, ejes o ambos.",
                    "Cota rápida", MessageBoxButtons.OK, MessageBoxIcon.Information);

                DialogResult = DialogResult.None;
                return;
            }

            Options = new QuickDimensionOptions
            {
                Scope = _includeOpenings.Checked ? DimensionScope.IncludeOpenings : DimensionScope.CrossedOnly,
                OpeningEdges = _openingEdges.Checked,
                OpeningCenters = _openingCenters.Checked,
                IncludeColumns = _includeColumns.Checked,
                WallCenterlines = _wallCenterlines.Checked,
                OffsetMeters = (double)_offset.Value,
                SearchToleranceMeters = (double)_tolerance.Value,
            };
        }

        private static decimal Clamp(double value, decimal min, decimal max)
        {
            decimal converted;
            try { converted = (decimal)value; }
            catch (OverflowException) { return 0m; }

            if (converted < min) return min;
            if (converted > max) return max;
            return converted;
        }
    }

    /// <summary>Envuelve el handle de la ventana de Revit para que los diálogos salgan centrados sobre ella.</summary>
    public class RevitOwnerWindow : IWin32Window
    {
        public RevitOwnerWindow(IntPtr handle) { Handle = handle; }

        public IntPtr Handle { get; private set; }
    }
}
