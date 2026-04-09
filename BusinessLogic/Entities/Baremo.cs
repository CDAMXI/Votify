using System;
using System.Collections.Generic;
using System.Text;

namespace Votify.Entities
{
    public partial class Baremo
    {
        public Baremo() { }
        public Baremo(string nombre, string descripcion, double peso, TipoCriterio tipo)
        {
            Nombre = nombre;
            Descripcion = descripcion;
            Peso = peso;
            Tipo = tipo;
        }
    }
}
