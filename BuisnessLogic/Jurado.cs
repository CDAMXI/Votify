using System;
using System.Collections.Generic;
using System.Text;

namespace Votify.BuisnessLogic
{
    internal interface Jurado : Votante
    {
        public string RolJurado() { return "EXPERT"; }
    }
}
