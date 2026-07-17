using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.Revit.DB;
using LinkWorksetInspector.Models;

namespace LinkWorksetInspector.Services
{
    /// <summary>Resultado completo del análisis de vínculos del documento.</summary>
    public class LinkWorksetReport
    {
        public List<LinkReportRow> Rows { get; set; } = new List<LinkReportRow>();

        public bool IsWorkshared { get; set; }

        /// <summary>
        /// Worksets cerrados que contienen vínculos afectados. Esta es la respuesta
        /// directa a "¿qué workset tengo que abrir?" sin abrirlos todos.
        /// </summary>
        public List<string> ClosedWorksetsWithLinks { get; set; } = new List<string>();
    }

    /// <summary>
    /// Recorre todos los tipos de vínculo (RVT y CAD) del documento —incluidos los
    /// que no están cargados pero siguen registrados— y determina en qué workset
    /// está cada uno, si ese workset está cerrado y por qué el vínculo no se ve.
    /// </summary>
    public static class LinkWorksetAnalyzer
    {
        public static LinkWorksetReport Analyze(Document doc, View activeView)
        {
            var report = new LinkWorksetReport { IsWorkshared = doc.IsWorkshared };
            var closedWorksets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            AnalyzeRevitLinks(doc, activeView, report, closedWorksets);
            AnalyzeCadLinks(doc, activeView, report, closedWorksets);

            report.ClosedWorksetsWithLinks = closedWorksets.OrderBy(n => n).ToList();
            report.Rows = report.Rows
                .OrderByDescending(r => r.HasProblem)
                .ThenBy(r => r.LinkName, StringComparer.OrdinalIgnoreCase)
                .ToList();
            return report;
        }

        private static void AnalyzeRevitLinks(Document doc, View activeView,
            LinkWorksetReport report, HashSet<string> closedWorksets)
        {
            ILookup<ElementId, Element> instancesByType = new FilteredElementCollector(doc)
                .OfClass(typeof(RevitLinkInstance))
                .ToLookup(i => i.GetTypeId(), i => i);

            foreach (RevitLinkType linkType in new FilteredElementCollector(doc)
                         .OfClass(typeof(RevitLinkType))
                         .Cast<RevitLinkType>())
            {
                bool nested = false;
                try { nested = linkType.IsNestedLink; } catch { }

                LinkedFileStatus status = LinkedFileStatus.Invalid;
                try { status = linkType.GetLinkedFileStatus(); } catch { }

                string path = GetLinkPath(doc, linkType);
                WorksetInfo typeWs = GetWorksetInfo(doc, linkType);
                List<Element> instances = instancesByType[linkType.Id].ToList();

                AddRows(report, doc, activeView, closedWorksets,
                    GetElementName(linkType), "RVT", nested, status, path, typeWs, instances);
            }
        }

        private static void AnalyzeCadLinks(Document doc, View activeView,
            LinkWorksetReport report, HashSet<string> closedWorksets)
        {
            ILookup<ElementId, Element> instancesByType = new FilteredElementCollector(doc)
                .OfClass(typeof(ImportInstance))
                .Cast<ImportInstance>()
                .Where(i => { try { return i.IsLinked; } catch { return false; } })
                .ToLookup(i => i.GetTypeId(), i => (Element)i);

            foreach (CADLinkType cadType in new FilteredElementCollector(doc)
                         .OfClass(typeof(CADLinkType))
                         .Cast<CADLinkType>())
            {
                // Los DWG "importados" (no vinculados) no son referencias externas: se omiten.
                bool isExternal = false;
                try { isExternal = ExternalFileUtils.IsExternalFileReference(doc, cadType.Id); } catch { }
                if (!isExternal) continue;

                LinkedFileStatus status = LinkedFileStatus.Invalid;
                try
                {
                    ExternalFileReference efr = ExternalFileUtils.GetExternalFileReference(doc, cadType.Id);
                    if (efr != null) status = efr.GetLinkedFileStatus();
                }
                catch { }

                string path = GetLinkPath(doc, cadType);
                WorksetInfo typeWs = GetWorksetInfo(doc, cadType);
                List<Element> instances = instancesByType[cadType.Id].ToList();

                AddRows(report, doc, activeView, closedWorksets,
                    GetElementName(cadType), "CAD", nested: false, status, path, typeWs, instances);
            }
        }

