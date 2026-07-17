namespace LinkWorksetInspector.Models
{
    /// <summary>
    /// Una fila del informe: una instancia de vínculo (o un tipo de vínculo sin
    /// instancias colocadas) con su workset y el diagnóstico de visibilidad.
    /// </summary>
    public class LinkReportRow
    {
        public string LinkName { get; set; }

        /// <summary>"RVT" o "CAD" (DWG, DXF, DGN…).</summary>
        public string Format { get; set; }

        /// <summary>Estado de carga, ya traducido (Cargado, Descargado, En workset cerrado…).</summary>
        public string LoadStatus { get; set; }

        /// <summary>Workset al que pertenece el TIPO de vínculo (lo que decide si Revit lo carga).</summary>
        public string TypeWorkset { get; set; }

        /// <summary>"abierto", "CERRADO" o "—".</summary>
        public string TypeWorksetState { get; set; }

        /// <summary>Workset al que pertenece la INSTANCIA colocada en el modelo.</summary>
        public string InstanceWorkset { get; set; }

        /// <summary>"abierto", "CERRADO" o "—".</summary>
        public string InstanceWorksetState { get; set; }

        /// <summary>"Sí", "No" o "—" (sin instancia o sin vista activa).</summary>
        public string VisibleInActiveView { get; set; }

        /// <summary>Explicación de por qué no se ve y qué hacer para verlo.</summary>
        public string Diagnosis { get; set; }

        /// <summary>Ruta del archivo vinculado (o ruta de nube).</summary>
        public string FilePath { get; set; }

        /// <summary>true si hay algo que impide ver o cargar el vínculo.</summary>
        public bool HasProblem { get; set; }
    }
}
