namespace Votify.Entities
{
    public partial class EncargadoVotacion : Rol
    {
        public EncargadoVotacion() : base()
        {
            TipoRol = "ENCARGADO";
            votaciones = new List<Votacion>();
        }
        public EncargadoVotacion(DateTime fechaAsignacion, double rawScore)
            : base(fechaAsignacion, rawScore)
        {
            TipoRol = "ENCARGADO";
            votaciones = new List<Votacion>();
        }

        //Identificador de tipo (usado por RolFactory)
        public override string RolVotante() => "ENCARGADO";
        public override double NormalizedScore() => rawScore;
    }
}
