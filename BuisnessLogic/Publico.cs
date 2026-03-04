using System;
using System.Collections.Generic;
using System.Text;
using Votify.BusinessLogic;

namespace Votify.BuisnessLogic
{
    internal interface Publico : Votante
    {
        public string RolVotante()
        {
            return "PUBLIC"; // Debe retornar un string
        }
    }
}
