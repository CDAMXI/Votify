using System;
using System.Collections.Generic;
using System.Text;

namespace Votify.Entities
{
    public partial class Usuario
    {
        //Atributos
        public string Username { get; set; }
        public string Password { get; set; }
        public string Email { get; set; }
        public int Id { get; set; }
        public string? FotoPerfil { get; set; }

        //Relaciones
        public virtual ICollection<Rol> roles { get; set; }
        public virtual ICollection<Evento> eventos { get; set; }
    }
}
