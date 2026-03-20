using System;
using System.Collections.Generic;
using System.Text;

namespace Votify.Entities
{
    public abstract partial class Publico : Rol
    {
        public Publico() : base()
        {
        }
        public Publico(DateTime fechaAsignacion, double rawScore) : base(fechaAsignacion, rawScore)
        {
        }

        public override string RolVotante() => "PUBLIC";
        public override double NormalizedScore() => rawScore * 0.8;
    }
}
