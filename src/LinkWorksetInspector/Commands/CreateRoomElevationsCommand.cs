using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using LinkWorksetInspector.Services.RoomElevations;
using LinkWorksetInspector.UI;

namespace LinkWorksetInspector.Commands
{
    /// <summary>
    /// Crea alzados interiores para cada habitación. La ventana de opciones es modal,
    /// así que la generación (con sus transacciones) ocurre dentro del contexto de la
    /// API de este comando.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CreateRoomElevationsCommand : IExternalCommand
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

                Func<RoomElevationOptions, Action<string>, IList<RoomElevationResult>> run =
                    (options, progress) =>
                    {
                        IList<Room> rooms = CollectRooms(uidoc, options.Scope);
                        if (rooms.Count == 0) return new List<RoomElevationResult>();
                        return new RoomElevationGenerator(doc, options).Run(rooms, progress);
                    };

                using (var form = new RoomElevationsForm(doc, run))
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

        private static IList<Room> CollectRooms(UIDocument uidoc, RoomScope scope)
        {
            Document doc = uidoc.Document;

            IEnumerable<Element> source;
            switch (scope)
            {
                case RoomScope.CurrentSelection:
                    source = uidoc.Selection.GetElementIds()
                        .Select(id => doc.GetElement(id))
                        .Where(e => e != null);
                    break;

                case RoomScope.ActiveView:
                    View active = doc.ActiveView;
                    source = active != null && !active.IsTemplate
                        ? new FilteredElementCollector(doc, active.Id)
                            .OfCategory(BuiltInCategory.OST_Rooms)
                            .WhereElementIsNotElementType()
                            .ToElements()
                        : new List<Element>();
                    break;

                default:
                    source = new FilteredElementCollector(doc)
                        .OfCategory(BuiltInCategory.OST_Rooms)
                        .WhereElementIsNotElementType()
                        .ToElements();
                    break;
            }

            return source.OfType<Room>()
                .Where(r => r.Area > 0)
                .OrderBy(r => RoomSortKey(r), StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string RoomSortKey(Room room)
        {
            try
            {
                Parameter number = room.get_Parameter(BuiltInParameter.ROOM_NUMBER);
                string value = number != null ? number.AsString() : null;
                return string.IsNullOrEmpty(value) ? "zzz" + room.Id : value.PadLeft(8, '0');
            }
            catch { return "zzz"; }
        }
    }
}
