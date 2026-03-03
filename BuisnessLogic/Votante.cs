using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Votify.BuisnessLogic;

namespace Votify.BusinessLogic // Corregido el error tipográfico
{
    internal class Votante : Usuario
    {
        public Votante(int id) : base(id) // Asumiendo que Usuario requiere id
        {
        }

        public string RolVotante()
        {
            return "Votante"; // Debe retornar un string
        }
    }
}
