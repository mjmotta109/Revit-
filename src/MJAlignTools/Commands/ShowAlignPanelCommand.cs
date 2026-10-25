using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using MJAlignTools.UI;

namespace MJAlignTools.Commands
{
    /// <summary>
    /// Abre el panel flotante de alineación. El comando en sí no modifica el modelo:
    /// cada botón del panel hace su propia transacción.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class ShowAlignPanelCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                AlignPanelHost.Show(commandData.Application);
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.ToString();
                return Result.Failed;
            }
        }
    }
}
