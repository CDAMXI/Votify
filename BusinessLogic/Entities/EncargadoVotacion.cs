namespace Votify.Entities
{
    public partial class EncargadoVotacion : Rol
    {
        public EncargadoVotacion() : base() { }
        public EncargadoVotacion(DateTime fechaAsignacion, double rawScore)
            : base(fechaAsignacion, rawScore) { }

        public override string RolVotante() => "VOTING_MANAGER";
        public override double NormalizedScore() => rawScore;
    }
}