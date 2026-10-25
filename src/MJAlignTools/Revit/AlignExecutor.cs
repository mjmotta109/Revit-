using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using MJAlignTools.Core;

namespace MJAlignTools.Revit
{
    /// <summary>Lo que pide el panel al pulsar un botón.</summary>
    internal sealed class AlignRequest
    {
        public AlignCommand Command { get; set; }
        public ReferenceMode Mode { get; set; }

        /// <summary>Texto del campo de espaciado ("Auto", vacío o una distancia).</summary>
        public string SpacingText { get; set; }
    }

    internal enum OutcomeKind
    {
        Success,
        Info,
        Error,
    }

    internal sealed class AlignOutcome
    {
        public OutcomeKind Kind { get; private set; }
        public string Message { get; private set; }

        public static AlignOutcome Success(string message) => new AlignOutcome { Kind = OutcomeKind.Success, Message = message };
        public static AlignOutcome Info(string message) => new AlignOutcome { Kind = OutcomeKind.Info, Message = message };
        public static AlignOutcome Error(string message) => new AlignOutcome { Kind = OutcomeKind.Error, Message = message };
    }

    /// <summary>Objeto clave recordado entre clics (por documento).</summary>
    internal sealed class KeyObjectRef
    {
        public KeyObjectRef(Document doc, Element element)
        {
            DocumentKey = KeyOf(doc);
            Id = element.Id;
            Label = Describe(element);
        }

        public string DocumentKey { get; }
        public ElementId Id { get; }
        public string Label { get; }

        /// <summary>Id del objeto clave si sigue existiendo en este documento; si no, null.</summary>
        public ElementId ResolveIn(Document doc)
        {
            if (KeyOf(doc) != DocumentKey) return null;
            return doc.GetElement(Id) != null ? Id : null;
        }

        private static string KeyOf(Document doc) => string.IsNullOrEmpty(doc.PathName) ? doc.Title : doc.PathName;

        public static string Describe(Element element)
        {
            string category = null;
            try { category = element.Category?.Name; } catch { }
            string name = null;
            try
            {
                name = element is FamilyInstance fi && fi.Symbol != null
                    ? fi.Symbol.FamilyName + ": " + fi.Name
                    : element.Name;
            }
            catch { }

            string label = string.IsNullOrEmpty(category) ? name : category + " — " + name;
            return (string.IsNullOrEmpty(label) ? "Elemento" : label) + "  [" + element.Id + "]";
        }
    }

    /// <summary>
    /// Ejecuta una operación del panel sobre la selección actual, en una sola
    /// transacción (un solo Ctrl+Z la deshace).
    /// </summary>
    internal static class AlignExecutor
    {
        private sealed class Item
        {
            public Item(Element element, Box2 box)
            {
                Element = element;
                Box = box;
            }

            public Element Element { get; }
            public Box2 Box { get; }
            public bool IsKey { get; set; }
        }

        private sealed class LayoutGroup
        {
            public List<Item> Items { get; } = new List<Item>();
            public Box2? Container { get; set; }
        }

