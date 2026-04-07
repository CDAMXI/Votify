using System;
using System.Collections.Generic;
using System.Text;

namespace Votify.Entities
{
    public partial class Organizador : Rol
    {
        public Organizador() : base() { }
        public Organizador(DateTime fechaAsignacion, double rawScore) : base(fechaAsignacion, rawScore) { }

        //Método Fábrica
        public override string RolVotante() => "ORGANIZER";
        public override double NormalizedScore() => rawScore;
    }
}
