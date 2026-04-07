using System;
using System.Collections.Generic;
using System.Text;

namespace Votify.Entities
{
    public partial class Voto
    {
        //Atributos
        public double Valor { get; set; }
        public string Comentario { get; set; }
        public DateTime Fecha { get; set; }

        public int VotanteId { get; set; }
        public int VotacionId { get; set; }
        public int ProyectoId { get; set; }

        //Relaciones
        public Votacion votacion;
        public Proyecto proyecto;
        public Rol votante;
    }
}
