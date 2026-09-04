using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using LinkWorksetInspector.Services.RoomElevations;
using DB = Autodesk.Revit.DB;

namespace LinkWorksetInspector.UI
{
    /// <summary>
    /// Opciones de generación de alzados por habitación y registro de resultados.
    /// El trabajo real lo hace el generador, que se invoca por callback desde el
    /// botón «Crear» (dentro del contexto de API del comando, que es modal).
    /// La API de Revit se usa con el alias DB porque Form, Panel, Label, View y
    /// Color existen tanto en Autodesk.Revit.DB como en WinForms.
    /// </summary>
    public class RoomElevationsForm : Form
    {
        private class Entry
        {
            public string Text;
            public DB.ElementId Id;
            public override string ToString() { return Text; }
        }

        private readonly Func<RoomElevationOptions, Action<string>, IList<RoomElevationResult>> _run;

        private readonly ComboBox _scope = new ComboBox();
        private readonly ComboBox _viewType = new ComboBox();
        private readonly ComboBox _template = new ComboBox();
        private readonly ComboBox _scale = new ComboBox();
        private readonly NumericUpDown _horizontalMargin = NewNumeric(0, 2000, 150);
        private readonly NumericUpDown _verticalMargin = NewNumeric(0, 2000, 50);
        private readonly NumericUpDown _beyondWall = NewNumeric(0, 2000, 250);
        private readonly NumericUpDown _minWall = NewNumeric(0, 5000, 400);
        private readonly NumericUpDown _defaultHeight = NewNumeric(500, 20000, 2700);
        private readonly CheckBox _useCeilings = new CheckBox { Text = "Recortar con cielos rasos", AutoSize = true, Checked = true };
        private readonly CheckBox _useSlabs = new CheckBox { Text = "Recortar con losas / placas", AutoSize = true, Checked = true };
        private readonly CheckBox _skipExisting = new CheckBox { Text = "Omitir habitaciones que ya tienen alzados", AutoSize = true, Checked = true };
        private readonly TextBox _namePattern = new TextBox { Text = "{numero} {nombre} - {direccion}" };
        private readonly RichTextBox _log = new RichTextBox();
        private readonly Label _status = new Label();
        private readonly Button _createButton = new Button();

        private bool _busy;

