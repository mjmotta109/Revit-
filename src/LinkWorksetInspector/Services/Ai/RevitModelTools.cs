using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace LinkWorksetInspector.Services.Ai
{
    /// <summary>
    /// Ejecuta las herramientas que la IA puede invocar sobre el modelo abierto.
    /// Todas son de solo lectura salvo "seleccionar_elementos" (cambia la selección,
    /// no el modelo). Debe ejecutarse en el hilo de la interfaz de Revit, dentro de
    /// un contexto válido de la API (el formulario de chat es modal dentro del comando).
    /// </summary>
    public class RevitModelTools
    {
        private readonly UIDocument _uidoc;
        private readonly Document _doc;

        public RevitModelTools(UIDocument uidoc)
        {
            _uidoc = uidoc;
            _doc = uidoc.Document;
        }

        /// <summary>Punto de entrada: nombre de herramienta + argumentos JSON → resultado en texto.</summary>
        public string Execute(string toolName, IReadOnlyDictionary<string, JsonElement> input)
        {
            try
            {
                switch (toolName)
                {
                    case "info_proyecto": return InfoProyecto();
                    case "listar_niveles": return ListarNiveles();
                    case "listar_worksets": return ListarWorksets();
                    case "listar_categorias": return ListarCategorias(GetString(input, "filtro"));
                    case "contar_elementos":
                        return ContarElementos(GetString(input, "categoria"), GetString(input, "nivel"),
                            GetString(input, "workset"), GetBool(input, "agrupar_por_nivel"));
                    case "buscar_elementos":
                        return BuscarElementos(GetString(input, "categoria"), GetString(input, "texto"),
                            GetInt(input, "limite") ?? 30);
                    case "estado_vinculos": return EstadoVinculos();
                    case "listar_advertencias": return ListarAdvertencias(GetInt(input, "limite") ?? 20);
                    case "parametros_elemento": return ParametrosElemento(GetLong(input, "id"));
                    case "seleccionar_elementos": return SeleccionarElementos(GetLongArray(input, "ids"));
                    default:
                        return "Error: herramienta desconocida «" + toolName + "».";
                }
            }
            catch (Exception ex)
            {
                return "Error al ejecutar la herramienta: " + ex.Message;
            }
        }

        // ------------------------------------------------------------------ herramientas

        private string InfoProyecto()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Archivo: " + _doc.Title);
            try { sb.AppendLine("Ruta: " + (_doc.PathName ?? "(sin guardar)")); } catch { }
            sb.AppendLine("Trabajo compartido (worksets): " + (_doc.IsWorkshared ? "sí" : "no"));

            try
            {
                ProjectInfo info = _doc.ProjectInformation;
                if (info != null)
                {
                    sb.AppendLine("Nombre del proyecto: " + info.Name);
                    sb.AppendLine("Número del proyecto: " + info.Number);
                    sb.AppendLine("Cliente: " + info.ClientName);
                    sb.AppendLine("Dirección: " + info.Address);
                }
            }
            catch { }

            View activeView = _doc.ActiveView;
            if (activeView != null)
                sb.AppendLine("Vista activa: " + activeView.Name + " (" + activeView.ViewType + ")");

            sb.AppendLine("Niveles: " + new FilteredElementCollector(_doc).OfClass(typeof(Level)).GetElementCount());
            sb.AppendLine("Vistas: " + new FilteredElementCollector(_doc).OfClass(typeof(View)).GetElementCount());
            sb.AppendLine("Planos: " + new FilteredElementCollector(_doc).OfClass(typeof(ViewSheet)).GetElementCount());
            sb.AppendLine("Vínculos RVT: " + new FilteredElementCollector(_doc).OfClass(typeof(RevitLinkType)).GetElementCount());
            sb.AppendLine("Advertencias: " + _doc.GetWarnings().Count);
            return sb.ToString();
        }

        private string ListarNiveles()
        {
            var levels = new FilteredElementCollector(_doc)
                .OfClass(typeof(Level)).Cast<Level>()
                .OrderBy(l => l.Elevation).ToList();
            if (levels.Count == 0) return "El modelo no tiene niveles.";

            var sb = new StringBuilder();
            sb.AppendLine("Niveles (de abajo arriba, elevación en unidades internas convertida a metros):");
            foreach (Level level in levels)
            {
                double meters = UnitUtils.ConvertFromInternalUnits(level.Elevation, UnitTypeId.Meters);
                sb.AppendLine("- " + level.Name + "  (elev. " + meters.ToString("0.00") + " m, id " + level.Id + ")");
            }
            return sb.ToString();
        }

        private string ListarWorksets()
        {
            if (!_doc.IsWorkshared)
                return "Este modelo no es de trabajo compartido: no tiene worksets (subproyectos).";

            var worksets = new FilteredWorksetCollector(_doc)
                .OfKind(WorksetKind.UserWorkset).ToWorksets()
                .OrderBy(w => w.Name).ToList();

            var sb = new StringBuilder();
            sb.AppendLine("Worksets de usuario (" + worksets.Count + "):");
            foreach (Workset ws in worksets)
            {
                sb.AppendLine("- " + ws.Name +
                    "  [" + (ws.IsOpen ? "abierto" : "CERRADO") + "]" +
                    (ws.IsVisibleByDefault ? "" : "  [oculto por defecto en vistas]") +
                    (string.IsNullOrEmpty(ws.Owner) ? "" : "  propietario: " + ws.Owner));
            }
            return sb.ToString();
        }

        private string ListarCategorias(string filtro)
        {
            var names = new List<string>();
            foreach (Category cat in _doc.Settings.Categories)
            {
                if (cat == null || cat.CategoryType != CategoryType.Model) continue;
                if (!string.IsNullOrEmpty(filtro) &&
                    cat.Name.IndexOf(filtro, StringComparison.OrdinalIgnoreCase) < 0) continue;
                names.Add(cat.Name);
            }
            names.Sort(StringComparer.OrdinalIgnoreCase);
            if (names.Count == 0)
                return "No se encontraron categorías de modelo" +
                       (string.IsNullOrEmpty(filtro) ? "." : " que contengan «" + filtro + "».");
            return "Categorías de modelo disponibles (" + names.Count + "):\n- " + string.Join("\n- ", names);
        }

        private string ContarElementos(string categoria, string nivel, string workset, bool agruparPorNivel)
        {
            if (string.IsNullOrWhiteSpace(categoria))
                return "Error: falta el argumento «categoria». Usa listar_categorias para ver los nombres válidos.";

            Category cat = FindCategory(categoria);
            if (cat == null)
                return "No existe una categoría llamada «" + categoria + "». " + SugerirCategorias(categoria);

            IList<Element> elements = new FilteredElementCollector(_doc)
                .OfCategoryId(cat.Id)
                .WhereElementIsNotElementType()
                .ToElements();

            // Filtro por workset
            if (!string.IsNullOrWhiteSpace(workset))
            {
                if (!_doc.IsWorkshared) return "El modelo no tiene worksets; no se puede filtrar por workset.";
                WorksetTable table = _doc.GetWorksetTable();
                elements = elements.Where(e =>
                {
                    try
                    {
                        Workset ws = table.GetWorkset(e.WorksetId);
                        return ws != null && ws.Name.IndexOf(workset, StringComparison.OrdinalIgnoreCase) >= 0;
                    }
                    catch { return false; }
                }).ToList();
            }

            // Filtro / agrupación por nivel
            if (!string.IsNullOrWhiteSpace(nivel))
            {
                elements = elements.Where(e => LevelNameOf(e).IndexOf(nivel, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
                return "Elementos de «" + cat.Name + "» en el nivel «" + nivel + "»" +
                       (string.IsNullOrWhiteSpace(workset) ? "" : " (workset «" + workset + "»)") +
                       ": " + elements.Count;
            }

            if (agruparPorNivel)
            {
                var groups = elements.GroupBy(e => LevelNameOf(e))
                    .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);
                var sb = new StringBuilder();
                sb.AppendLine("Elementos de «" + cat.Name + "» por nivel (total " + elements.Count + "):");
                foreach (var g in groups)
                    sb.AppendLine("- " + (string.IsNullOrEmpty(g.Key) ? "(sin nivel asociado)" : g.Key) + ": " + g.Count());
                return sb.ToString();
            }

            return "Elementos de «" + cat.Name + "»" +
                   (string.IsNullOrWhiteSpace(workset) ? "" : " en workset «" + workset + "»") +
                   ": " + elements.Count;
        }

        private string BuscarElementos(string categoria, string texto, int limite)
        {
            if (string.IsNullOrWhiteSpace(categoria))
                return "Error: falta el argumento «categoria». Usa listar_categorias para ver los nombres válidos.";

            Category cat = FindCategory(categoria);
            if (cat == null)
                return "No existe una categoría llamada «" + categoria + "». " + SugerirCategorias(categoria);

            if (limite < 1) limite = 1;
            if (limite > 100) limite = 100;

            var matches = new List<string>();
            int total = 0;
            foreach (Element e in new FilteredElementCollector(_doc)
                         .OfCategoryId(cat.Id).WhereElementIsNotElementType())
            {
                string name;
                try { name = e.Name; } catch { name = ""; }
                if (!string.IsNullOrEmpty(texto) &&
                    (name ?? "").IndexOf(texto, StringComparison.OrdinalIgnoreCase) < 0) continue;

                total++;
                if (matches.Count < limite)
                {
                    string levelName = LevelNameOf(e);
                    matches.Add("- id " + e.Id + ": " + (string.IsNullOrEmpty(name) ? "(sin nombre)" : name) +
                                (string.IsNullOrEmpty(levelName) ? "" : "  [nivel: " + levelName + "]"));
                }
            }

            if (total == 0)
                return "No se encontraron elementos de «" + cat.Name + "»" +
                       (string.IsNullOrEmpty(texto) ? "." : " cuyo nombre contenga «" + texto + "».");

            var sb = new StringBuilder();
            sb.AppendLine("Encontrados " + total + " elementos de «" + cat.Name + "»" +
                          (string.IsNullOrEmpty(texto) ? "" : " con «" + texto + "» en el nombre") +
                          (total > matches.Count ? " (se muestran los primeros " + matches.Count + ")" : "") + ":");
            foreach (string m in matches) sb.AppendLine(m);
            return sb.ToString();
        }

        private string EstadoVinculos()
        {
            LinkWorksetReport report = LinkWorksetAnalyzer.Analyze(_doc, _doc.ActiveView);
            if (report.Rows.Count == 0) return "El modelo no contiene vínculos RVT ni CAD.";

            var sb = new StringBuilder();
            sb.AppendLine("Vínculos del modelo (" + report.Rows.Count + " filas):");
            foreach (var r in report.Rows)
            {
                sb.AppendLine("- " + r.LinkName + " [" + r.Format + "] · estado: " + r.LoadStatus +
                              " · workset del tipo: " + r.TypeWorkset + " (" + r.TypeWorksetState + ")" +
                              " · workset de instancia: " + r.InstanceWorkset + " (" + r.InstanceWorksetState + ")" +
                              " · visible en vista activa: " + r.VisibleInActiveView);
                if (r.HasProblem) sb.AppendLine("  PROBLEMA: " + r.Diagnosis);
            }
            if (report.ClosedWorksetsWithLinks.Count > 0)
                sb.AppendLine("Worksets CERRADOS con vínculos afectados: " +
                              string.Join(", ", report.ClosedWorksetsWithLinks));
            return sb.ToString();
        }

        private string ListarAdvertencias(int limite)
        {
            IList<FailureMessage> warnings = _doc.GetWarnings();
            if (warnings.Count == 0) return "El modelo no tiene advertencias. 🎉";

            if (limite < 1) limite = 1;
            if (limite > 100) limite = 100;

            var groups = warnings
                .GroupBy(w => { try { return w.GetDescriptionText(); } catch { return "(sin descripción)"; } })
                .OrderByDescending(g => g.Count())
                .Take(limite)
                .ToList();

            var sb = new StringBuilder();
            sb.AppendLine("Advertencias del modelo: " + warnings.Count + " en total. Tipos más frecuentes:");
            foreach (var g in groups)
                sb.AppendLine("- (" + g.Count() + "×) " + g.Key);
            return sb.ToString();
        }

        private string ParametrosElemento(long? id)
        {
            if (id == null) return "Error: falta el argumento «id» (número de id del elemento).";

            Element e = _doc.GetElement(new ElementId((int)id.Value));
            if (e == null) return "No existe ningún elemento con id " + id + " en este modelo.";

            var sb = new StringBuilder();
            string name;
            try { name = e.Name; } catch { name = "(sin nombre)"; }
            sb.AppendLine("Elemento id " + id + ": " + name +
                          (e.Category != null ? "  [categoría: " + e.Category.Name + "]" : ""));

            var lines = new List<string>();
            foreach (Parameter p in e.Parameters)
            {
                try
                {
                    if (p == null || p.Definition == null) continue;
                    string value;
                    switch (p.StorageType)
                    {
                        case StorageType.String: value = p.AsString(); break;
                        case StorageType.Integer: value = p.AsInteger().ToString(); break;
                        case StorageType.Double: value = p.AsValueString(); break;
                        case StorageType.ElementId: value = p.AsValueString() ?? p.AsElementId().ToString(); break;
                        default: value = p.AsValueString(); break;
                    }
                    if (string.IsNullOrEmpty(value)) continue;
                    lines.Add("- " + p.Definition.Name + ": " + value);
                }
                catch { }
            }
            lines.Sort(StringComparer.OrdinalIgnoreCase);
            foreach (string line in lines.Take(80)) sb.AppendLine(line);
            if (lines.Count > 80) sb.AppendLine("… y " + (lines.Count - 80) + " parámetros más.");
            return sb.ToString();
        }

        private string SeleccionarElementos(List<long> ids)
        {
            if (ids == null || ids.Count == 0)
                return "Error: falta el argumento «ids» (lista de ids de elementos).";

            var elementIds = new List<ElementId>();
            foreach (long id in ids)
            {
                var eid = new ElementId((int)id);
                if (_doc.GetElement(eid) != null) elementIds.Add(eid);
            }
            if (elementIds.Count == 0) return "Ninguno de los ids indicados existe en el modelo.";

            _uidoc.Selection.SetElementIds(elementIds);
            try { _uidoc.ShowElements(elementIds); } catch { /* algunas vistas no permiten zoom */ }

            return "Seleccionados " + elementIds.Count + " elementos en Revit" +
                   (elementIds.Count < ids.Count ? " (" + (ids.Count - elementIds.Count) + " ids no existían)" : "") + ".";
        }

        // ------------------------------------------------------------------ auxiliares

        /// <summary>Resumen breve del modelo para el prompt de sistema.</summary>
        public string BuildSnapshot()
        {
            var sb = new StringBuilder();
            try
            {
                sb.AppendLine("Modelo abierto: " + _doc.Title);
                sb.AppendLine("Trabajo compartido: " + (_doc.IsWorkshared ? "sí" : "no"));
                View view = _doc.ActiveView;
                if (view != null) sb.AppendLine("Vista activa: " + view.Name);
                sb.AppendLine("Niveles: " + new FilteredElementCollector(_doc).OfClass(typeof(Level)).GetElementCount() +
                              " · Vínculos RVT: " + new FilteredElementCollector(_doc).OfClass(typeof(RevitLinkType)).GetElementCount() +
                              " · Advertencias: " + _doc.GetWarnings().Count);
            }
            catch { }
            return sb.ToString();
        }

        private Category FindCategory(string name)
        {
            Category exact = null;
            Category partial = null;
            foreach (Category cat in _doc.Settings.Categories)
            {
                if (cat == null) continue;
                if (string.Equals(cat.Name, name, StringComparison.OrdinalIgnoreCase)) { exact = cat; break; }
                if (partial == null && cat.CategoryType == CategoryType.Model &&
                    cat.Name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0) partial = cat;
            }
            return exact ?? partial;
        }

        private string SugerirCategorias(string name)
        {
            var similares = new List<string>();
            string firstWord = name.Split(' ')[0];
            foreach (Category cat in _doc.Settings.Categories)
            {
                if (cat == null || cat.CategoryType != CategoryType.Model) continue;
                if (cat.Name.IndexOf(firstWord, StringComparison.OrdinalIgnoreCase) >= 0)
                    similares.Add(cat.Name);
                if (similares.Count >= 8) break;
            }
            return similares.Count > 0
                ? "Categorías parecidas: " + string.Join(", ", similares) + ". También puedes usar listar_categorias."
                : "Usa la herramienta listar_categorias para ver los nombres válidos (dependen del idioma de Revit).";
        }

        private string LevelNameOf(Element e)
        {
            try
            {
                if (e.LevelId != null && e.LevelId != ElementId.InvalidElementId)
                {
                    var level = _doc.GetElement(e.LevelId) as Level;
                    if (level != null) return level.Name;
                }
            }
            catch { }
            return "";
        }

        private static string GetString(IReadOnlyDictionary<string, JsonElement> input, string key)
        {
            if (input != null && input.TryGetValue(key, out JsonElement value) &&
                value.ValueKind == JsonValueKind.String)
                return value.GetString();
            return null;
        }

        private static bool GetBool(IReadOnlyDictionary<string, JsonElement> input, string key)
        {
            return input != null && input.TryGetValue(key, out JsonElement value) &&
                   value.ValueKind == JsonValueKind.True;
        }

        private static int? GetInt(IReadOnlyDictionary<string, JsonElement> input, string key)
        {
            if (input != null && input.TryGetValue(key, out JsonElement value) &&
                value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int n))
                return n;
            return null;
        }

        private static long? GetLong(IReadOnlyDictionary<string, JsonElement> input, string key)
        {
            if (input != null && input.TryGetValue(key, out JsonElement value) &&
                value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out long n))
                return n;
            return null;
        }

        private static List<long> GetLongArray(IReadOnlyDictionary<string, JsonElement> input, string key)
        {
            var result = new List<long>();
            if (input != null && input.TryGetValue(key, out JsonElement value) &&
                value.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in value.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Number && item.TryGetInt64(out long n))
                        result.Add(n);
                }
            }
            return result;
        }
    }
}
