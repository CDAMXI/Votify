using System;
using System.Collections.Generic;
using System.Text;

namespace Votify.Entities
{
    public partial class EncargadoVotacion : Rol
    {
        //Atributos de Rol

        //Relaciones
        public ICollection<Votacion> votaciones { get; set; }
    }
}
