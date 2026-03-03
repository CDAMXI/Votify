using System;
using System.Collections.Generic;
using System.Text;

namespace Votify.BuisnessLogic
{
    internal class Jurado : Votante
    {
        public Jurado() { }

        public Jurado(string name) { }

        public string RolJurado() { return "Jurado"; }
    }
}
