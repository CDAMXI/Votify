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
            Valor = valor;
            Comentario = comentario;
            Fecha = fecha;
        }
    }
}
