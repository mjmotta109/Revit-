using System;
using System.Windows;
using System.Windows.Interop;
using Autodesk.Revit.UI;
using MJAlignTools.Revit;

namespace MJAlignTools.UI
{
    /// <summary>Mantiene una única instancia del panel durante la sesión de Revit.</summary>
    internal static class AlignPanelHost
    {
        private static AlignPanelWindow _window;
        private static AlignRequestHandler _handler;
        private static ExternalEvent _event;

        /// <summary>Abre el panel, o lo trae al frente si ya está abierto. Llamar desde un comando de Revit.</summary>
        public static void Show(UIApplication app)
        {
            if (_window != null)
            {
                if (_window.WindowState == WindowState.Minimized) _window.WindowState = WindowState.Normal;
                _window.Activate();
                return;
            }

            if (_event == null)
            {
                // ExternalEvent.Create necesita un contexto válido de la API: el comando lo es.
                _handler = new AlignRequestHandler();
                _event = ExternalEvent.Create(_handler);
            }

            IntPtr revitWindow = IntPtr.Zero;
            try { revitWindow = app.MainWindowHandle; } catch { }

            _window = new AlignPanelWindow(_event, _handler, revitWindow);
            if (revitWindow != IntPtr.Zero)
            {
                // Revit como propietario: el panel flota siempre sobre Revit y se minimiza con él.
                new WindowInteropHelper(_window) { Owner = revitWindow };
            }
            _window.Closed += (s, e) => _window = null;
            _window.Show();
        }

        public static void Shutdown()
        {
            try { _window?.Close(); } catch { }
            _window = null;
            try { _event?.Dispose(); } catch { }
            _event = null;
            _handler = null;
        }
    }
}
