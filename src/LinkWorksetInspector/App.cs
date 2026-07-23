using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace LinkWorksetInspector
{
    /// <summary>
    /// Crea la pestaña de ribbon "MJ Tools" con el botón "¿Dónde está el vínculo?".
    /// </summary>
    public class App : IExternalApplication
    {
        private const string TabName = "MJ Tools";
        private const string PanelName = "Vínculos";

        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                try { application.CreateRibbonTab(TabName); }
                catch (Autodesk.Revit.Exceptions.ArgumentException) { /* la pestaña ya existe */ }

                RibbonPanel panel = null;
                foreach (RibbonPanel p in application.GetRibbonPanels(TabName))
                {
                    if (p.Name == PanelName) { panel = p; break; }
                }
                if (panel == null) panel = application.CreateRibbonPanel(TabName, PanelName);

                var buttonData = new PushButtonData(
                    "LinkWorksetInspector_Inspect",
                    "¿Dónde está\nel vínculo?",
                    typeof(App).Assembly.Location,
                    "LinkWorksetInspector.Commands.InspectLinkWorksetsCommand")
                {
                    ToolTip = "Muestra en qué workset (subproyecto) está cada vínculo (RVT y CAD), aunque no esté cargado.",
                    LongDescription =
                        "Lista todos los vínculos del modelo con su workset (subproyecto), el estado de ese " +
                        "workset (abierto o cerrado), el estado de carga del vínculo y un diagnóstico de por qué " +
                        "podría no estar visible. Así puedes abrir SOLO el workset necesario en lugar de " +
                        "abrirlos todos y sobrecargar el proyecto.",
                    AvailabilityClassName = "LinkWorksetInspector.CommandAvailability",
                };

                var button = panel.AddItem(buttonData) as PushButton;
                if (button != null)
                {
                    try
                    {
                        button.LargeImage = RibbonIconFactory.Create(32);
                        button.Image = RibbonIconFactory.Create(16);
                    }
                    catch { /* sin icono el botón funciona igual */ }
                }

                var aiButtonData = new PushButtonData(
                    "LinkWorksetInspector_AiAssistant",
                    "Asistente\nIA",
                    typeof(App).Assembly.Location,
                    "LinkWorksetInspector.Commands.AiAssistantCommand")
                {
                    ToolTip = "Chatea con una IA que consulta el modelo abierto (Claude, API de Anthropic).",
                    LongDescription =
                        "Abre un chat donde puedes preguntar en lenguaje natural sobre el modelo: " +
                        "cuántos elementos hay por nivel, qué vínculos están descargados y en qué " +
                        "workset, qué advertencias tiene el proyecto, parámetros de un elemento, etc. " +
                        "La IA consulta los datos reales mediante herramientas de solo lectura. " +
                        "Requiere una API key de Anthropic.",
                    AvailabilityClassName = "LinkWorksetInspector.CommandAvailability",
                };

                var aiButton = panel.AddItem(aiButtonData) as PushButton;
                if (aiButton != null)
                {
                    try
                    {
                        aiButton.LargeImage = RibbonIconFactory.CreateSpark(32);
                        aiButton.Image = RibbonIconFactory.CreateSpark(16);
                    }
                    catch { /* sin icono el botón funciona igual */ }
                }

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
