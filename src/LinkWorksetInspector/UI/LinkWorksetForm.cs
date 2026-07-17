using System;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using LinkWorksetInspector.Models;
using LinkWorksetInspector.Services;

namespace LinkWorksetInspector.UI
{
    /// <summary>
    /// Ventana con la tabla de vínculos: workset, estado y diagnóstico.
    /// Solo lectura: no modifica el modelo.
    /// </summary>
    public class LinkWorksetForm : Form
    {
        private readonly LinkWorksetReport _report;
        private readonly DataGridView _grid;
        private readonly CheckBox _onlyProblems;
        private readonly Label _summary;

        public LinkWorksetForm(LinkWorksetReport report, string docTitle, string activeViewName)
        {
            _report = report;

            Text = "¿En qué workset está mi vínculo? — " + docTitle;
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1280, 640);
            MinimumSize = new Size(900, 420);
            Font = new Font("Segoe UI", 9f);
            ShowIcon = false;
            MaximizeBox = true;
            MinimizeBox = false;

            _summary = new Label
            {
                Dock = DockStyle.Top,
                AutoSize = false,
                Height = 58,
                Padding = new Padding(10, 8, 10, 0),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            };

            _onlyProblems = new CheckBox
            {
                Text = "Mostrar solo vínculos con problemas",
                Dock = DockStyle.Top,
                Height = 28,
                Padding = new Padding(10, 0, 0, 0),
                Checked = false,
            };
            _onlyProblems.CheckedChanged += (s, e) => FillGrid();

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                Height = 46,
                Padding = new Padding(8),
            };
            var closeButton = new Button { Text = "Cerrar", Width = 110, Height = 28 };
            closeButton.Click += (s, e) => Close();
            var copyButton = new Button { Text = "Copiar tabla", Width = 130, Height = 28 };
            copyButton.Click += (s, e) => CopyToClipboard();
            buttons.Controls.Add(closeButton);
            buttons.Controls.Add(copyButton);

            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                AllowUserToOrderColumns = true,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells,
                BackgroundColor = SystemColors.Window,
                BorderStyle = BorderStyle.None,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
            };
            AddColumn("Vínculo", 230);
            AddColumn("Formato", 65);
            AddColumn("Estado de carga", 130);
            AddColumn("Workset del vínculo (tipo)", 175);
            AddColumn("Workset de la instancia", 175);
            AddColumn("¿Visible en vista activa?", 95);
            DataGridViewTextBoxColumn diagColumn = AddColumn("Diagnóstico — qué hacer", 430);
            diagColumn.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            _grid.CellDoubleClick += OnRowDoubleClick;

            // WinForms procesa el docking en orden z inverso (el último añadido se
            // ancla primero): el resumen debe añadirse el último para quedar arriba.
            Controls.Add(_grid);
            Controls.Add(buttons);
            Controls.Add(_onlyProblems);
            Controls.Add(_summary);

            BuildSummary(activeViewName);
            FillGrid();
        }

        private DataGridViewTextBoxColumn AddColumn(string header, int width)
        {
            var column = new DataGridViewTextBoxColumn
            {
                HeaderText = header,
                Width = width,
                SortMode = DataGridViewColumnSortMode.NotSortable,
            };
            _grid.Columns.Add(column);
            return column;
        }

        private void BuildSummary(string activeViewName)
        {
            int total = _report.Rows.Count;
            int problems = _report.Rows.Count(r => r.HasProblem);

            var sb = new StringBuilder();
            sb.AppendLine("Vínculos analizados: " + total +
                          "    ·    Con problemas: " + problems +
                          "    ·    Vista activa: " + (string.IsNullOrEmpty(activeViewName) ? "(ninguna)" : activeViewName));

            if (!_report.IsWorkshared)
            {
                sb.AppendLine("Este modelo no tiene worksets/subproyectos (no es de trabajo compartido); solo se muestra el estado de carga.");
            }
            else if (_report.ClosedWorksetsWithLinks.Count > 0)
            {
                sb.AppendLine("Worksets CERRADOS con vínculos afectados — abre solo estos en Colaborar ▸ Subproyectos: " +
                              string.Join(", ", _report.ClosedWorksetsWithLinks));
            }
            else
            {
                sb.AppendLine("Ningún vínculo está bloqueado por un workset cerrado.");
            }

            _summary.Text = sb.ToString();
        }

        private void FillGrid()
        {
            _grid.SuspendLayout();
            _grid.Rows.Clear();

            foreach (LinkReportRow r in _report.Rows)
            {
                if (_onlyProblems.Checked && !r.HasProblem) continue;

                int index = _grid.Rows.Add(
                    r.LinkName,
                    r.Format,
                    r.LoadStatus,
                    Combine(r.TypeWorkset, r.TypeWorksetState),
                    Combine(r.InstanceWorkset, r.InstanceWorksetState),
                    r.VisibleInActiveView,
                    r.Diagnosis);

                DataGridViewRow row = _grid.Rows[index];
                row.Tag = r;
                if (r.HasProblem)
                {
                    row.DefaultCellStyle.BackColor = Color.MistyRose;
                    row.DefaultCellStyle.SelectionBackColor = Color.IndianRed;
                }
            }

            _grid.ResumeLayout();
        }

        private static string Combine(string workset, string state)
        {
            if (string.IsNullOrEmpty(state) || state == "—") return workset;
            return workset + "  [" + state + "]";
        }

        private void OnRowDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (!(_grid.Rows[e.RowIndex].Tag is LinkReportRow r)) return;

            var sb = new StringBuilder();
            sb.AppendLine("Vínculo:  " + r.LinkName);
            sb.AppendLine("Formato:  " + r.Format);
            sb.AppendLine("Estado de carga:  " + r.LoadStatus);
            sb.AppendLine("Workset del tipo:  " + Combine(r.TypeWorkset, r.TypeWorksetState));
            sb.AppendLine("Workset de la instancia:  " + Combine(r.InstanceWorkset, r.InstanceWorksetState));
            sb.AppendLine("Visible en la vista activa:  " + r.VisibleInActiveView);
            sb.AppendLine();
            sb.AppendLine("Ruta:");
            sb.AppendLine(r.FilePath);
            sb.AppendLine();
            sb.AppendLine("Diagnóstico:");
            sb.AppendLine(r.Diagnosis);

            MessageBox.Show(this, sb.ToString(), "Detalle del vínculo",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void CopyToClipboard()
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Join("\t",
                "Vínculo", "Formato", "Estado de carga", "Workset del tipo",
                "Workset de la instancia", "Visible en vista activa", "Diagnóstico", "Ruta"));

            foreach (LinkReportRow r in _report.Rows)
            {
                sb.AppendLine(string.Join("\t",
                    r.LinkName, r.Format, r.LoadStatus,
                    Combine(r.TypeWorkset, r.TypeWorksetState),
                    Combine(r.InstanceWorkset, r.InstanceWorksetState),
                    r.VisibleInActiveView, r.Diagnosis, r.FilePath));
            }

            try
            {
                Clipboard.SetText(sb.ToString());
                MessageBox.Show(this, "Tabla copiada al portapapeles (pégala en Excel o en un correo).",
                    "Copiar tabla", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "No se pudo copiar al portapapeles: " + ex.Message,
                    "Copiar tabla", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }

    /// <summary>Envuelve el HWND de la ventana principal de Revit para usarlo como owner.</summary>
    public class RevitOwnerWindow : IWin32Window
    {
        public IntPtr Handle { get; }
        public RevitOwnerWindow(IntPtr handle) { Handle = handle; }
    }
}
