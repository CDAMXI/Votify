using Votify.Persistence;
using Votify.Entities;

namespace Votify.BusinessLogic.Service
{
    public class VotifyService : IVotifyService
    {
        private const string MensajeNoUsuarioLogueado = "No hay ningún usuario logueado";
        private const int MaxLongitudComentario = 500;
        private const int MinutosExpiracionResetToken = 10;

        private Usuario? usuario;
        private Rol? rol;
        private readonly IDAL dal;

        public VotifyService(IDAL dal)
        {
            this.dal = dal;
        }

        public void LogIn(string username, string password)
        {
            Usuario user = dal.GetWhere<Usuario>(u => u.Username == username).FirstOrDefault();
            if (user == null || password != user.Password)
                throw new ServiceException("Usuario o contraseña no válidos");

            usuario = user;
            CargarRolDeUsuario(user);
        }

        public void LogOut()
        {
            RequireUsuarioLogueado();
            usuario = null;
            rol = null;
        }

        public void RestoreSession(string username)
        {
            Usuario user = dal.GetWhere<Usuario>(u => u.Username == username).FirstOrDefault();
            if (user == null)
                throw new ServiceException("Usuario no encontrado");

            usuario = user;
            CargarRolDeUsuario(user);
        }

        public void Registrar(string username, string email, string password)
        {
            bool usuarioExistente = dal.GetWhere<Usuario>(u => u.Username == username).Any();
            if (usuarioExistente)
                throw new ServiceException("El usuario ya existe");

            dal.Insert<Usuario>(new Usuario(username, email, password, 0));
            dal.Commit();
        }

        public Usuario GetUsuarioActual() => usuario;

        public (string Username, string Email, string? FotoPerfil) GetPerfil()
        {
            RequireUsuarioLogueado();
            return (usuario!.Username, usuario.Email, usuario.FotoPerfil);
        }

        public void UpdateEmail(string nuevoEmail)
        {
            RequireUsuarioLogueado();
            usuario!.Email = nuevoEmail;
            dal.Commit();
        }

        public void UpdatePassword(string passwordActual, string nuevaPassword)
        {
            RequireUsuarioLogueado();
            if (usuario!.Password != passwordActual)
                throw new ServiceException("La contraseña actual no es correcta");

            usuario.Password = nuevaPassword;
            dal.Commit();
        }

        public void UpdateFotoPerfil(string base64Foto)
        {
            RequireUsuarioLogueado();
            usuario!.FotoPerfil = base64Foto;
            dal.Commit();
        }

        public string GeneratePasswordResetToken(string email)
        {
            Usuario user = dal.GetWhere<Usuario>(u => u.Email == email).FirstOrDefault();
            if (user == null)
                throw new ServiceException("No existe ninguna cuenta con ese correo");

            user.ResetToken = Guid.NewGuid().ToString("N");
            user.ResetTokenExpiry = DateTime.UtcNow.AddMinutes(MinutosExpiracionResetToken);
            dal.Commit();
            return user.ResetToken;
        }

        public void ResetPassword(string token, string nuevaPassword)
        {
            Usuario user = dal.GetWhere<Usuario>(u => u.ResetToken == token).FirstOrDefault();
            if (user == null)
                throw new ServiceException("El enlace no es válido");

            if (user.ResetTokenExpiry < DateTime.UtcNow)
                throw new ServiceException("El enlace ha expirado");

            user.Password = nuevaPassword;
            user.ResetToken = null;
            user.ResetTokenExpiry = null;
            dal.Commit();
        }

        public void GuardarVoto(int idVotacion, int idCompetidor, double puntuacion, string? comentario)
        {
            RequireUsuarioLogueado();
            RequireRolActivo();
            ValidarPermisosVoto();
            ValidarLongitudComentario(comentario);

            Votacion votacion = ObtenerVotacionOFallar(idVotacion);
            Proyecto proyecto = ObtenerProyectoOFallar(idCompetidor);
            Evento evento = ObtenerEventoDeVotacionOFallar(votacion);

            ValidarCompetidorPuedeVotar(evento);
            ValidarVotoUnico(votacion, proyecto);

            Voto voto = new Voto(puntuacion, comentario ?? string.Empty, DateTime.Now)
            {
                votacion = votacion,
                proyecto = proyecto,
                votante = rol
            };

            dal.Insert<Voto>(voto);
            dal.Commit();
        }

        public void Commit() => dal.Commit();

        public int CrearVotacion(DateTime fechaFin, bool activa)
        {
            RequireUsuarioLogueado();

            EncargadoVotacion encargado = new EncargadoVotacion(DateTime.Now, 0);
            encargado.usuario = usuario;

            Votacion votacion = new Votacion(DateTime.Now, fechaFin, activa, encargado);

            dal.Insert<EncargadoVotacion>(encargado);
            dal.Insert<Votacion>(votacion);
            dal.Commit();

            return votacion.Id;
        }

