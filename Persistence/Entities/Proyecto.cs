using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Votify.Entities
{
    public partial class Proyecto
    {
        [Key]
        public int Id { get; set; }
        public string Nombre { get; set; }
        public string Descripcion { get; set; }

        [NotMapped]
        public ICollection<string> Materiales { get; set; }

        public virtual Competidor competidor { get; set; }
        public virtual Evento evento { get; set; }
        public virtual Categoria categoria { get; set; }
        public virtual ICollection<Voto> votos { get; set; }
    }
}
