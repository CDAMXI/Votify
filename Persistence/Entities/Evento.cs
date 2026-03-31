using System;
using System.Collections.Generic;
using System.Text;

namespace Votify.Entities
{
    public partial class Evento
    {
        //Atributos
        public int IdEvento { get; set; }
        public string Nombre { get; set; }
        public DateTime FechaIni { get; set; }
        public DateTime FechaFin { get; set; }
        public string Descripcion { get; set; }

        //Relaciones
        public ICollection<Sugerencias> sugerencias;
        public Rol rol;
        public ICollection<Proyecto> proyectos;
        public ICollection<Categoria> categorias;
        public Reglas reglas;
    }
}
