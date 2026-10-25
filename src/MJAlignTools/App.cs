using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using MJAlignTools.UI;

namespace MJAlignTools
{
    /// <summary>
    /// Añade el panel "Alinear" con el botón "Alinear objetos" a la pestaña de
    /// ribbon "MJ Tools" (la comparte con las demás herramientas MJ).
    /// </summary>
    public class App : IExternalApplication
    {
        private const string TabName = "MJ Tools";
        private const string PanelName = "Alinear";

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
                    "MJAlignTools_ShowPanel",
                    "Alinear\nobjetos",
                    typeof(App).Assembly.Location,
                    "MJAlignTools.Commands.ShowAlignPanelCommand")
                {
                    ToolTip = "Panel de alineación y distribución, como el de Illustrator.",
                    LongDescription =
                        "Abre un panel flotante para alinear, distribuir y espaciar los elementos seleccionados " +
                        "(mobiliario, equipos, genéricos, textos, vistas en planos…) respecto a la selección, " +
                        "a un objeto clave o a la habitación en la que está cada uno. " +
                        "Trabaja en el plano de la vista activa: en planta, alzado o sección.",
                    AvailabilityClassName = "MJAlignTools.CommandAvailability",
                };

                var button = panel.AddItem(buttonData) as PushButton;
                if (button != null)
                {
                    try
                    {
                        button.LargeImage = AlignIcons.Ribbon(32);
                        button.Image = AlignIcons.Ribbon(16);
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
            AlignPanelHost.Shutdown();
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
