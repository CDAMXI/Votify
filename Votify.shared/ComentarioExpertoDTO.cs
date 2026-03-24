using System;
using System.Collections.Generic;
using System.Text;

namespace Votify.shared
{
    internal class ComentarioExpertoDTO
    {
        public required int Id { get; set; }
        public required string UNombreExperto { get; set; }
        public required int IdPropuesta { get; set; }
        public required string Contenido { get; set; }

    }
}
