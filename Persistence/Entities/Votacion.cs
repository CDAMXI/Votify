using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.ComponentModel.DataAnnotations.Schema;
using Votify.Entities.EstadosVotacion;

namespace Votify.Entities
{
    public partial class Votacion
    {
        //Atributos
        public int Id { get; set; }
        public string Titulo { get; set; }
        public string Descripcion { get; set; }
        public DateTime FechaIni { get; set; }
        public DateTime FechaFin { get; set; }
        public string NombreEstado { get; set; } = "Activa";
        
        [NotMapped]
        public EstadoVotacion EstadoActual 
        { 
            get => EstadoFactory.Crear(NombreEstado);
            set => NombreEstado = value.ObtenerNombre();
        }
        public int EventoId { get; set; }
        public int EncargadoId { get; set; }
        public int PesoJurado { get; set; } = 70;
        public int PesoPublico { get; set; } = 30;
        public string EstrategiaCalculo { get; set; } = "ESTANDAR";

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
