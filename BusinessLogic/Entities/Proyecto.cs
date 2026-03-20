using System;
using System.Collections.Generic;
using System.Text;

namespace Votify.Entities
{
    public partial class Proyecto
    {
        public Proyecto() { }

        public Proyecto(int id, string nombre, string descripcion, ICollection<string> materiales)
        {
            Id = id;
            Nombre = nombre;
            Descripcion = descripcion;
            Materiales = materiales;
        }
    }
}
