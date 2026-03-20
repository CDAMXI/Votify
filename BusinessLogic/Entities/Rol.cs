using System;
using System.Collections.Generic;
using System.Text;

namespace Votify.Entities
{
    public partial class Rol
    {
        public Rol() { }

        public Rol(DateTime fechaAsignacion)
        {
            FechaAsignacion = fechaAsignacion;
        }
    }
}
