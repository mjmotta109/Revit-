using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace LinkWorksetInspector.UI
{
    /// <summary>Diálogo para introducir y guardar la API key de Anthropic.</summary>
    public class ApiKeyForm : Form
    {
        private readonly TextBox _keyBox;

        public string ApiKey => _keyBox.Text.Trim();

        public ApiKeyForm()
        {
            Text = "Configurar asistente IA";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(560, 260);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Font = new Font("Segoe UI", 9.5f);

            var info = new Label
            {
                Dock = DockStyle.Top,
                Height = 76,
                Padding = new Padding(12, 10, 12, 0),
                Text = "El asistente usa la API de Claude (Anthropic) y necesita una API key.\n" +
                       "Puedes crearla en platform.claude.com (requiere cuenta y crédito de API).\n" +
                       "El uso de la API tiene coste según el modelo y los tokens consumidos.",
            };

            var link = new LinkLabel
            {
                Dock = DockStyle.Top,
                Height = 24,
                Padding = new Padding(12, 0, 12, 0),
                Text = "Abrir platform.claude.com para crear una API key",
            };
            link.LinkClicked += (s, e) =>
            {
                try { Process.Start(new ProcessStartInfo("https://platform.claude.com") { UseShellExecute = true }); }
                catch { }
            };

            var keyLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 22,
                Padding = new Padding(12, 4, 12, 0),
                Text = "API key (empieza por \"sk-ant-\"):",
            };

            var keyPanel = new Panel { Dock = DockStyle.Top, Height = 32, Padding = new Padding(12, 2, 12, 2) };
            _keyBox = new TextBox { Dock = DockStyle.Fill, UseSystemPasswordChar = true };
            keyPanel.Controls.Add(_keyBox);

            var warning = new Label
            {
                Dock = DockStyle.Top,
                Height = 34,
                Padding = new Padding(12, 2, 12, 0),
                ForeColor = Color.DimGray,
                Text = "Se guardará en %AppData%\\LinkWorksetInspector (texto plano). Como alternativa,\n" +
                       "define la variable de entorno ANTHROPIC_API_KEY y no se guardará nada.",
            };

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                Height = 44,
                Padding = new Padding(8),
            };
            var cancel = new Button { Text = "Cancelar", Width = 100, DialogResult = DialogResult.Cancel };
            var ok = new Button { Text = "Guardar", Width = 100, DialogResult = DialogResult.OK };
            ok.Click += (s, e) =>
            {
                if (ApiKey.Length < 10)
                {
                    MessageBox.Show(this, "Introduce una API key válida.", "Asistente IA",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    DialogResult = DialogResult.None;
                }
            };
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(ok);
            AcceptButton = ok;
            CancelButton = cancel;

            Controls.Add(buttons);
            Controls.Add(warning);
            Controls.Add(keyPanel);
            Controls.Add(keyLabel);
            Controls.Add(link);
            Controls.Add(info);
        }
    }
}