        public static AlignOutcome Run(UIApplication app, AlignRequest request, KeyObjectRef key)
        {
            UIDocument uidoc = app.ActiveUIDocument;
            if (uidoc == null) return AlignOutcome.Error("No hay ningún documento abierto.");
            Document doc = uidoc.Document;
            if (doc.IsReadOnly) return AlignOutcome.Error("El documento es de solo lectura.");

            View view = null;
            try { view = uidoc.ActiveGraphicalView; } catch { }
            if (view == null) view = doc.ActiveView;
            if (view == null) return AlignOutcome.Error("Activa una vista gráfica (planta, alzado, sección, plano…).");

            ViewFrame frame = ViewFrame.For(view);
            ICollection<ElementId> selectedIds = uidoc.Selection.GetElementIds();
            if (selectedIds.Count == 0)
                return AlignOutcome.Info("Selecciona en la vista los elementos que quieres alinear y vuelve a pulsar el botón.");

            AlignCommand command = request.Command;
            ReferenceMode mode = request.Mode;

            // Espaciado fijo (solo para "Distribuir espaciado").
            double? spacing = null;
            if (AlignCommands.KindOf(command) == LayoutKind.Spacing && !IsAuto(request.SpacingText))
            {
                if (!TryParseLength(doc, request.SpacingText, out double value))
                {
                    return AlignOutcome.Error("No entiendo el espaciado \"" + request.SpacingText.Trim() +
                        "\". Escribe una distancia en las unidades del proyecto (p. ej. 600 mm, 0.6 m, 2' 0\") o \"Auto\".");
                }
                spacing = value;
            }

            ElementId keyId = null;
            if (mode == ReferenceMode.KeyObject)
            {
                keyId = key?.ResolveIn(doc);
                if (keyId == null)
                    return AlignOutcome.Info("Elige primero el objeto clave: pulsa «Elegir…» y haz clic en él en la vista.");
            }

            // Elementos de la selección que se pueden mover.
            var items = new List<Item>();
            int pinned = 0, grouped = 0, nested = 0, unmeasured = 0;
            foreach (ElementId id in selectedIds)
            {
                if (keyId != null && id.Equals(keyId)) continue; // la clave se añade aparte

                Element element = doc.GetElement(id);
                if (element == null) continue;
                if (element.Pinned) { pinned++; continue; }
                if (element.GroupId != null && element.GroupId != ElementId.InvalidElementId) { grouped++; continue; }
                if (element is FamilyInstance fi && fi.SuperComponent != null) { nested++; continue; }
                if (!ElementExtents.TryGet(element, frame, out Box2 box)) { unmeasured++; continue; }
                items.Add(new Item(element, box));
            }

            if (keyId != null)
            {
                Element keyElement = doc.GetElement(keyId);
                if (!ElementExtents.TryGet(keyElement, frame, out Box2 keyBox))
                    return AlignOutcome.Error("No se pudo medir el objeto clave en esta vista. Elige otro.");
                items.Add(new Item(keyElement, keyBox) { IsKey = true });
            }

            // Grupos de trabajo: uno solo, o uno por habitación.
            var groups = new List<LayoutGroup>();
            int withoutRoom = 0;
            if (mode == ReferenceMode.Room)
            {
                var locator = new RoomLocator(doc, frame);
                var byRoom = new Dictionary<string, LayoutGroup>();
                foreach (Item item in items)
                {
                    RoomInfo room = locator.Find(item.Element);
                    if (room == null) { withoutRoom++; continue; }
                    if (!byRoom.TryGetValue(room.Key, out LayoutGroup group))
                    {
                        group = new LayoutGroup { Container = room.Box };
                        byRoom.Add(room.Key, group);
                    }
                    group.Items.Add(item);
                }
                groups.AddRange(byRoom.Values);
            }
            else
            {
                var group = new LayoutGroup();
                group.Items.AddRange(items);
                groups.Add(group);
            }

            int minimum = AlignEngine.MinimumItems(command, mode, spacing.HasValue);
            var moves = new List<KeyValuePair<Item, Offset2>>();
            int processedGroups = 0, alreadyInPlace = 0;
            foreach (LayoutGroup group in groups)
            {
                if (group.Items.Count < minimum) continue;
                processedGroups++;

                List<Box2> boxes = group.Items.Select(i => i.Box).ToList();
                int keyIndex = group.Items.FindIndex(i => i.IsKey);
                Offset2[] offsets = AlignEngine.Compute(command, boxes, mode, keyIndex, group.Container, spacing);

                for (int i = 0; i < offsets.Length; i++)
                {
                    if (group.Items[i].IsKey) continue;
                    if (offsets[i].IsNegligible) alreadyInPlace++;
                    else moves.Add(new KeyValuePair<Item, Offset2>(group.Items[i], offsets[i]));
                }
            }

            string notes = SkippedNotes(pinned, grouped, nested, unmeasured, withoutRoom);

            if (processedGroups == 0)
            {
                string reason;
                if (mode == ReferenceMode.Room)
                    reason = "Ninguno de los elementos seleccionados está dentro de una habitación cerrada (ni del modelo ni de un vínculo cargado).";
                else if (mode == ReferenceMode.KeyObject)
                    reason = "Esta operación necesita al menos " + (minimum - 1) + " elemento(s) seleccionados además del objeto clave.";
                else
                    reason = "Esta operación necesita al menos " + minimum + " elementos seleccionados que se puedan mover.";
                return AlignOutcome.Info(reason + notes);
            }

            if (moves.Count == 0)
                return AlignOutcome.Info("Nada que mover: los elementos ya estaban en su sitio." + notes);

            int moved = 0, failed = 0;
            string firstError = null;
            string title = AlignCommands.Describe(command);
            using (var transaction = new Transaction(doc, title))
            {
                transaction.Start();
                foreach (KeyValuePair<Item, Offset2> move in moves)
                {
                    try
                    {
                        MoveElement(doc, move.Key.Element, frame.ToModel(move.Value));
                        moved++;
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        if (firstError == null) firstError = ex.Message;
                    }
                }

                if (moved == 0)
                {
                    transaction.RollBack();
                    return AlignOutcome.Error("Revit no permitió mover ningún elemento: " + firstError + notes);
                }

                if (transaction.Commit() != TransactionStatus.Committed)
                    return AlignOutcome.Error("Revit canceló la operación (revisa los avisos).");
            }

            string message = title + ": " + moved + " elemento(s) movido(s)";
            if (mode == ReferenceMode.Room) message += " en " + processedGroups + " habitación(es)";
            if (spacing.HasValue) message += ", separación " + FormatLength(doc, spacing.Value);
            message += ".";
            if (alreadyInPlace > 0) message += " " + alreadyInPlace + " ya estaba(n) en su sitio.";
            if (failed > 0) message += " " + failed + " no se pudieron mover (" + firstError + ").";
            if (frame.UsesPlanAxes && view is View3D) message += " En vistas 3D se alinea en planta (ejes X/Y).";
            message += notes;

            return failed > 0 || notes.Length > 0 ? AlignOutcome.Info(message) : AlignOutcome.Success(message);
        }

