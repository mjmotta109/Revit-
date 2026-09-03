using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using QuickDimension.Model;
using QuickDimension.Services;
using QuickDimension.UI;

namespace QuickDimension.Commands
{
    /// <summary>
    /// Comando principal: el usuario traza una línea y la herramienta crea la cota
    /// con los elementos arquitectónicos que encuentra en esa dirección.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class QuickDimensionCommand : IExternalCommand
    {
        private const ObjectSnapTypes Snaps =
            ObjectSnapTypes.Endpoints | ObjectSnapTypes.Midpoints | ObjectSnapTypes.Intersections |
            ObjectSnapTypes.Centers | ObjectSnapTypes.Perpendicular | ObjectSnapTypes.Nearest |
            ObjectSnapTypes.WorkPlaneGrid;

        /// <summary>Las opciones se recuerdan mientras Revit siga abierto.</summary>
        private static QuickDimensionOptions _lastOptions;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIDocument uidoc = commandData.Application.ActiveUIDocument;
                if (uidoc == null || uidoc.Document == null)
                {
                    message = "No hay ningún documento abierto.";
                    return Result.Cancelled;
                }

                Document doc = uidoc.Document;

                View view = null;
                try { view = uidoc.ActiveGraphicalView; }
                catch { }
                if (view == null) view = doc.ActiveView;

                if (!QuickDimensionEngine.IsSupportedView(view))
                {
                    TaskDialog.Show("Cota rápida",
                        "Esta herramienta funciona en vistas de planta, sección, alzado y detalle.\n\n" +
                        "Cambia a una de ellas y vuelve a intentarlo.");
                    return Result.Cancelled;
                }

                QuickDimensionOptions options;
                using (var form = new QuickDimensionOptionsForm(_lastOptions))
                {
                    IntPtr owner = IntPtr.Zero;
                    try { owner = commandData.Application.MainWindowHandle; }
                    catch { }

                    System.Windows.Forms.DialogResult answer = owner != IntPtr.Zero
                        ? form.ShowDialog(new RevitOwnerWindow(owner))
                        : form.ShowDialog();

                    if (answer != System.Windows.Forms.DialogResult.OK) return Result.Cancelled;
                    options = form.Options;
                }

                _lastOptions = options.Clone();

                int created = DrawLoop(uidoc, doc, view, options);

                if (created == 0) return Result.Cancelled;
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.ToString();
                return Result.Failed;
            }
        }

        /// <summary>Va pidiendo líneas hasta que el usuario pulsa ESC.</summary>
        private static int DrawLoop(UIDocument uidoc, Document doc, View view, QuickDimensionOptions options)
        {
            int created = 0;

            while (true)
            {
                XYZ start;
                XYZ end;

                try
                {
                    start = uidoc.Selection.PickPoint(Snaps,
                        "Cota rápida: primer punto de la línea (ESC para terminar)");
                    end = uidoc.Selection.PickPoint(Snaps,
                        "Cota rápida: segundo punto de la línea (ESC para terminar)");
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    return created;
                }
                catch (Autodesk.Revit.Exceptions.InvalidOperationException ex)
                {
                    TaskDialog.Show("Cota rápida", "No se puede picar puntos en esta vista.\n\n" + ex.Message);
                    return created;
                }

                DimensionPlan plan = QuickDimensionEngine.Build(doc, view, start, end, options);

                if (!plan.IsValid)
                {
                    if (!Retry("Cota rápida", plan.FailureReason ?? "No se encontraron referencias para acotar."))
                    {
                        return created;
                    }

                    continue;
                }

                string error;
                if (TryCreate(doc, view, plan, out error))
                {
                    created++;
                    continue;
                }

                if (!Retry("Cota rápida", "Revit no aceptó las referencias encontradas.\n\n" + error))
                {
                    return created;
                }
            }
        }

        /// <summary>
        /// Intenta crear la cota con todas las referencias y, si Revit la rechaza, repite
        /// quedándose solo con las más fiables antes de darse por vencido.
        /// </summary>
        private static bool TryCreate(Document doc, View view, DimensionPlan plan, out string error)
        {
            error = null;

            var ladder = new[] { ReferenceTier.Plane, ReferenceTier.Geometry, ReferenceTier.Solid };

            foreach (ReferenceTier tier in ladder)
            {
                Line line;
                ReferenceArray references;
                int count;

                if (!plan.TryBuild(tier, out line, out references, out count)) continue;

                using (var transaction = new Transaction(doc, "Cota rápida"))
                {
                    transaction.Start();

                    try
                    {
                        Dimension dimension = doc.Create.NewDimension(view, line, references);
                        if (dimension == null) throw new InvalidOperationException("Revit no devolvió ninguna cota.");

                        transaction.Commit();
                        return true;
                    }
                    catch (Exception ex)
                    {
                        error = ex.Message;
                        if (transaction.HasStarted()) transaction.RollBack();
                    }
                }
            }

            if (error == null) error = "No quedaron suficientes referencias válidas.";
            return false;
        }

        private static bool Retry(string title, string body)
        {
            var dialog = new TaskDialog(title)
            {
                MainInstruction = "No se pudo crear la cota",
                MainContent = body,
                CommonButtons = TaskDialogCommonButtons.Retry | TaskDialogCommonButtons.Close,
                DefaultButton = TaskDialogResult.Retry,
            };

            return dialog.Show() == TaskDialogResult.Retry;
        }
    }

    /// <summary>El botón solo se habilita cuando hay un documento abierto.</summary>
    public class QuickDimensionAvailability : IExternalCommandAvailability
    {
        public bool IsCommandAvailable(UIApplication applicationData, CategorySet selectedCategories)
        {
            return applicationData != null
                && applicationData.ActiveUIDocument != null
                && applicationData.ActiveUIDocument.Document != null;
        }
    }
}
