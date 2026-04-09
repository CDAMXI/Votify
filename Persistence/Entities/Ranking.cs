using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Votify.Entities
{
    public partial class Ranking
    {
        [Key]
        public int Id { get; set; }
        public int Posicion { get; set; }
        public double PuntajeTotal { get; set; }
        public bool EsManual { get; set; }

        public virtual Proyecto proyecto { get; set; }
        public virtual Votacion votacion { get; set; }
    }
}
