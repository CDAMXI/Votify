using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Votify.Entities
{
    public partial class Rol
    {
        //Atributos
        [Key]
        public int Id { get; set; }
        public DateTime FechaAsignacion { get; set; }

        //Relaciones
        public virtual Evento evento { get; set; }
        public virtual Usuario usuario { get; set; }
    }
}
