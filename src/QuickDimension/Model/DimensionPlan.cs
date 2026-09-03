using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace QuickDimension.Model
{
    /// <summary>
    /// Lo que el motor deduce de la línea trazada: dónde va la cota, en qué dirección
    /// mide y qué referencias la componen. No toca el modelo; crear la cota es cosa del comando.
    /// </summary>
    public class DimensionPlan
    {
        /// <summary>Dos referencias a la misma cota más cerca que esto se consideran la misma (en pies, ~0,3 mm).</summary>
        private const double PositionTolerance = 1e-3;

        /// <summary>Longitud mínima de la línea de cota, por encima de la tolerancia de curva corta de Revit.</summary>
        private const double MinimumSpan = 1e-2;

        private readonly List<ReferenceCandidate> _candidates = new List<ReferenceCandidate>();
        private readonly List<string> _notes = new List<string>();

        /// <summary>Punto por el que pasa la cota: el centro de la línea trazada, ya desplazado.</summary>
        public XYZ Origin { get; set; }

        /// <summary>Dirección exacta en la que mide la cota, deducida de la geometría encontrada.</summary>
        public XYZ Direction { get; set; }

        /// <summary>Motivo por el que no se puede acotar, o <c>null</c> si el plan es utilizable.</summary>
        public string FailureReason { get; set; }

        public IList<ReferenceCandidate> Candidates { get { return _candidates; } }

        public IList<string> Notes { get { return _notes; } }

        public bool IsValid
        {
            get { return FailureReason == null && Origin != null && Direction != null && _candidates.Count >= 2; }
        }

        public void AddNote(string note)
        {
            if (!string.IsNullOrEmpty(note) && !_notes.Contains(note)) _notes.Add(note);
        }

        /// <summary>
        /// Prepara la cota usando solo las referencias hasta el nivel de fiabilidad indicado.
        /// Devuelve <c>false</c> si con ese subconjunto no queda una cota válida.
        /// </summary>
        public bool TryBuild(ReferenceTier maxTier, out Line line, out ReferenceArray references, out int segmentCount)
        {
            line = null;
            references = null;
            segmentCount = 0;

            if (Origin == null || Direction == null) return false;

            var kept = new List<ReferenceCandidate>();
            foreach (ReferenceCandidate candidate in _candidates)
            {
                if (candidate.Tier <= maxTier) kept.Add(candidate);
            }
            if (kept.Count < 2) return false;

            kept.Sort((a, b) => a.Position.CompareTo(b.Position));

            // Dos referencias en la misma posición darían un segmento de longitud cero y Revit lo rechaza.
            var unique = new List<ReferenceCandidate>();
            foreach (ReferenceCandidate candidate in kept)
            {
                if (unique.Count > 0 &&
                    Math.Abs(candidate.Position - unique[unique.Count - 1].Position) < PositionTolerance)
                {
                    continue;
                }
                unique.Add(candidate);
            }
            if (unique.Count < 2) return false;

            double first = unique[0].Position;
            double last = unique[unique.Count - 1].Position;
            if (last - first < MinimumSpan) return false;

            line = Line.CreateBound(
                Origin + Direction.Multiply(first),
                Origin + Direction.Multiply(last));

            references = new ReferenceArray();
            foreach (ReferenceCandidate candidate in unique) references.Append(candidate.Reference);

            segmentCount = unique.Count - 1;
            return true;
        }
    }
}
