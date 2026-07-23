using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LinkWorksetInspector.Services.Ai;
using LinkWorksetInspector.UI;

namespace LinkWorksetInspector.Commands
{
    /// <summary>
    /// Abre el asistente IA: un chat (Claude, API de Anthropic) con herramientas
    /// de solo lectura sobre el modelo abierto. La ventana es modal, de modo que
    /// las herramientas se ejecutan dentro del contexto de API de este comando.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class AiAssistantCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIDocument uidoc = commandData.Application.ActiveUIDocument;
                if (uidoc == null)
                {
                    message = "No hay ningún documento abierto.";
                    return Result.Cancelled;
                }

                // --- API key ---
                AiSettings settings = AiSettings.Load();
                if (!settings.HasApiKey)
                {
                    using (var keyForm = new ApiKeyForm())
                    {
                        if (keyForm.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                            return Result.Cancelled;
                        settings.ApiKey = keyForm.ApiKey;
                        try { settings.Save(); }
                        catch (Exception ex)
                        {
                            TaskDialog.Show("Asistente IA",
                                "No se pudo guardar la configuración: " + ex.Message +
                                "\nLa clave se usará solo en esta sesión.");
                        }
                    }
                }

                // --- servicio de chat con herramientas del modelo ---
                var tools = new RevitModelTools(uidoc);
                string systemPrompt = BuildSystemPrompt(tools.BuildSnapshot());

                var service = new ClaudeChatService(settings.ApiKey, settings.Model, systemPrompt, tools.Execute);

                using (var form = new AiChatForm(service, uidoc.Document.Title))
                {
                    IntPtr revitWindow = IntPtr.Zero;
                    try { revitWindow = commandData.Application.MainWindowHandle; } catch { }

                    if (revitWindow != IntPtr.Zero)
                        form.ShowDialog(new RevitOwnerWindow(revitWindow));
                    else
                        form.ShowDialog();
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.ToString();
                return Result.Failed;
            }
        }

        private static string BuildSystemPrompt(string snapshot)
        {
            return
"Eres un asistente BIM integrado en Autodesk Revit mediante un add-in. El usuario es un profesional " +
"AEC hispanohablante y te pregunta sobre el modelo que tiene abierto.\n\n" +
"Reglas:\n" +
"- Responde en el idioma del usuario (normalmente español), de forma clara y concisa.\n" +
"- Para cualquier dato del modelo (conteos, vínculos, worksets, advertencias, parámetros) USA las " +
"herramientas disponibles en lugar de suponer o inventar. Si una herramienta devuelve un error con " +
"sugerencias (por ejemplo nombres de categoría parecidos), reintenta con esos valores.\n" +
"- Los nombres de categorías, niveles y worksets dependen del idioma y del proyecto: si dudas, usa " +
"listar_categorias, listar_niveles o listar_worksets primero.\n" +
"- No puedes modificar el modelo: tus herramientas son de solo lectura, salvo seleccionar_elementos, " +
"que solo cambia la selección en pantalla.\n" +
"- Cuando el usuario pregunte por vínculos (links) que no aparecen, usa estado_vinculos y explica el " +
"diagnóstico con los pasos de la interfaz de Revit en español (Colaborar ▸ Gestionar colaboración ▸ " +
"Subproyectos; Gestionar vínculos).\n" +
"- Presenta los resultados numéricos en listas legibles; no muestres ids salvo que sean útiles.\n\n" +
"Estado del modelo al abrir el chat:\n" + snapshot;
        }
    }
}
