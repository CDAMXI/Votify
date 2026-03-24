using System;
using System.Collections.Generic;
using System.Text;

namespace Votify.shared
{
    internal class InfoSesionDTO
    {
        public required string Token { get; set; }
        public required string UNombreUsuario{ get; set; }
        public required string Rol{ get; set; }
    }
}
