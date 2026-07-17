using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LinkWorksetInspector.Services;
using LinkWorksetInspector.UI;

namespace LinkWorksetInspector.Commands
{
    /// <summary>
    /// Comando principal: analiza todos los vínculos del modelo y muestra la
    /// tabla de worksets y diagnósticos. No modifica el modelo (sin transacciones).
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class InspectLinkWorksetsCommand : IExternalCommand
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

                Document doc = uidoc.Document;

                View activeView = null;
                try { activeView = uidoc.ActiveGraphicalView; } catch { }
                if (activeView == null) activeView = doc.ActiveView;

                LinkWorksetReport report = LinkWorksetAnalyzer.Analyze(doc, activeView);

                if (report.Rows.Count == 0)
                {
                    TaskDialog.Show("Vínculos y worksets",
                        "Este modelo no contiene vínculos RVT ni vínculos CAD.");
                    return Result.Succeeded;
                }

                using (var form = new LinkWorksetForm(report, doc.Title, activeView != null ? activeView.Name : null))
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
    }
}
