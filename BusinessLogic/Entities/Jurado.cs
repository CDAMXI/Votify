using System;
using System.Collections.Generic;
using System.Text;

namespace Votify.Entities
{
    public partial class Jurado : Rol
    {
        public Jurado() : base()
        {
            TipoRol = "JURADO";
        }
        public Jurado(DateTime fechaAsignacion, double rawScore) : base(fechaAsignacion, rawScore)
        {
            TipoRol = "JURADO";
        }

        //Identificador de tipo (usado por RolFactory)
        public override string RolVotante() => "JURADO";
        public override double NormalizedScore() => rawScore * 1.2;
    }
}
