using System;
using System.Collections.Generic;
using System.Text;

namespace Votify.Entities
{
    public partial class Categoria
    {
        public Categoria()
        {
        }

        public Categoria(string nombre, string descripcion) : this()
        {
            Nombre = nombre;
            Descripcion = descripcion;
        }
    }
}
