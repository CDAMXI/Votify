using System;
using System.Collections.Generic;
using System.Text;

namespace Votify.Entities
{
    public partial class Organizador : Rol
    {
        //Atributos de Rol

        //Relaciones
        public ICollection<Evento> eventos;
    }
}
