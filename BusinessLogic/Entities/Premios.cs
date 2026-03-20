using System;
using System.Collections.Generic;
using System.Text;

namespace Votify.Entities
{
    public partial class Premios
    {
        public Premios() { }
        public Premios(int id, string nombre, string descripcion)
        {
            Id = id;
            Nombre = nombre;
            Descripcion = descripcion;
        }
    }
}
