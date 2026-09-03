using System;
using Autodesk.Revit.UI;

namespace QuickDimension
{
    /// <summary>
    /// Añade el botón "Cota rápida" al panel "Acotar" de la pestaña "MJ Tools".
    /// La pestaña se comparte con las demás herramientas de la suite.
    /// </summary>
    public class App : IExternalApplication
    {
        private const string TabName = "MJ Tools";
        private const string PanelName = "Acotar";

        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                try { application.CreateRibbonTab(TabName); }
                catch (Autodesk.Revit.Exceptions.ArgumentException) { /* otra herramienta ya la creó */ }

                RibbonPanel panel = null;
                foreach (RibbonPanel existing in application.GetRibbonPanels(TabName))
                {
                    if (existing.Name == PanelName) { panel = existing; break; }
                }
                if (panel == null) panel = application.CreateRibbonPanel(TabName, PanelName);

                var buttonData = new PushButtonData(
                    "QuickDimension_Draw",
                    "Cota\nrápida",
                    typeof(App).Assembly.Location,
                    "QuickDimension.Commands.QuickDimensionCommand")
                {
                    ToolTip = "Traza una línea y crea la cota con los elementos arquitectónicos que encuentra.",
                    LongDescription =
                        "Traza una línea sobre la vista y la herramienta acota lo que hay en esa dirección. " +
                        "Puedes quedarte solo con lo que la línea atraviesa (caras de muros y columnas) o " +
                        "incluir además las aberturas proyectadas: jambas y ejes de puertas, ventanas y vanos " +
                        "de muro. El mobiliario y el equipamiento nunca entran en la cota.",
                    AvailabilityClassName = "QuickDimension.Commands.QuickDimensionAvailability",
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
}
