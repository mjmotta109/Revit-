using System;
using System.Windows.Media;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace LinkWorksetInspector
{
    /// <summary>Crea la pestaña de ribbon "MJ Tools" con las herramientas del add-in.</summary>
    public class App : IExternalApplication
    {
        private const string TabName = "MJ Tools";
        private const string LinksPanel = "Vínculos";
        private const string ViewsPanel = "Vistas";

        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                try { application.CreateRibbonTab(TabName); }
                catch (Autodesk.Revit.Exceptions.ArgumentException) { /* la pestaña ya existe */ }

                RibbonPanel links = GetOrCreatePanel(application, LinksPanel);
                RibbonPanel views = GetOrCreatePanel(application, ViewsPanel);

                AddButton(links,
                    "LinkWorksetInspector_Inspect",
                    "¿Dónde está\nel vínculo?",
                    "LinkWorksetInspector.Commands.InspectLinkWorksetsCommand",
                    "Muestra en qué workset (subproyecto) está cada vínculo (RVT y CAD), aunque no esté cargado.",
                    "Lista todos los vínculos del modelo con su workset (subproyecto), el estado de ese " +
                    "workset (abierto o cerrado), el estado de carga del vínculo y un diagnóstico de por qué " +
                    "podría no estar visible. Así puedes abrir SOLO el workset necesario en lugar de " +
                    "abrirlos todos y sobrecargar el proyecto.",
                    RibbonIconFactory.CreateLink);

                AddButton(links,
                    "LinkWorksetInspector_AiAssistant",
                    "Asistente\nIA",
                    "LinkWorksetInspector.Commands.AiAssistantCommand",
                    "Chatea con una IA que consulta el modelo abierto (Claude, API de Anthropic).",
                    "Abre un chat donde puedes preguntar en lenguaje natural sobre el modelo: " +
                    "cuántos elementos hay por nivel, qué vínculos están descargados y en qué " +
                    "workset, qué advertencias tiene el proyecto, parámetros de un elemento, etc. " +
                    "La IA consulta los datos reales mediante herramientas de solo lectura. " +
                    "Requiere una API key de Anthropic.",
                    RibbonIconFactory.CreateSpark);

                AddButton(views,
                    "LinkWorksetInspector_RoomElevations",
                    "Alzados por\nhabitación",
                    "LinkWorksetInspector.Commands.CreateRoomElevationsCommand",
                    "Crea los alzados interiores de cada habitación, recortados al piso, los muros y el cielo raso.",
                    "Genera un alzado por cada orientación de muro de cada habitación. Cada vista se " +
                    "recorta al ancho de la estancia y, en vertical, entre el piso y el cielo raso o la " +
                    "losa más cercana por encima; el corte lejano se ajusta a los muros de esa misma " +
                    "habitación para que no aparezcan las estancias contiguas.",
                    RibbonIconFactory.CreateRoomElevation);

                return Result.Succeeded;
            }
            catch (Exception)
            {
                return Result.Failed;
            }
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }

        private static RibbonPanel GetOrCreatePanel(UIControlledApplication application, string name)
        {
            foreach (RibbonPanel panel in application.GetRibbonPanels(TabName))
            {
                if (panel.Name == name) return panel;
            }
            return application.CreateRibbonPanel(TabName, name);
        }

        private static void AddButton(RibbonPanel panel, string id, string text, string className,
            string tooltip, string longDescription, Func<int, ImageSource> icon)
        {
            var data = new PushButtonData(id, text, typeof(App).Assembly.Location, className)
            {
                ToolTip = tooltip,
                LongDescription = longDescription,
                AvailabilityClassName = "LinkWorksetInspector.CommandAvailability",
            };

            var button = panel.AddItem(data) as PushButton;
            if (button == null) return;

            try
            {
                button.LargeImage = icon(32);
                button.Image = icon(16);
            }
            catch { /* sin icono el botón funciona igual */ }
        }
    }

    /// <summary>El botón solo se habilita cuando hay un documento abierto.</summary>
    public class CommandAvailability : IExternalCommandAvailability
    {
        public bool IsCommandAvailable(UIApplication applicationData, CategorySet selectedCategories)
        {
            return applicationData != null
                && applicationData.ActiveUIDocument != null
                && applicationData.ActiveUIDocument.Document != null;
        }
    }
}