        private static void AddRows(LinkWorksetReport report, Document doc, View activeView,
            HashSet<string> closedWorksets, string linkName, string format, bool nested,
            LinkedFileStatus status, string path, WorksetInfo typeWs, List<Element> instances)
        {
            if (instances.Count == 0)
            {
                report.Rows.Add(BuildRow(doc, activeView, closedWorksets,
                    linkName, format, nested, status, path, typeWs, null));
                return;
            }

            foreach (Element instance in instances)
            {
                report.Rows.Add(BuildRow(doc, activeView, closedWorksets,
                    linkName, format, nested, status, path, typeWs, instance));
            }
        }

        private static LinkReportRow BuildRow(Document doc, View activeView,
            HashSet<string> closedWorksets, string linkName, string format, bool nested,
            LinkedFileStatus status, string path, WorksetInfo typeWs, Element instance)
        {
            WorksetInfo instWs = instance != null ? GetWorksetInfo(doc, instance) : new WorksetInfo();

            var row = new LinkReportRow
            {
                LinkName = linkName,
                Format = format,
                FilePath = path,
                LoadStatus = nested ? "Anidado (dentro de otro vínculo)" : StatusText(status),
                TypeWorkset = typeWs.Name,
                TypeWorksetState = StateText(typeWs.IsOpen),
                InstanceWorkset = instance == null ? "—" : instWs.Name,
                InstanceWorksetState = instance == null ? "—" : StateText(instWs.IsOpen),
            };

            // --- ¿Se ve en la vista activa? ---
            var hiddenReasons = new List<string>();
            string visible = "—";
            bool notLoaded = status == LinkedFileStatus.NotFound
                          || status == LinkedFileStatus.Unloaded
                          || status == LinkedFileStatus.LocallyUnloaded
                          || status == LinkedFileStatus.InClosedWorkset;

            if (!nested)
            {
                if (notLoaded)
                {
                    visible = "No";
                }
                else if (instance == null)
                {
                    visible = "—";
                }
                else if (instWs.IsOpen == false)
                {
                    visible = "No";
                }
                else if (IsGraphicalView(activeView))
                {
                    visible = CheckViewVisibility(doc, activeView, instance, instWs, hiddenReasons);
                }
            }
            row.VisibleInActiveView = visible;

            // --- Diagnóstico ---
            var diag = new StringBuilder();
            bool problem = false;

            if (nested)
            {
                diag.Append("Vínculo anidado: su carga y visibilidad se controlan desde el vínculo padre.");
            }
            else
            {
                switch (status)
                {
                    case LinkedFileStatus.NotFound:
                        problem = true;
                        diag.Append("Revit no encuentra el archivo (ruta rota o sin acceso a la red). " +
                                    "Corrige la ruta en Gestionar vínculos (pestaña Insertar o Gestionar).");
                        break;

                    case LinkedFileStatus.InClosedWorkset:
                        problem = true;
                        diag.Append("El vínculo NO se cargó porque su workset «" + typeWs.Name + "» está cerrado " +
                                    "(en Gestionar vínculos aparece como «En subproyecto cerrado»). " +
                                    "Abre SOLO ese workset —Colaborar ▸ Gestionar colaboración ▸ Subproyectos ▸ " +
                                    "seleccionarlo ▸ Abrir— y el vínculo se cargará sin necesidad de abrir todos.");
                        if (typeWs.Valid && typeWs.IsOpen == false) closedWorksets.Add(typeWs.Name);
                        break;

                    case LinkedFileStatus.Unloaded:
                        problem = true;
                        diag.Append("El vínculo está descargado. Recárgalo desde Gestionar vínculos " +
                                    "(botón Volver a cargar).");
                        break;

                    case LinkedFileStatus.LocallyUnloaded:
                        problem = true;
                        diag.Append("El vínculo está descargado solo en tu copia local. " +
                                    "Recárgalo desde Gestionar vínculos (botón Volver a cargar).");
                        break;
                }

                // El workset del tipo está cerrado aunque el estado de carga no lo delate.
                if (typeWs.IsOpen == false && status != LinkedFileStatus.InClosedWorkset)
                {
                    problem = true;
                    AppendSentence(diag, "Además, el workset del vínculo «" + typeWs.Name +
                        "» está CERRADO: ábrelo en Colaborar ▸ Gestionar colaboración ▸ Subproyectos.");
                    closedWorksets.Add(typeWs.Name);
                }

                if (instance == null)
                {
                    problem = true;
                    AppendSentence(diag, "No se encontró ninguna instancia colocada de este vínculo: " +
                        "el archivo sigue registrado en Gestionar vínculos, pero su instancia pudo " +
                        "haberse borrado del modelo. Si el vínculo está cargado, colócalo de nuevo " +
                        "(Gestionar vínculos no lo recoloca solo).");
                }
                else
                {
                    if (instWs.IsOpen == false)
                    {
                        problem = true;
                        AppendSentence(diag, "La instancia está en el workset «" + instWs.Name +
                            "», que está CERRADO: por eso no la ves. Ábrelo en Colaborar ▸ Gestionar " +
                            "colaboración ▸ Subproyectos (solo ese, no hace falta abrir todos).");
                        closedWorksets.Add(instWs.Name);
                    }

                    if (hiddenReasons.Count > 0)
                    {
                        problem = true;
                        AppendSentence(diag, "En la vista activa: " + string.Join(" ", hiddenReasons));
                    }
                }

                if (!problem)
                {
                    string checkedPart = visible == "Sí"
                        ? "Sin bloqueos detectados: cargado, worksets abiertos y visible en la vista activa. "
                        : "Sin bloqueos detectados: cargado y worksets abiertos. No se pudo comprobar la " +
                          "visibilidad porque no hay una vista gráfica activa. ";
                    diag.Append(checkedPart +
                                "Si no lo ves en alguna vista, revisa allí: V/G ▸ pestañas Vínculos de Revit y " +
                                "Subproyectos, filtros, plantilla de vista, rango de vista, fases, opciones de " +
                                "diseño y región de recorte.");
                }
            }

            row.Diagnosis = diag.ToString();
            row.HasProblem = problem;
            return row;
        }

