using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Votify.Entities
{
    public partial class Votacion
    {
        //Atributos
        public int Id { get; set; }
        public DateTime FechaIni { get; set; }
        public DateTime FechaFin { get; set; }
        public bool Estado { get; set; }

        //Relaciones
        public virtual ICollection<Competidor> competidores { get; set; }
        public virtual ICollection<Jurado> jurados { get; set; }
        public virtual ICollection<Publico> publicos { get; set; }
        public virtual EncargadoVotacion Encargado { get; set; }
        public virtual ICollection<Baremo> criterios { get; set; }
        public virtual ICollection<Voto> votos { get; set; }
        public virtual Categoria categoria { get; set; }
        public virtual Ranking ranking { get; set; }
        public virtual Evento evento { get; set; }

    }
}
