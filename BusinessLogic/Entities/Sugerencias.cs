using System;
using System.Collections.Generic;
using System.Text;

namespace Votify.Entities
{
    public partial class Sugerencias
    {
        public Sugerencias() { }
        public Sugerencias(string contenido, DateTime fecha)
        {
            Contenido = contenido;
            Fecha = fecha;
        }
    }
}