        private static void MoveElement(Document doc, Element element, XYZ vector)
        {
            if (element is Viewport viewport)
            {
                viewport.SetBoxCenter(viewport.GetBoxCenter().Add(vector));
                return;
            }
            ElementTransformUtils.MoveElement(doc, element.Id, vector);
        }

        private static string SkippedNotes(int pinned, int grouped, int nested, int unmeasured, int withoutRoom)
        {
            var parts = new List<string>();
            if (pinned > 0) parts.Add(pinned + " fijado(s) (pin)");
            if (grouped > 0) parts.Add(grouped + " dentro de un grupo (selecciona el grupo entero)");
            if (nested > 0) parts.Add(nested + " anidado(s) en otra familia");
            if (unmeasured > 0) parts.Add(unmeasured + " sin geometría medible en esta vista");
            if (withoutRoom > 0) parts.Add(withoutRoom + " fuera de cualquier habitación");
            return parts.Count == 0 ? "" : " Omitidos: " + string.Join("; ", parts) + ".";
        }

        private static bool IsAuto(string text)
        {
            return string.IsNullOrWhiteSpace(text) || string.Equals(text.Trim(), "auto", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryParseLength(Document doc, string text, out double value)
        {
            return UnitFormatUtils.TryParse(doc.GetUnits(), SpecTypeId.Length, text.Trim(), out value);
        }

        private static string FormatLength(Document doc, double value)
        {
            try { return UnitFormatUtils.Format(doc.GetUnits(), SpecTypeId.Length, value, false); }
            catch { return value.ToString("0.###") + " ft"; }
        }
    }
}
