using System;
using System.Collections.Generic;
using System.Text;

namespace Votify.shared
{
    internal class ResultadosDTO
    {
        public required int IdPropuesta { get; set; }
        public required string Titulo { get; set; }
        public required string Descripcion { get; set; }
        public required DateOnly FechaFin { get; set; }
        public required int Votos { get; set; }
    }
}