        /// <summary>
        /// Revisa los motivos por los que una instancia cargada y en workset abierto
        /// podría no verse en la vista activa. Devuelve "Sí" o "No".
        /// </summary>
        private static string CheckViewVisibility(Document doc, View view, Element instance,
            WorksetInfo instWs, List<string> reasons)
        {
            bool hidden = false;

            // 0) Vínculo CAD colocado con "Solo vista actual": solo existe en su vista propietaria.
            try
            {
                if (instance.ViewSpecific && instance.OwnerViewId != view.Id)
                {
                    hidden = true;
                    var ownerView = doc.GetElement(instance.OwnerViewId) as View;
                    string ownerName = ownerView != null ? ownerView.Name : "otra vista";
                    reasons.Add("el vínculo se colocó con la opción «Solo vista actual» y únicamente existe en la vista «" +
                                ownerName + "».");
                }
            }
            catch { }

            // 1) Visibilidad del workset en esta vista (V/G ▸ pestaña Subproyectos).
            try
            {
                if (doc.IsWorkshared && instWs.Valid)
                {
                    WorksetVisibility wv = view.GetWorksetVisibility(instWs.Id);
                    if (wv == WorksetVisibility.Hidden)
                    {
                        hidden = true;
                        reasons.Add("el workset «" + instWs.Name +
                            "» está oculto en esta vista (V/G ▸ pestaña Subproyectos ▸ ponlo en Mostrar).");
                    }
                    else if (wv == WorksetVisibility.UseGlobalSetting)
                    {
                        Workset ws = doc.GetWorksetTable().GetWorkset(instWs.Id);
                        if (ws != null && !ws.IsVisibleByDefault)
                        {
                            hidden = true;
                            reasons.Add("el workset «" + instWs.Name +
                                "» tiene desactivada la casilla «Visible en todas las vistas» (diálogo Subproyectos).");
                        }
                    }
                }
            }
            catch { }

            // 2) Categoría oculta en la vista (V/G).
            try
            {
                Category cat = instance.Category;
                if (cat != null && view.GetCategoryHidden(cat.Id))
                {
                    hidden = true;
                    reasons.Add("la categoría «" + cat.Name + "» está oculta en esta vista (V/G).");
                }
            }
            catch { }

            // 3) Elemento oculto manualmente (Ocultar en vista ▸ Elementos).
            try
            {
                if (instance.IsHidden(view))
                {
                    hidden = true;
                    reasons.Add("la instancia está oculta manualmente en esta vista: usa la bombilla " +
                        "«Mostrar elementos ocultos» (atajo RH), selecciónala y elige Mostrar elemento.");
                }
            }
            catch { }

            // 4) Opción de diseño no principal.
            try
            {
                DesignOption opt = instance.DesignOption;
                if (opt != null && !opt.IsPrimary)
                {
                    hidden = true;
                    reasons.Add("la instancia pertenece a la opción de diseño «" + GetElementName(opt) +
                        "», que no es la principal.");
                }
            }
            catch { }

            return hidden ? "No" : "Sí";
        }

