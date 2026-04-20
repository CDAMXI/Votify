using System;
using System.Collections.Generic;
using System.Text;

namespace Votify.Entities
{
    public partial class Publico : Rol
    {
        public Publico() : base()
        {
            TipoRol = "PUBLICO";
        }
        public Publico(DateTime fechaAsignacion, double rawScore) : base(fechaAsignacion, rawScore)
        {
            TipoRol = "PUBLICO";
        }

        //Identificador de tipo (usado por RolFactory)
        public override string RolVotante() => "PUBLICO";
        public override double NormalizedScore() => rawScore * 0.8;
    }
}
