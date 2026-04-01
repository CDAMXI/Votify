using System;
using System.Collections.Generic;
using System.Text;

namespace Votify.Entities
{
    public partial class Criterio
    {
        public Criterio() { }
        public Criterio(string nombre, string descripcion, double peso, TipoCriterio tipo)
        {
            Nombre = nombre;
            Descripcion = descripcion;
            Peso = peso;
            Tipo = tipo;
        }
    }
}
