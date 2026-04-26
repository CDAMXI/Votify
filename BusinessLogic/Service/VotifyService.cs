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
            username = username.Trim();
            email = email.Trim().ToLowerInvariant();

            bool usuarioExistente = dal.GetWhere<Usuario>(u => u.Username == username).Any();
            if (usuarioExistente)
                throw new ServiceException("El usuario ya existe");

            bool emailExistente = dal.GetWhere<Usuario>(u => u.Email.ToLower() == email).Any();
            if (emailExistente)
                throw new ServiceException("El correo ya está registrado");

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
            ValidarLongitudComentario(comentario);

            Votacion votacion = ObtenerVotacionOFallar(idVotacion);
            Proyecto proyecto = ObtenerProyectoOFallar(idCompetidor);
            Evento evento = ObtenerEventoDeVotacionOFallar(votacion);

            // Buscar el rol del usuario para este evento consultando cada subtype
            // individualmente — evita el JOIN multi-tabla TPT de EF6+Npgsql
            int eventoId = evento.IdEvento;
            Rol? rolEvento = BuscarRolEnEvento(eventoId);
            if (rolEvento == null)
                throw new ServiceException("No tienes un rol asignado en este evento");

            if (rolEvento is Organizador || rolEvento is EncargadoVotacion)
                throw new ServiceException("El rol actual no puede votar");

            if (rolEvento is Competidor comp && !evento.PermiteCompetidoresVotar)
                throw new ServiceException("Los competidores no pueden votar en este evento");

            // Usar IDs directamente para evitar el bug de JOINs de EF6+Npgsql
            bool yaVoto = dal.GetWhere<Voto>(v =>
                v.VotanteId == rolEvento.Id &&
                v.VotacionId == votacion.Id &&
                v.ProyectoId == proyecto.Id
            ).Any();
            if (yaVoto)
                throw new ServiceException("Ya has votado en este proyecto para esta votación");

            Voto voto = new Voto(puntuacion, comentario ?? string.Empty, DateTime.Now)
            {
                votacion = votacion,
                proyecto = proyecto,
                votante = rolEvento
            };

            dal.Insert<Voto>(voto);
            dal.Commit();
        }

        // Consulta cada tabla concreta de rol por separado para evitar el
        // JOIN multi-tabla que genera EF6 al usar la clase base abstracta Rol
        private Rol? BuscarRolEnEvento(int eventoId)
        {
            int uid = usuario!.Id;
            return
                (Rol?)dal.GetWhere<Jurado>(r => r.UsuarioId == uid && r.EventoId == eventoId).FirstOrDefault() ??
                (Rol?)dal.GetWhere<Publico>(r => r.UsuarioId == uid && r.EventoId == eventoId).FirstOrDefault() ??
                (Rol?)dal.GetWhere<Competidor>(r => r.UsuarioId == uid && r.EventoId == eventoId).FirstOrDefault() ??
                (Rol?)dal.GetWhere<Organizador>(r => r.UsuarioId == uid && r.EventoId == eventoId).FirstOrDefault() ??
                (Rol?)dal.GetWhere<EncargadoVotacion>(r => r.UsuarioId == uid && r.EventoId == eventoId).FirstOrDefault();
        }

        public void Commit() => dal.Commit();

        public int CrearVotacion(string titulo, string? descripcion, DateTime fechaFin, bool activa)
        {
            RequireUsuarioLogueado();

            DateTime fechaInicio = DateTime.Now;
            if (fechaFin <= fechaInicio)
                throw new ServiceException("La fecha de fin debe ser posterior a la fecha actual");

            string nombre = string.IsNullOrWhiteSpace(titulo) ? "Votacion" : titulo.Trim();
            string descripcionNormalizada = descripcion?.Trim() ?? string.Empty;

            // 1. Insertar Evento primero para obtener su ID antes de crear los roles
            Evento evento = new Evento
            {
                Nombre = nombre,
                Descripcion = descripcionNormalizada,
                FechaIni = fechaInicio,
                FechaFin = fechaFin,
                PermiteCompetidoresVotar = false,
                organizador = usuario!,
                OrganizadorId = usuario!.Id
            };
            dal.Insert<Evento>(evento);
            dal.Commit(); // evento.IdEvento queda asignado por la BD

            // 2. Insertar roles con FK escalar ya conocida para evitar el bug de shadow FK de EF6
            Organizador organizador = new Organizador(fechaInicio, 0)
            {
                usuario  = usuario,
                evento   = evento,
                UsuarioId = usuario!.Id,
                EventoId  = evento.IdEvento
            };

            EncargadoVotacion encargado = new EncargadoVotacion(fechaInicio, 0)
            {
                usuario  = usuario,
                evento   = evento,
                UsuarioId = usuario!.Id,
                EventoId  = evento.IdEvento
            };

            dal.Insert<Organizador>(organizador);
            dal.Insert<EncargadoVotacion>(encargado);
            dal.Commit(); // encargado.Id queda asignado por la BD

            // 3. Insertar Votacion con FKs escalares explícitas — evita shadow FK de EF6
            Votacion votacion = new Votacion(fechaInicio, fechaFin, activa, encargado)
            {
                Titulo       = nombre,
                Descripcion  = descripcionNormalizada,
                evento       = evento,
                EventoId     = evento.IdEvento,
                EncargadoId  = encargado.Id
            };
            dal.Insert<Votacion>(votacion);
            dal.Commit();

            return votacion.Id;
        }

        public IEnumerable<Votacion> GetMisVotaciones()
        {
            RequireUsuarioLogueado();

            // EF6+Npgsql falla con JOINs profundos desde Votaciones hacia Roles/Usuarios.
            // Solución: obtener IDs de encargado desde usuario.roles (ya cargado en memoria),
            // luego materializar TODAS las votaciones con una sola SELECT sin JOIN,
            // y filtrar en memoria con lazy loading solo sobre la tabla Roles.
            var encargadoIds = (usuario!.roles
                ?.OfType<EncargadoVotacion>()
                .Select(e => e.Id)
                .ToHashSet()) ?? new HashSet<int>();

            if (!encargadoIds.Any())
                return Enumerable.Empty<Votacion>();

            return dal.GetAll<Votacion>()   // SELECT * FROM Votaciones — sin JOINs
                      .ToList()             // materializar en memoria
                      .Where(v => v.Encargado != null && encargadoIds.Contains(v.Encargado.Id))
                      .ToList();
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
            rol = rolRecuperado;
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

        public void ModificarEvento(int idVotacion, string titulo, string descripcion, DateTime fechaFin)
        {
            RequireUsuarioLogueado();
            Votacion votacion = ObtenerVotacionOFallar(idVotacion);
            if (votacion.Encargado?.usuario?.Id != usuario!.Id)
                throw new ServiceException("No eres el encargado de esta votación");

            string nombre = string.IsNullOrWhiteSpace(titulo) ? votacion.Titulo : titulo.Trim();
            string desc = descripcion?.Trim() ?? string.Empty;

            votacion.Titulo = nombre;
            votacion.Descripcion = desc;
            votacion.FechaFin = fechaFin;

            Evento evento = ObtenerEventoDeVotacionOFallar(votacion);
            evento.Nombre = nombre;
            evento.Descripcion = desc;
            evento.FechaFin = fechaFin;

            dal.Commit();
        }

        public void EliminarEvento(int idVotacion)
        {
            RequireUsuarioLogueado();
            Votacion votacion = ObtenerVotacionOFallar(idVotacion);
            if (votacion.Encargado?.usuario?.Id != usuario!.Id)
                throw new ServiceException("No eres el encargado de esta votación");

            Evento evento = ObtenerEventoDeVotacionOFallar(votacion);
            dal.Delete<Evento>(evento);
            dal.Commit();
        }

        private Evento ObtenerEventoDeVotacionOFallar(Votacion votacion)
        {
            if (votacion.evento == null)
                throw new ServiceException("La votación no está asociada a ningún evento");
            return votacion.evento;
        }
    }
}
