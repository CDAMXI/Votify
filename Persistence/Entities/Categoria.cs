using System;
using System.Collections.Generic;
using System.Text;

namespace Votify.Entities
{
    public partial class Categoria
    {
        //Atributos
        public string Username { get; set; }
        public string Password { get; set; }
        public string Email { get; set; }
        public int Id { get; set; }

        //Relaciones
        public Evento evento;
        public ICollection<Premios> premios;
        public HojaRuta hojaRuta; //Definir multiplicidad
    }
}
