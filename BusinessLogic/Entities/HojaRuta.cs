using System;
using System.Collections.Generic;
using System.Text;

namespace Votify.Entities
{
    public partial class HojaRuta
    {
        public HojaRuta() { }

        public HojaRuta(string contenido, DateTime fechaGeneracion)
        {
            Descripcion = contenido;
            FechaGeneracion = fechaGeneracion;
        }
    }
}
