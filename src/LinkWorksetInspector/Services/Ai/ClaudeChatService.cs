using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Anthropic;
using Anthropic.Models.Messages;

namespace LinkWorksetInspector.Services.Ai
{
    /// <summary>
    /// Conversación con Claude (API de Anthropic) con herramientas que consultan
    /// el modelo de Revit. Mantiene el historial completo y ejecuta el bucle
    /// agéntico: petición → tool_use → ejecutar herramienta → tool_result → repetir.
    /// El ejecutor de herramientas corre en el mismo hilo (el de la UI de Revit),
    /// porque las continuaciones async se reanudan en el bucle de mensajes del
    /// formulario modal.
    /// </summary>
    public class ClaudeChatService
    {
        private const int MaxToolIterations = 30;

        private readonly AnthropicClient _client;
        private readonly string _model;
        private readonly string _systemPrompt;
        private readonly Func<string, IReadOnlyDictionary<string, JsonElement>, string> _toolExecutor;
        private readonly List<MessageParam> _history = new List<MessageParam>();

        /// <summary>Avisos de progreso para la UI ("Consultando el modelo…", "Ejecutando herramienta…").</summary>
        public event Action<string> StatusChanged;

        public ClaudeChatService(string apiKey, string model, string systemPrompt,
            Func<string, IReadOnlyDictionary<string, JsonElement>, string> toolExecutor)
        {
            _client = new AnthropicClient { ApiKey = apiKey };
            _model = string.IsNullOrWhiteSpace(model) ? "claude-opus-4-8" : model;
            _systemPrompt = systemPrompt;
            _toolExecutor = toolExecutor;
        }

        public async Task<string> SendAsync(string userMessage)
        {
            _history.Add(new MessageParam { Role = Role.User, Content = userMessage });

            var finalText = new StringBuilder();

            for (int iteration = 0; iteration < MaxToolIterations; iteration++)
            {
                StatusChanged?.Invoke("Consultando a Claude…");

                var parameters = new MessageCreateParams
                {
                    Model = _model,
                    MaxTokens = 16000,
                    Thinking = new ThinkingConfigAdaptive(),
                    System = new List<TextBlockParam> { new TextBlockParam { Text = _systemPrompt } },
                    Tools = BuildTools(),
                    Messages = new List<MessageParam>(_history),
                };

                Message response = await _client.Messages.Create(parameters).ConfigureAwait(true);

                if (response.StopReason == "refusal")
                {
                    return "Claude declinó responder a esta petición por políticas de seguridad. " +
                           "Prueba a reformularla.";
                }

                // Reconstruir el turno del asistente y ejecutar sus herramientas.
                var assistantContent = new List<ContentBlockParam>();
                var toolResults = new List<ContentBlockParam>();
                finalText.Clear();

                foreach (ContentBlock block in response.Content)
                {
                    if (block.TryPickText(out TextBlock text))
                    {
                        assistantContent.Add(new TextBlockParam { Text = text.Text });
                        finalText.AppendLine(text.Text);
                    }
                    else if (block.TryPickThinking(out ThinkingBlock thinking))
                    {
                        // La firma debe conservarse intacta al devolver el bloque.
                        assistantContent.Add(new ThinkingBlockParam
                        {
                            Thinking = thinking.Thinking,
                            Signature = thinking.Signature,
                        });
                    }
                    else if (block.TryPickRedactedThinking(out RedactedThinkingBlock redacted))
                    {
                        assistantContent.Add(new RedactedThinkingBlockParam { Data = redacted.Data });
                    }
                    else if (block.TryPickToolUse(out ToolUseBlock toolUse))
                    {
                        assistantContent.Add(new ToolUseBlockParam
                        {
                            ID = toolUse.ID,
                            Name = toolUse.Name,
                            Input = toolUse.Input,
                        });

                        StatusChanged?.Invoke("Ejecutando herramienta: " + toolUse.Name + "…");
                        string result;
                        try
                        {
                            result = _toolExecutor(toolUse.Name, toolUse.Input)
                                     ?? "(la herramienta no devolvió nada)";
                        }
                        catch (Exception ex)
                        {
                            result = "Error al ejecutar la herramienta: " + ex.Message;
                        }

                        toolResults.Add(new ToolResultBlockParam
                        {
                            ToolUseID = toolUse.ID,
                            Content = result,
                        });
                    }
                }

                _history.Add(new MessageParam { Role = Role.Assistant, Content = assistantContent });

                if (response.StopReason == "tool_use" && toolResults.Count > 0)
                {
                    _history.Add(new MessageParam { Role = Role.User, Content = toolResults });
                    continue;
                }

                string answer = finalText.ToString().Trim();
                return answer.Length > 0 ? answer : "(Claude no devolvió texto en esta respuesta.)";
            }

            return "Se alcanzó el límite de pasos de herramientas sin una respuesta final. " +
                   "Prueba con una pregunta más concreta.";
        }

