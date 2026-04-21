using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Votify.Entities
{
    public partial class Evento
    {
        //Atributos
        [Key]
        public int IdEvento { get; set; }
        public int OrganizadorId { get; set; }
        public string Nombre { get; set; }
        public DateTime FechaIni { get; set; }
        public DateTime FechaFin { get; set; }
        public string Descripcion { get; set; }
        public bool PermiteCompetidoresVotar { get; set; }
        


        //Relaciones
        public virtual Usuario organizador { get; set; }
        public virtual ICollection<Sugerencias> sugerencias { get; set; }
        public virtual ICollection<Rol> roles { get; set; }
        public virtual ICollection<Proyecto> proyectos { get; set; }
        public virtual ICollection<Categoria> categorias { get; set; }
        public virtual Reglas reglas { get; set; }
        public virtual ICollection<Votacion> votaciones { get; set; }
    }
}