        /// <summary>
        /// Solo tiene sentido comprobar visibilidad en vistas gráficas de modelo:
        /// en tablas, planos, informes o leyendas los vínculos no se dibujan.
        /// </summary>
        private static bool IsGraphicalView(View view)
        {
            if (view == null) return false;
            switch (view.ViewType)
            {
                case ViewType.Undefined:
                case ViewType.Schedule:
                case ViewType.ColumnSchedule:
                case ViewType.PanelSchedule:
                case ViewType.CostReport:
                case ViewType.LoadsReport:
                case ViewType.PresureLossReport:
                case ViewType.DrawingSheet:
                case ViewType.Legend:
                case ViewType.Rendering:
                case ViewType.ProjectBrowser:
                case ViewType.SystemBrowser:
                case ViewType.Internal:
                    return false;
                default:
                    return true;
            }
        }

        private class WorksetInfo
        {
            public string Name = "—";
            public bool? IsOpen;      // null = no aplica (modelo sin worksets)
            public WorksetId Id;
            public bool Valid;
        }

        private static WorksetInfo GetWorksetInfo(Document doc, Element element)
        {
            var info = new WorksetInfo();
            try
            {
                if (!doc.IsWorkshared) return info;

                WorksetId id = element.WorksetId;
                if (id == null || id == WorksetId.InvalidWorksetId) return info;

                Workset ws = doc.GetWorksetTable().GetWorkset(id);
                if (ws == null) return info;

                info.Name = ws.Name;
                info.IsOpen = ws.IsOpen;
                info.Id = id;
                info.Valid = true;
            }
            catch { }
            return info;
        }

        private static string GetLinkPath(Document doc, Element linkType)
        {
            // Referencia externa clásica (rutas locales o de servidor).
            try
            {
                ExternalFileReference efr = ExternalFileUtils.GetExternalFileReference(doc, linkType.Id);
                if (efr != null)
                {
                    ModelPath mp = efr.GetAbsolutePath();
                    if (mp != null)
                    {
                        string p = ModelPathUtils.ConvertModelPathToUserVisiblePath(mp);
                        if (!string.IsNullOrWhiteSpace(p)) return p;
                    }
                }
            }
            catch { }

            // Vínculos de nube (BIM 360 / ACC) u otros recursos externos.
            try
            {
                IDictionary<ExternalResourceType, ExternalResourceReference> refs =
                    linkType.GetExternalResourceReferences();
                if (refs != null)
                {
                    foreach (KeyValuePair<ExternalResourceType, ExternalResourceReference> kv in refs)
                    {
                        if (kv.Value == null) continue;
                        string p = kv.Value.InSessionPath;
                        if (!string.IsNullOrWhiteSpace(p)) return p;
                    }
                }
            }
            catch { }

            return "(ruta no disponible)";
        }

        private static string GetElementName(Element e)
        {
            try { return e.Name; } catch { return "(sin nombre)"; }
        }

        private static string StatusText(LinkedFileStatus status)
        {
            switch (status)
            {
                case LinkedFileStatus.Loaded: return "Cargado";
                case LinkedFileStatus.Unloaded: return "Descargado";
                case LinkedFileStatus.LocallyUnloaded: return "Descargado (solo local)";
                case LinkedFileStatus.NotFound: return "No encontrado";
                case LinkedFileStatus.InClosedWorkset: return "En workset cerrado";
                default: return "No determinado";
            }
        }

        private static string StateText(bool? isOpen)
        {
            if (isOpen == null) return "—";
            return isOpen.Value ? "abierto" : "CERRADO";
        }

        private static void AppendSentence(StringBuilder sb, string sentence)
        {
            if (sb.Length > 0) sb.Append("  ");
            sb.Append(sentence);
        }
    }
}