        /// <summary>Definición de las herramientas que Claude puede invocar sobre el modelo.</summary>
        private static List<ToolUnion> BuildTools()
        {
            return new List<ToolUnion>
            {
                MakeTool("info_proyecto",
                    "Devuelve información general del modelo abierto: nombre, ruta, proyecto, vista activa y conteos básicos (niveles, vistas, planos, vínculos, advertencias).",
                    new Dictionary<string, object>(), new List<string>()),

                MakeTool("listar_niveles",
                    "Lista los niveles del modelo con su elevación en metros y su id.",
                    new Dictionary<string, object>(), new List<string>()),

                MakeTool("listar_worksets",
                    "Lista los worksets (subproyectos) de usuario del modelo, indicando si cada uno está abierto o cerrado, su visibilidad por defecto y su propietario.",
                    new Dictionary<string, object>(), new List<string>()),

                MakeTool("listar_categorias",
                    "Lista los nombres de las categorías de modelo disponibles (en el idioma de Revit del usuario). Úsala cuando contar_elementos o buscar_elementos no encuentren la categoría pedida.",
                    new Dictionary<string, object>
                    {
                        ["filtro"] = new { type = "string", description = "Texto opcional para filtrar los nombres de categoría (p. ej. 'muro')." },
                    }, new List<string>()),

                MakeTool("contar_elementos",
                    "Cuenta los elementos colocados de una categoría, con filtros opcionales por nivel y workset, o agrupados por nivel. Llama a esta herramienta siempre que el usuario pregunte cuántos elementos hay.",
                    new Dictionary<string, object>
                    {
                        ["categoria"] = new { type = "string", description = "Nombre de la categoría tal como aparece en Revit (p. ej. 'Muros', 'Puertas'). Acepta coincidencia parcial." },
                        ["nivel"] = new { type = "string", description = "Nombre (o parte) del nivel para filtrar. Opcional." },
                        ["workset"] = new { type = "string", description = "Nombre (o parte) del workset para filtrar. Opcional." },
                        ["agrupar_por_nivel"] = new { type = "boolean", description = "true para devolver el desglose por nivel. Opcional." },
                    }, new List<string> { "categoria" }),

                MakeTool("buscar_elementos",
                    "Busca elementos de una categoría cuyo nombre contenga un texto, devolviendo sus ids, nombres y niveles. Útil antes de seleccionar_elementos o parametros_elemento.",
                    new Dictionary<string, object>
                    {
                        ["categoria"] = new { type = "string", description = "Nombre de la categoría en Revit." },
                        ["texto"] = new { type = "string", description = "Texto a buscar dentro del nombre del elemento. Opcional (sin él lista los primeros)." },
                        ["limite"] = new { type = "integer", description = "Máximo de resultados a devolver (1-100, por defecto 30)." },
                    }, new List<string> { "categoria" }),

                MakeTool("estado_vinculos",
                    "Analiza todos los vínculos RVT y CAD del modelo: estado de carga, workset del tipo y de la instancia (abierto/cerrado), visibilidad y diagnóstico de por qué un vínculo no se ve. Llama a esta herramienta para cualquier pregunta sobre vínculos o links.",
                    new Dictionary<string, object>(), new List<string>()),

                MakeTool("listar_advertencias",
                    "Devuelve las advertencias (warnings) del modelo agrupadas por tipo y ordenadas por frecuencia.",
                    new Dictionary<string, object>
                    {
                        ["limite"] = new { type = "integer", description = "Máximo de tipos de advertencia a devolver (1-100, por defecto 20)." },
                    }, new List<string>()),

                MakeTool("parametros_elemento",
                    "Devuelve los parámetros con valor de un elemento concreto, dado su id.",
                    new Dictionary<string, object>
                    {
                        ["id"] = new { type = "integer", description = "Id del elemento (obtenido con buscar_elementos)." },
                    }, new List<string> { "id" }),

                MakeTool("seleccionar_elementos",
                    "Selecciona en Revit los elementos indicados por id y hace zoom hacia ellos. Es la única herramienta que cambia algo (solo la selección; nunca modifica el modelo).",
                    new Dictionary<string, object>
                    {
                        ["ids"] = new
                        {
                            type = "array",
                            items = new { type = "integer" },
                            description = "Lista de ids de elementos a seleccionar.",
                        },
                    }, new List<string> { "ids" }),
            };
        }

        private static Tool MakeTool(string name, string description,
            Dictionary<string, object> properties, List<string> required)
        {
            var jsonProperties = new Dictionary<string, JsonElement>();
            foreach (KeyValuePair<string, object> kv in properties)
                jsonProperties[kv.Key] = JsonSerializer.SerializeToElement(kv.Value);

            return new Tool
            {
                Name = name,
                Description = description,
                InputSchema = new()
                {
                    Properties = jsonProperties,
                    Required = required,
                },
            };
        }
    }
}