        public IEnumerable<Votacion> GetMisVotaciones()
        {
            RequireUsuarioLogueado();
            return dal.GetWhere<Votacion>(v => v.Encargado != null && v.Encargado.usuario.Id == usuario!.Id);
        }

        public void BorrarVotacion(int idVotacion)
        {
            Votacion votacion = dal.GetById<Votacion>(idVotacion);
            if (votacion.Encargado != rol)
                throw new ServiceException("No eres el encargado de esta votación");

            dal.Delete<Votacion>(votacion);
            dal.Commit();
        }

        public Votacion GetVotacion(int idVotacion)
        {
            Votacion votacion = dal.GetById<Votacion>(idVotacion);
            if (votacion == null)
                throw new ServiceException("La votación no existe");
            return votacion;
        }

        public void ModificarFechaVotacion(int idVotacion, DateTime nuevaFechaFin)
        {
            RequireUsuarioLogueado();
            Votacion votacion = dal.GetById<Votacion>(idVotacion);
            if (votacion == null)
                throw new ServiceException("La votación no existe");
            if (votacion.Encargado?.usuario?.Id != usuario!.Id)
                throw new ServiceException("No eres el encargado de esta votación");

            votacion.FechaFin = nuevaFechaFin;
            dal.Commit();
        }

        public Rol GetRolEnEvento(int idEvento)
        {
            RequireUsuarioLogueado();
            return usuario!.roles?.FirstOrDefault(r => r.evento?.IdEvento == idEvento);
        }

        public bool HasVotadoEnEvento(int idEvento)
        {
            RequireUsuarioLogueado();
            return dal.GetWhere<Voto>(v =>
                v.votante.usuario.Id == usuario!.Id &&
                v.votacion.evento.IdEvento == idEvento
            ).Any();
        }

        public void AsignarRolEnEvento(string tipoRol, int idEvento)
        {
            RequireUsuarioLogueado();

            Evento evento = dal.GetById<Evento>(idEvento);
            if (evento == null)
                throw new ServiceException("El evento no existe");

            Rol nuevoRol = RolFactory.Create(tipoRol, DateTime.Now, 0);
            nuevoRol.evento = evento;
            usuario!.roles ??= new List<Rol>();
            usuario.roles.Add(nuevoRol);
            dal.Insert<Rol>(nuevoRol);
            dal.Commit();
        }

        // ── Helpers privados ────────────────────────────────────────────────

        private void CargarRolDeUsuario(Usuario user)
        {
            Rol? rolRecuperado = user.roles?.FirstOrDefault();
            rol = rolRecuperado != null
                ? RolFactory.Create(rolRecuperado.RolVotante(), rolRecuperado.FechaAsignacion, rolRecuperado.RawScore())
                : null;
        }

        private void RequireUsuarioLogueado()
        {
            if (usuario == null)
                throw new ServiceException(MensajeNoUsuarioLogueado);
        }

        private void RequireRolActivo()
        {
            if (rol == null)
                throw new ServiceException("No hay ningún rol activo");
        }

        private void ValidarPermisosVoto()
        {
            if (rol is Organizador || rol is EncargadoVotacion)
                throw new ServiceException("El rol actual no puede votar");
        }

        private void ValidarLongitudComentario(string? comentario)
        {
            if (comentario != null && comentario.Length > MaxLongitudComentario)
                throw new ServiceException($"El comentario no puede superar los {MaxLongitudComentario} caracteres");
        }

        private void ValidarCompetidorPuedeVotar(Evento evento)
        {
            if (rol is Competidor && !evento.PermiteCompetidoresVotar)
                throw new ServiceException("Los competidores no pueden votar en este evento");
        }

        private void ValidarVotoUnico(Votacion votacion, Proyecto proyecto)
        {
            bool yaVotó = dal.GetWhere<Voto>(v =>
                v.votante == rol && v.votacion == votacion && v.proyecto == proyecto
            ).Any();

            if (yaVotó)
                throw new ServiceException("Ya has votado en este proyecto para esta votación");
        }

        private Votacion ObtenerVotacionOFallar(int idVotacion)
        {
            Votacion votacion = dal.GetById<Votacion>(idVotacion);
            if (votacion == null)
                throw new ServiceException("La votación no existe");
            return votacion;
        }

        private Proyecto ObtenerProyectoOFallar(int idProyecto)
        {
            Proyecto proyecto = dal.GetById<Proyecto>(idProyecto);
            if (proyecto == null)
                throw new ServiceException("El proyecto no existe");
            return proyecto;
        }

        private Evento ObtenerEventoDeVotacionOFallar(Votacion votacion)
        {
            if (votacion.evento == null)
                throw new ServiceException("La votación no está asociada a ningún evento");
            return votacion.evento;
        }
    }
}
