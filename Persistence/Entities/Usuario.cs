using System;
using System.Collections.Generic;
using System.Text;

namespace Votify.Entities
{
    public partial class Usuario
    {
        //Atributos
        public string Username { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
        public int Id { get; set; }

        //Relaciones
        public ICollection<Rol> roles;
        public ICollection<Evento> eventos;
    }
}
