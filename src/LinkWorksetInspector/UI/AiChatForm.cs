using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using LinkWorksetInspector.Services.Ai;

namespace LinkWorksetInspector.UI
{
    /// <summary>
    /// Ventana de chat con el asistente IA. Es modal dentro del comando de Revit,
    /// así que las continuaciones async y la ejecución de herramientas ocurren en
    /// el hilo de la UI de Revit, con contexto de API válido.
    /// </summary>
    public class AiChatForm : Form
    {
        private readonly ClaudeChatService _service;
        private readonly RichTextBox _conversation;
        private readonly TextBox _input;
        private readonly Button _sendButton;
        private readonly Label _status;
        private bool _busy;

        public AiChatForm(ClaudeChatService service, string docTitle)
        {
            _service = service;
            _service.StatusChanged += OnServiceStatus;

            Text = "Asistente IA — " + docTitle;
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(760, 640);
            MinimumSize = new Size(520, 400);
            Font = new Font("Segoe UI", 9.5f);
            ShowIcon = false;
            MinimizeBox = false;

            _conversation = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.White,
                BorderStyle = BorderStyle.None,
                HideSelection = false,
            };

            _status = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 22,
                Padding = new Padding(10, 2, 10, 2),
                ForeColor = Color.DimGray,
                Text = "",
            };

            var inputPanel = new Panel { Dock = DockStyle.Bottom, Height = 76, Padding = new Padding(8) };
            _sendButton = new Button { Text = "Enviar", Dock = DockStyle.Right, Width = 100 };
            _sendButton.Click += async (s, e) => await SendAsync();
            _input = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                AcceptsReturn = false,
                ScrollBars = ScrollBars.Vertical,
            };
            _input.KeyDown += async (s, e) =>
            {
                if (e.KeyCode == Keys.Enter && !e.Shift)
                {
                    e.SuppressKeyPress = true;
                    await SendAsync();
                }
            };
            inputPanel.Controls.Add(_input);
            inputPanel.Controls.Add(_sendButton);

            Controls.Add(_conversation);
            Controls.Add(_status);
            Controls.Add(inputPanel);
            _conversation.BringToFront();

            AppendSystem("Pregúntame sobre el modelo abierto: \"¿cuántas puertas hay por nivel?\", " +
                         "\"¿qué vínculos están descargados y en qué workset?\", \"¿qué advertencias tiene el " +
                         "modelo?\", \"selecciona los muros del nivel 2\"…  (Enter envía, Mayús+Enter hace salto de línea)");

            FormClosing += (s, e) =>
            {
                if (_busy)
                {
                    MessageBox.Show(this, "Espera a que termine la respuesta actual antes de cerrar.",
                        "Asistente IA", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    e.Cancel = true;
                }
            };
        }

        private async Task SendAsync()
        {
            if (_busy) return;
            string question = _input.Text.Trim();
            if (question.Length == 0) return;

            _busy = true;
            _input.Text = "";
            _input.Enabled = false;
            _sendButton.Enabled = false;
            AppendUser(question);

            try
            {
                string answer = await _service.SendAsync(question);
                AppendAssistant(answer);
            }
            catch (Exception ex)
            {
                AppendError(TranslateError(ex));
            }
            finally
            {
                _busy = false;
                _status.Text = "";
                if (!IsDisposed)
                {
                    _input.Enabled = true;
                    _sendButton.Enabled = true;
                    _input.Focus();
                }
            }
        }

        private static string TranslateError(Exception ex)
        {
            string typeName = ex.GetType().Name;
            if (typeName.Contains("Unauthorized"))
                return "La API key no es válida o fue revocada. Revisa la configuración (consola de Anthropic).";
            if (typeName.Contains("RateLimit"))
                return "Se alcanzó el límite de peticiones de la API. Espera un momento y vuelve a intentarlo.";
            if (typeName.Contains("IO") || typeName.Contains("Connection") || typeName.Contains("Http"))
                return "No se pudo conectar con la API de Anthropic. Revisa tu conexión a internet o el proxy.";
            return "Error inesperado: " + ex.Message;
        }

        private void OnServiceStatus(string message)
        {
            if (IsDisposed) return;
            _status.Text = message;
            _status.Refresh();
        }

        // ---------------------------------------------------------------- render

        private void AppendUser(string text) => AppendBlock("Tú", Color.FromArgb(0x0B, 0x3D, 0x91), text);

        private void AppendAssistant(string text) => AppendBlock("Asistente", Color.FromArgb(0x1B, 0x7A, 0x43), text);

        private void AppendError(string text) => AppendBlock("Error", Color.Firebrick, text);

        private void AppendSystem(string text)
        {
            _conversation.SelectionStart = _conversation.TextLength;
            _conversation.SelectionColor = Color.Gray;
            _conversation.SelectionFont = new Font(Font, FontStyle.Italic);
            _conversation.AppendText(text + Environment.NewLine + Environment.NewLine);
            _conversation.SelectionFont = Font;
            _conversation.SelectionColor = Color.Black;
        }

        private void AppendBlock(string author, Color color, string text)
        {
            if (IsDisposed) return;
            _conversation.SelectionStart = _conversation.TextLength;
            _conversation.SelectionColor = color;
            _conversation.SelectionFont = new Font(Font, FontStyle.Bold);
            _conversation.AppendText(author + ":" + Environment.NewLine);
            _conversation.SelectionFont = Font;
            _conversation.SelectionColor = Color.Black;
            _conversation.AppendText(text.Trim() + Environment.NewLine + Environment.NewLine);
            _conversation.SelectionStart = _conversation.TextLength;
            _conversation.ScrollToCaret();
        }
    }
}
