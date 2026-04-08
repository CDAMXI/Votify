using System;
using System.Collections.Generic;
using System.Text;

namespace Votify.Entities
{
    public partial class Voto
    {
        public Voto() { }

        public Voto(double valor, string comentario, DateTime fecha)
        {
            if (valor < 0 || valor > 10)
                throw new ArgumentException("El valor debe estar entre 0 y 10");

            if (comentario != null && comentario.Length > 500)
                throw new ArgumentException("El comentario no puede superar los 500 caracteres");

            Valor = valor;
            Comentario = comentario;
            Fecha = fecha;
        }
    }
}
