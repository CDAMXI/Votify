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
        public ICollection<Competidor> competidores;
        public ICollection<Jurado> jurados;
        public ICollection<Publico> publicos;
        public EncargadoVotacion Encargado;
        public ICollection<Criterio> criterios;
        public ICollection<Voto> votos;
        public Categoria categoria;
        public Ranking ranking;
        public virtual Evento evento { get; set; }

    }
}
