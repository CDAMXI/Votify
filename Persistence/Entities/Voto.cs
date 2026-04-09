using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Votify.Entities
{
    public partial class Voto
    {
        //Atributos
        [Key]
        public int Id { get; set; }
        public double Valor { get; set; }
        public string Comentario { get; set; }
        public DateTime Fecha { get; set; }

        public int VotanteId { get; set; }
        public int VotacionId { get; set; }
        public int ProyectoId { get; set; }

        //Relaciones
        public virtual Votacion votacion { get; set; }
        public virtual Proyecto proyecto { get; set; }
        public virtual Rol votante { get; set; }
    }
}
