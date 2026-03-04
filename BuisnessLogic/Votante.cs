using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Votify.BuisnessLogic;

namespace Votify.BusinessLogic // Corregido el error tipográfico
{
    internal interface Votante : Usuario
    {
        public string RolVotante()
        {
            return "Votante"; // Debe retornar un string
        }
    }
}
