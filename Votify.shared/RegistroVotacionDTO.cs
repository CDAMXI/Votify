using System;
using System.Collections.Generic;
using System.Text;

namespace Votify.shared
{
    internal class RegistroVotacionDTO
    {
        public required int IdPropuesta { get; set; }
        public required string UNombreUsuario { get; set; }
    }
}