        public RoomElevationsForm(DB.Document doc,
            Func<RoomElevationOptions, Action<string>, IList<RoomElevationResult>> run)
        {
            _run = run;

            Text = "Alzados por habitación";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(760, 740);
            MinimumSize = new Size(640, 560);
            Font = new Font("Segoe UI", 9f);
            ShowIcon = false;
            MinimizeBox = false;

            FillScope();
            FillViewTypes(doc);
            FillTemplates(doc);
            FillScales();

            var options = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 2,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(12, 10, 12, 6),
            };
            options.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));
            options.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            AddRow(options, "Habitaciones:", _scope);
            AddRow(options, "Tipo de vista de alzado:", _viewType);
            AddRow(options, "Plantilla de vista:", _template);
            AddRow(options, "Escala:", _scale);
            AddRow(options, "Margen horizontal (mm):", _horizontalMargin);
            AddRow(options, "Margen vertical, piso y techo (mm):", _verticalMargin);
            AddRow(options, "Profundidad tras el muro (mm):", _beyondWall);
            AddRow(options, "Longitud mínima de muro (mm):", _minWall);
            AddRow(options, "Altura si no hay techo ni losa (mm):", _defaultHeight);
            AddRow(options, "Nombre de la vista:", _namePattern);
            AddRow(options, "", _useCeilings);
            AddRow(options, "", _useSlabs);
            AddRow(options, "", _skipExisting);

            var help = new Label
            {
                Dock = DockStyle.Top,
                AutoSize = false,
                Height = 56,
                Padding = new Padding(14, 4, 14, 6),
                ForeColor = Color.DimGray,
                Text = "Se crea un alzado por cada orientación de muro de la habitación. Cada vista se " +
                       "recorta al ancho de la estancia y, en vertical, entre el piso y el cielo raso o " +
                       "la losa más cercana por encima.\n" +
                       "En el nombre puedes usar {numero}, {nombre} y {direccion}.",
            };

            _status.Dock = DockStyle.Bottom;
            _status.Height = 22;
            _status.Padding = new Padding(12, 2, 12, 2);
            _status.ForeColor = Color.DimGray;

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                Height = 46,
                Padding = new Padding(8),
            };
            var closeButton = new Button { Text = "Cerrar", Width = 110, Height = 28 };
            closeButton.Click += (s, e) => Close();
            _createButton.Text = "Crear alzados";
            _createButton.Width = 140;
            _createButton.Height = 28;
            _createButton.Click += (s, e) => Create();
            buttons.Controls.Add(closeButton);
            buttons.Controls.Add(_createButton);

            _log.Dock = DockStyle.Fill;
            _log.ReadOnly = true;
            _log.BackColor = Color.White;
            _log.BorderStyle = BorderStyle.None;

            var logPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 4, 12, 4) };
            logPanel.Controls.Add(_log);

            // WinForms ancla los controles en orden z inverso: el último añadido
            // reclama primero su borde, así que el relleno va antes que la cabecera.
            Controls.Add(logPanel);
            Controls.Add(buttons);
            Controls.Add(_status);
            Controls.Add(help);
            Controls.Add(options);

            FormClosing += (s, e) =>
            {
                if (_busy)
                {
                    MessageBox.Show(this, "Espera a que termine la creación de alzados.",
                        "Alzados por habitación", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    e.Cancel = true;
                }
            };

            if (_viewType.Items.Count == 0) _createButton.Enabled = false;
        }

        /// <summary>
        /// El aviso se escribe aquí y no en el constructor: dar formato al RichTextBox
        /// antes de que exista el handle del formulario obliga a recrearlo y puede
        /// perder el color.
        /// </summary>
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            if (_viewType.Items.Count == 0)
                AppendLine("El proyecto no tiene ningún tipo de vista de alzado. Créalo primero en " +
                           "Vista ▸ Alzado, o carga una plantilla de proyecto que lo incluya.", Color.Firebrick);
        }

        // ---------------------------------------------------------------- opciones

        private void FillScope()
        {
            _scope.DropDownStyle = ComboBoxStyle.DropDownList;
            _scope.Items.Add("Habitaciones visibles en la vista activa");
            _scope.Items.Add("Todas las habitaciones del modelo");
            _scope.Items.Add("Solo las habitaciones seleccionadas");
            _scope.SelectedIndex = 0;
        }

        private void FillViewTypes(DB.Document doc)
        {
            _viewType.DropDownStyle = ComboBoxStyle.DropDownList;
            foreach (DB.ViewFamilyType type in new DB.FilteredElementCollector(doc)
                         .OfClass(typeof(DB.ViewFamilyType)).Cast<DB.ViewFamilyType>()
                         .Where(t => t.ViewFamily == DB.ViewFamily.Elevation)
                         .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase))
            {
                _viewType.Items.Add(new Entry { Text = type.Name, Id = type.Id });
            }
            if (_viewType.Items.Count > 0) _viewType.SelectedIndex = 0;
        }

        private void FillTemplates(DB.Document doc)
        {
            _template.DropDownStyle = ComboBoxStyle.DropDownList;
            _template.Items.Add(new Entry { Text = "(ninguna)", Id = DB.ElementId.InvalidElementId });

            foreach (DB.View view in new DB.FilteredElementCollector(doc)
                         .OfClass(typeof(DB.View)).Cast<DB.View>()
                         .Where(v => v.IsTemplate &&
                                     (v.ViewType == DB.ViewType.Elevation || v.ViewType == DB.ViewType.Section))
                         .OrderBy(v => v.Name, StringComparer.OrdinalIgnoreCase))
            {
                _template.Items.Add(new Entry { Text = view.Name, Id = view.Id });
            }
            _template.SelectedIndex = 0;
        }

        private void FillScales()
        {
            _scale.DropDownStyle = ComboBoxStyle.DropDownList;
            foreach (int scale in new[] { 10, 20, 25, 50, 75, 100, 125, 200 })
                _scale.Items.Add(scale);
            _scale.SelectedItem = 50;
        }

        private RoomElevationOptions BuildOptions()
        {
            var viewType = _viewType.SelectedItem as Entry;
            var template = _template.SelectedItem as Entry;

            return new RoomElevationOptions
            {
                Scope = _scope.SelectedIndex == 1 ? RoomScope.AllInModel
                      : _scope.SelectedIndex == 2 ? RoomScope.CurrentSelection
                      : RoomScope.ActiveView,
                ViewFamilyTypeId = viewType != null ? viewType.Id : DB.ElementId.InvalidElementId,
                ViewTemplateId = template != null ? template.Id : DB.ElementId.InvalidElementId,
                Scale = _scale.SelectedItem is int ? (int)_scale.SelectedItem : 50,
                HorizontalMarginMm = (double)_horizontalMargin.Value,
                VerticalMarginMm = (double)_verticalMargin.Value,
                BeyondWallMm = (double)_beyondWall.Value,
                MinWallLengthMm = (double)_minWall.Value,
                DefaultHeightMm = (double)_defaultHeight.Value,
                UseCeilings = _useCeilings.Checked,
                UseSlabs = _useSlabs.Checked,
                SkipRoomsWithElevations = _skipExisting.Checked,
                NamePattern = _namePattern.Text,
            };
        }

        // ---------------------------------------------------------------- ejecución

        private void Create()
        {
            if (_busy) return;

            RoomElevationOptions options = BuildOptions();
            if (options.ViewFamilyTypeId == DB.ElementId.InvalidElementId)
            {
                MessageBox.Show(this, "Elige un tipo de vista de alzado.", "Alzados por habitación",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _busy = true;
            _createButton.Enabled = false;
            _log.Clear();
            UseWaitCursor = true;

            try
            {
                Report(_run(options, SetStatus));
            }
            catch (Exception ex)
            {
                AppendLine("Error: " + ex.Message, Color.Firebrick);
            }
            finally
            {
                _busy = false;
                _createButton.Enabled = true;
                UseWaitCursor = false;
                _status.Text = "";
            }
        }

        private void SetStatus(string message)
        {
            _status.Text = message;
            _status.Refresh();
        }

        private void Report(IList<RoomElevationResult> results)
        {
            if (results == null || results.Count == 0)
            {
                AppendLine("No se encontró ninguna habitación con el criterio elegido.", Color.Firebrick);
                return;
            }

            int views = results.Sum(r => r.CreatedViews.Count);
            int done = results.Count(r => r.CreatedViews.Count > 0);
            int skipped = results.Count(r => r.Skipped);
            int failed = results.Count(r => r.Failed);

            AppendLine("Se crearon " + views + " alzados en " + done + " habitaciones." +
                       (skipped > 0 ? "  Omitidas: " + skipped + "." : "") +
                       (failed > 0 ? "  Con problemas: " + failed + "." : ""),
                       Color.FromArgb(0x1B, 0x7A, 0x43), true);
            AppendLine("", Color.Black);

            foreach (RoomElevationResult result in results)
            {
                Color color = result.Failed ? Color.Firebrick
                            : result.Skipped ? Color.DarkGoldenrod
                            : Color.FromArgb(0x0B, 0x3D, 0x91);

                AppendLine(result.RoomLabel, color, true);
                foreach (string view in result.CreatedViews) AppendLine("    + " + view, Color.Black);
                foreach (string note in result.Notes) AppendLine("    · " + note, Color.DimGray);
                AppendLine("", Color.Black);
            }
        }

        private void AppendLine(string text, Color color, bool bold = false)
        {
            _log.SelectionStart = _log.TextLength;
            _log.SelectionColor = color;
            _log.SelectionFont = new Font(Font, bold ? FontStyle.Bold : FontStyle.Regular);
            _log.AppendText(text + Environment.NewLine);
            _log.SelectionStart = _log.TextLength;
            _log.ScrollToCaret();
        }

        // ---------------------------------------------------------------- layout

        private static NumericUpDown NewNumeric(int minimum, int maximum, int value)
        {
            return new NumericUpDown
            {
                Minimum = minimum,
                Maximum = maximum,
                Value = value,
                Increment = 10,
                Width = 100,
            };
        }

        private static void AddRow(TableLayoutPanel layout, string text, Control control)
        {
            layout.Controls.Add(new Label
            {
                Text = text,
                AutoSize = false,
                Width = 255,
                Height = 26,
                TextAlign = ContentAlignment.MiddleLeft,
            });

            control.Margin = new Padding(0, 2, 0, 2);
            if (control is ComboBox || control is TextBox) control.Width = 380;
            layout.Controls.Add(control);
        }
    }
}
