using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace MJAlignTools.Revit
{
    /// <summary>
    /// El panel es una ventana no modal (sigue abierta mientras trabajas, como en
    /// Illustrator), así que no puede llamar a la API de Revit directamente: deja la
    /// petición aquí y levanta un <see cref="ExternalEvent"/>; Revit llama a
    /// <see cref="Execute"/> en cuanto está libre.
    /// </summary>
    internal sealed class AlignRequestHandler : IExternalEventHandler
    {
        private AlignRequest _pendingAlign;
        private bool _pendingPickKey;

        /// <summary>Se dispara en el hilo de Revit (el mismo de la ventana) al terminar cada petición.</summary>
        public event Action<AlignOutcome> Completed;

        public KeyObjectRef Key { get; private set; }

        public void RequestAlign(AlignRequest request)
        {
            _pendingAlign = request;
            _pendingPickKey = false;
        }

        public void RequestPickKey()
        {
            _pendingAlign = null;
            _pendingPickKey = true;
        }

        public void Execute(UIApplication app)
        {
            AlignOutcome outcome;
            try
            {
                if (_pendingPickKey) outcome = PickKey(app);
                else if (_pendingAlign != null) outcome = AlignExecutor.Run(app, _pendingAlign, Key);
                else outcome = AlignOutcome.Info("");
            }
            catch (Exception ex)
            {
                outcome = AlignOutcome.Error("Error inesperado: " + ex.Message);
            }
            finally
            {
                _pendingAlign = null;
                _pendingPickKey = false;
            }

            Completed?.Invoke(outcome);
        }

        public string GetName() => "MJ Align Tools";

        private AlignOutcome PickKey(UIApplication app)
        {
            UIDocument uidoc = app.ActiveUIDocument;
            if (uidoc == null) return AlignOutcome.Error("No hay ningún documento abierto.");
            Document doc = uidoc.Document;

            // Elegir la clave no debe perder la selección que se va a alinear.
            ICollection<ElementId> previous = uidoc.Selection.GetElementIds();
            try
            {
                Reference picked = uidoc.Selection.PickObject(ObjectType.Element,
                    "Haz clic en el objeto clave: los demás se alinearán a él y él no se moverá (Esc para cancelar).");
                Element element = picked != null ? doc.GetElement(picked.ElementId) : null;
                if (element == null) return AlignOutcome.Error("No se pudo leer el elemento elegido.");

                Key = new KeyObjectRef(doc, element);
                return AlignOutcome.Success("Objeto clave: " + Key.Label);
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return AlignOutcome.Info("Elección del objeto clave cancelada.");
            }
            finally
            {
                try { uidoc.Selection.SetElementIds(previous); } catch { }
            }
        }
    }
}
