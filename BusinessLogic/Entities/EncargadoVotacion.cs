namespace Votify.Entities
{
    public partial class EncargadoVotacion : Rol
    {
        public EncargadoVotacion() : base()
        {
            votaciones = new List<Votacion>();
        }
        public EncargadoVotacion(DateTime fechaAsignacion, double rawScore)
            : base(fechaAsignacion, rawScore)
        {
            votaciones = new List<Votacion>();
        }

        //Identificador de tipo (usado por RolFactory)
        public override string RolVotante() => "VOTING_MANAGER";
        public override double NormalizedScore() => rawScore;
    }
}