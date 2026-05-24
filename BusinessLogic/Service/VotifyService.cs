using System;
using System.Collections.Generic;
using System.Linq;
using Votify.Persistence;
using Votify.Entities;

namespace Votify.BusinessLogic.Service
{
    public class VotifyService : IVotifyService
    {
        private const string MensajeNoUsuarioLogueado = "No hay ningún usuario logueado";
        private const int MaxLongitudComentario = 500;
        private const int MinutosExpiracionResetToken = 10;
        private const string PrefijoCategoriaProyecto = "__CAT__:";
        private const string PrefijoCriteriosCategoria = "||__CRITERIOS__:";
        private const string PrefijoCriteriosDescripcion = "\n__CRITERIOS__:";

        private Usuario? usuario;
        private Rol? rol;

        private readonly IDAL<Usuario> _usuarioRepository;
        private readonly IDAL<Voto> _votoRepository;
        private readonly IDAL<Votacion> _votacionRepository;
        private readonly IDAL<Evento> _eventoRepository;
        private readonly IDAL<Rol> _rolRepository;
        private readonly IDAL<Proyecto> _proyectoRepository;
        private readonly IDAL<Jurado> _juradoRepository;
        private readonly IDAL<Publico> _publicoRepository;
        private readonly IDAL<Competidor> _competidorRepository;
        private readonly IDAL<Organizador> _organizadorRepository;
        private readonly IDAL<EncargadoVotacion> _encargadoRepository;
        private readonly IDAL<Reclamacion> _reclamacionRepository;

        public VotifyService(VotifyRepositories repositories)
        {
            _usuarioRepository = repositories.Usuarios;
            _votoRepository = repositories.Votos;
            _votacionRepository = repositories.Votaciones;
            _eventoRepository = repositories.Eventos;
            _rolRepository = repositories.Roles;
            _proyectoRepository = repositories.Proyectos;
            _juradoRepository = repositories.Jurados;
            _publicoRepository = repositories.Publicos;
            _competidorRepository = repositories.Competidores;
            _organizadorRepository = repositories.Organizadores;
            _encargadoRepository = repositories.Encargados;
            _reclamacionRepository = repositories.Reclamaciones;
        }

        // Delegamos el Commit global a cualquier repositorio temporalmente 
        public void Commit() => _usuarioRepository.Commit();

        public void LogIn(string username, string password)
        {
            Usuario user = _usuarioRepository.GetWhere(u => u.Username == username).FirstOrDefault();
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
            Usuario user = _usuarioRepository.GetWhere(u => u.Username == username).FirstOrDefault();
            if (user == null)
                throw new ServiceException("Usuario no encontrado");

            usuario = user;
            CargarRolDeUsuario(user);
        }

        public void Registrar(string username, string email, string password)
        {
            username = username.Trim();
            email = email.Trim().ToLowerInvariant();

            bool usuarioExistente = _usuarioRepository.GetWhere(u => u.Username == username).Any();
            if (usuarioExistente)
                throw new ServiceException("El usuario ya existe");

            bool emailExistente = _usuarioRepository.GetWhere(u => u.Email.ToLower() == email).Any();
            if (emailExistente)
                throw new ServiceException("El correo ya está registrado");

            _usuarioRepository.Insert(new Usuario(username, email, password, 0));
            Commit();
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
            Commit();
        }

        public void UpdatePassword(string passwordActual, string nuevaPassword)
        {
            RequireUsuarioLogueado();
            if (usuario!.Password != passwordActual)
                throw new ServiceException("La contraseña actual no es correcta");

            usuario.Password = nuevaPassword;
            Commit();
        }

        public void UpdateFotoPerfil(string base64Foto)
        {
            RequireUsuarioLogueado();
            usuario!.FotoPerfil = base64Foto;
            Commit();
        }

        public string GeneratePasswordResetToken(string email)
        {
            Usuario user = _usuarioRepository.GetWhere(u => u.Email == email).FirstOrDefault();
            if (user == null)
                throw new ServiceException("No existe ninguna cuenta con ese correo");

            user.ResetToken = Guid.NewGuid().ToString("N");
            user.ResetTokenExpiry = DateTime.UtcNow.AddMinutes(MinutosExpiracionResetToken);
            Commit();
            return user.ResetToken;
        }

        public void ResetPassword(string token, string nuevaPassword)
        {
            Usuario user = _usuarioRepository.GetWhere(u => u.ResetToken == token).FirstOrDefault();
            if (user == null)
                throw new ServiceException("El enlace no es válido");

            if (user.ResetTokenExpiry < DateTime.UtcNow)
                throw new ServiceException("El enlace ha expirado");

            user.Password = nuevaPassword;
            user.ResetToken = null;
            user.ResetTokenExpiry = null;
            Commit();
        }

        public void GuardarVoto(int idVotacion, int idProyecto, double puntuacion, string? comentario)
        {
            RequireUsuarioLogueado();
            ValidarLongitudComentario(comentario);

            Votacion votacion = ObtenerVotacionOFallar(idVotacion);
            Proyecto proyecto = ObtenerProyectoOFallar(idProyecto);
            Evento evento = ObtenerEventoDeVotacionOFallar(votacion);

            if (!ProyectoPerteneceAVotacion(proyecto, votacion))
                throw new ServiceException("El proyecto no pertenece a esta categoría");

            if (votacion.FechaFin <= DateTime.Now)
                throw new ServiceException("La votación está cerrada");

            if (!votacion.Estado)
                throw new ServiceException("La votación está pausada");

            int eventoId = evento.IdEvento;
            Rol? rolEvento = BuscarRolEnEvento(eventoId);
            if (rolEvento == null)
                throw new ServiceException("No tienes un rol asignado en este evento");

            if (rolEvento is Organizador || rolEvento is EncargadoVotacion)
                throw new ServiceException("El rol actual no puede votar");

            if (rolEvento is Competidor && !evento.PermiteCompetidoresVotar)
                throw new ServiceException("Los competidores no pueden votar en este evento");

            bool yaVoto = _votoRepository.GetWhere(v =>
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

            _votoRepository.Insert(voto);
            Commit();
        }

        // Obtener mis votos en una votación específica
        public List<int> GetMisVotos(int idVotacion)
        {
            RequireUsuarioLogueado();
            Votacion votacion = ObtenerVotacionOFallar(idVotacion);
            if (votacion.evento == null) return new List<int>();

            Rol? rol = BuscarRolEnEvento(votacion.evento.IdEvento);
            if (rol == null) return new List<int>();

            return _votoRepository.GetWhere(v => v.VotanteId == rol.Id && v.VotacionId == idVotacion)
                .Select(v => v.ProyectoId).ToList();
        }

        private Rol? BuscarRolEnEvento(int eventoId)
        {
            int uid = usuario!.Id;
            return
                (Rol?)_juradoRepository.GetWhere(r => r.UsuarioId == uid && r.EventoId == eventoId).FirstOrDefault() ??
                (Rol?)_publicoRepository.GetWhere(r => r.UsuarioId == uid && r.EventoId == eventoId).FirstOrDefault() ??
                (Rol?)_competidorRepository.GetWhere(r => r.UsuarioId == uid && r.EventoId == eventoId).FirstOrDefault() ??
                (Rol?)_organizadorRepository.GetWhere(r => r.UsuarioId == uid && r.EventoId == eventoId).FirstOrDefault() ??
                (Rol?)_encargadoRepository.GetWhere(r => r.UsuarioId == uid && r.EventoId == eventoId).FirstOrDefault();
        }

        public int CrearVotacion(CrearVotacionRequest request)
        {
            RequireUsuarioLogueado();

            DateTime fechaInicio = DateTime.Now;
            if (request.FechaFin <= fechaInicio)
                throw new ServiceException("La fecha de fin debe ser posterior a la fecha actual");

            ValidarPesosResultados(request.PesoJurado, request.PesoPublico);

            string nombre = string.IsNullOrWhiteSpace(request.Titulo) ? "Votacion" : request.Titulo.Trim();
            string descripcionNormalizada = request.Descripcion?.Trim() ?? string.Empty;

            Evento evento = new Evento
            {
                Nombre = nombre,
                Descripcion = descripcionNormalizada,
                FechaIni = fechaInicio,
                FechaFin = request.FechaFin,
                PermiteCompetidoresVotar = request.PermiteCompetidoresVotar,
                codigoEncargado = request.CodigoEncargado?.Trim() ?? string.Empty,
                codigoJurado = request.CodigoJurado?.Trim() ?? string.Empty,
                organizador = usuario!,
                OrganizadorId = usuario!.Id
            };
            _eventoRepository.Insert(evento);
            Commit();

            Organizador organizador = new Organizador(fechaInicio, 0)
            {
                usuario = usuario,
                evento = evento,
                UsuarioId = usuario!.Id,
                EventoId = evento.IdEvento
            };

            EncargadoVotacion encargado = new EncargadoVotacion(fechaInicio, 0)
            {
                usuario = usuario,
                evento = evento,
                UsuarioId = usuario!.Id,
                EventoId = evento.IdEvento
            };

            _organizadorRepository.Insert(organizador);
            _encargadoRepository.Insert(encargado);
            Commit();

            var categoriasNormalizadas = (request.Categorias ?? new List<string>())
                .Select(DescomponerCategoria)
                .Where(c => !string.IsNullOrWhiteSpace(c.Nombre))
                .GroupBy(c => c.Nombre, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();

            if (!categoriasNormalizadas.Any())
                categoriasNormalizadas.Add((nombre, null));

            var votacionesCreadas = new List<Votacion>();

            foreach (var categoria in categoriasNormalizadas)
            {
                bool esUnica = categoriasNormalizadas.Count == 1;
                string tituloVotacion = esUnica ? nombre : categoria.Nombre;
                string descripcionVisible = esUnica
                    ? descripcionNormalizada
                    : string.IsNullOrWhiteSpace(descripcionNormalizada)
                        ? $"Categoría: {categoria.Nombre}"
                        : $"{descripcionNormalizada} · Categoría: {categoria.Nombre}";

                Votacion votacion = new Votacion(fechaInicio, request.FechaFin, request.Activa, encargado)
                {
                    Titulo = tituloVotacion,
                    Descripcion = ConstruirDescripcionVotacion(descripcionVisible, categoria.CriteriosCodificados),
                    evento = evento,
                    EventoId = evento.IdEvento,
                    EncargadoId = encargado.Id,
                    PesoJurado = request.PesoJurado,
                    PesoPublico = request.PesoPublico
                };

                _votacionRepository.Insert(votacion);
                votacionesCreadas.Add(votacion);
            }

            Commit();

            return votacionesCreadas.First().Id;
        }

        public IEnumerable<Votacion> GetVotacionesByEvento(int idEvento)
        {
            return _votacionRepository.GetWhere(v => v.EventoId == idEvento).ToList();
        }

        public IEnumerable<Votacion> GetMisVotaciones()
        {
            RequireUsuarioLogueado();

            var encargadoIds = (usuario!.roles
                ?.OfType<EncargadoVotacion>()
                .Select(e => e.Id)
                .ToHashSet()) ?? new HashSet<int>();

            if (!encargadoIds.Any())
                return Enumerable.Empty<Votacion>();

            return _votacionRepository.GetAll()
                        .ToList()
                        .Where(v => v.Encargado != null && encargadoIds.Contains(v.Encargado.Id))
                        .ToList();
        }

        // Obtener todas las votaciones (para la vista general)
        public IEnumerable<Votacion> GetAllVotaciones() => _votacionRepository.GetAll().ToList();

        public Votacion GetVotacion(int idVotacion)
        {
            Votacion votacion = _votacionRepository.GetById(idVotacion);
            if (votacion == null)
                throw new ServiceException("La votación no existe");
            return votacion;
        }

        public Rol GetRolEnEvento(int idEvento)
        {
            RequireUsuarioLogueado();
            return BuscarRolEnEvento(idEvento);
        }

        // Helper para obtener el tipo de rol en string
        public string? GetTipoRolEnEvento(int idEvento)
        {
            RequireUsuarioLogueado();
            return BuscarRolEnEvento(idEvento)?.TipoRol;
        }

        // Helper para obtener tipo de rol sin logueo estricto (útil para listas)
        public string? GetTipoRolDeUsuario(int idUsuario, int idEvento)
        {
            var rol =
                (Rol?)_juradoRepository.GetWhere(r => r.UsuarioId == idUsuario && r.EventoId == idEvento).FirstOrDefault() ??
                (Rol?)_publicoRepository.GetWhere(r => r.UsuarioId == idUsuario && r.EventoId == idEvento).FirstOrDefault() ??
                (Rol?)_competidorRepository.GetWhere(r => r.UsuarioId == idUsuario && r.EventoId == idEvento).FirstOrDefault() ??
                (Rol?)_organizadorRepository.GetWhere(r => r.UsuarioId == idUsuario && r.EventoId == idEvento).FirstOrDefault() ??
                (Rol?)_encargadoRepository.GetWhere(r => r.UsuarioId == idUsuario && r.EventoId == idEvento).FirstOrDefault();

            return rol?.TipoRol;
        }

        public bool HasVotadoEnEvento(int idEvento)
        {
            RequireUsuarioLogueado();
            Rol? rol = BuscarRolEnEvento(idEvento);
            if (rol == null) return false;

            int rolId = rol.Id;
            var votacionIds = _votacionRepository.GetWhere(v => v.EventoId == idEvento)
                .Select(v => v.Id)
                .ToList();

            return votacionIds.Any(vid =>
                _votoRepository.GetWhere(v => v.VotanteId == rolId && v.VotacionId == vid).Any());
        }

        public void AsignarRolEnEvento(string tipoRol, int idEvento, string? codigoAcceso = null)
        {
            RequireUsuarioLogueado();

            tipoRol = (tipoRol ?? string.Empty).Trim().ToUpperInvariant();
            if (tipoRol != "PUBLICO" && tipoRol != "JURADO" && tipoRol != "COMPETIDOR" && tipoRol != "ENCARGADO")
                throw new ServiceException("Solo puedes unirte al evento como público, jurado, competidor o encargado");

            Evento evento = _eventoRepository.GetById(idEvento);
            if (evento == null)
                throw new ServiceException("El evento no existe");

            if (BuscarRolEnEvento(idEvento) != null)
                throw new ServiceException("Ya tienes un rol asignado en este evento");

            if (tipoRol == "JURADO")
            {
                var codigoEsperado = evento.codigoJurado?.Trim() ?? string.Empty;
                var codigoIngresado = codigoAcceso?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(codigoEsperado) || !string.Equals(codigoEsperado, codigoIngresado, StringComparison.Ordinal))
                    throw new ServiceException("Código de jurado incorrecto");
            }

            if (tipoRol == "ENCARGADO")
            {
                var codigoEsperado = evento.codigoEncargado?.Trim() ?? string.Empty;
                var codigoIngresado = codigoAcceso?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(codigoEsperado) || !string.Equals(codigoEsperado, codigoIngresado, StringComparison.Ordinal))
                    throw new ServiceException("Código de encargado incorrecto");
            }

            Rol nuevoRol = RolFactory.Create(tipoRol, DateTime.Now, 0);
            nuevoRol.usuario = usuario!;
            nuevoRol.evento = evento;
            nuevoRol.UsuarioId = usuario!.Id;
            nuevoRol.EventoId = evento.IdEvento;
            usuario!.roles ??= new List<Rol>();
            usuario.roles.Add(nuevoRol);

            _rolRepository.Insert(nuevoRol);
            Commit();
            rol = nuevoRol;
        }

        public void ModificarVotacion(int idVotacion, DateTime nuevaFechaFin, bool estado)
        {
            RequireUsuarioLogueado();
            Votacion votacion = ObtenerVotacionOFallar(idVotacion);
            if (!UsuarioPuedeGestionarVotacion(votacion))
                throw new ServiceException("No tienes permisos para modificar esta votación");
            if (nuevaFechaFin <= DateTime.Now)
                throw new ServiceException("La fecha de fin debe ser posterior a la fecha actual");
            votacion.FechaFin = nuevaFechaFin;
            votacion.Estado = estado;
            Commit();
        }

        public void CerrarVotacion(int idVotacion)
        {
            RequireUsuarioLogueado();
            Votacion votacion = ObtenerVotacionOFallar(idVotacion);
            if (!UsuarioPuedeGestionarVotacion(votacion))
                throw new ServiceException("No tienes permisos para cerrar esta votación");

            DateTime fechaCierre = votacion.FechaIni <= DateTime.Now
                ? votacion.FechaIni
                : DateTime.Now;

            votacion.Estado = false;
            votacion.FechaFin = fechaCierre;
            Commit();
        }

        // Métodos de Gestión de Proyectos
        public Proyecto CrearProyecto(int idVotacion, string nombre, string? descripcion, string? usernameCompetidor = null)
        {
            RequireUsuarioLogueado();
            Votacion votacion = ObtenerVotacionOFallar(idVotacion);
            Evento evento = ObtenerEventoDeVotacionOFallar(votacion);

            if (string.IsNullOrWhiteSpace(nombre))
                throw new ServiceException("El nombre del proyecto es obligatorio");

            if (string.IsNullOrWhiteSpace(descripcion))
                throw new ServiceException("La descripción del proyecto es obligatoria");

            Rol? rolEvento = BuscarRolEnEvento(evento.IdEvento);
            if (rolEvento == null)
                throw new ServiceException("No tienes un rol asignado en este evento");

            bool esOrganizador = rolEvento is Organizador;
            bool esCompetidor = rolEvento is Competidor;

            if (!esOrganizador && !esCompetidor)
                throw new ServiceException("No tienes permisos para crear proyectos en este evento");

            Usuario competidorUser;
            Competidor competidorRol;

            if (esOrganizador)
            {
                if (string.IsNullOrWhiteSpace(usernameCompetidor))
                    throw new ServiceException("Debes indicar el competidor del proyecto");

                competidorUser = _usuarioRepository.GetWhere(u => u.Username == usernameCompetidor).FirstOrDefault();
                if (competidorUser == null)
                    throw new ServiceException($"No existe ningún usuario con el nombre '{usernameCompetidor}'");

                competidorRol = _competidorRepository.GetWhere(c => c.UsuarioId == competidorUser.Id && c.EventoId == evento.IdEvento).FirstOrDefault();
                if (competidorRol == null)
                {
                    competidorRol = new Competidor(DateTime.Now, 0)
                    {
                        usuario = competidorUser,
                        evento = evento,
                        UsuarioId = competidorUser.Id,
                        EventoId = evento.IdEvento
                    };
                    _competidorRepository.Insert(competidorRol);
                    Commit();
                }
            }
            else
            {
                competidorUser = usuario!;
                competidorRol = (Competidor)rolEvento;
            }

            bool yaExisteProyectoEnCategoria = _proyectoRepository
                .GetWhere(p => p.EventoId == evento.IdEvento && p.CompetidorId == competidorRol.Id)
                .ToList()
                .Any(p => ProyectoPerteneceAVotacion(p, votacion));

            if (yaExisteProyectoEnCategoria)
                throw new ServiceException("Ya has presentado un proyecto en esta categoría");

            Proyecto proyecto = new Proyecto
            {
                Nombre = nombre.Trim(),
                Descripcion = descripcion.Trim(),
                competidor = competidorRol,
                evento = evento,
                CompetidorId = competidorRol.Id,
                EventoId = evento.IdEvento,
                ParticipantesAdicionales = ConstruirParticipantesAdicionales(ObtenerCategoriaDeVotacion(votacion), Enumerable.Empty<string>())
            };
            _proyectoRepository.Insert(proyecto);
            Commit();

            proyecto.competidor.usuario = competidorUser;
            return proyecto;
        }

        public void ModificarProyecto(int idProyecto, string nombre, string? descripcion, List<string>? participantesAdicionales)
        {
            RequireUsuarioLogueado();
            Proyecto proyecto = _proyectoRepository.GetById(idProyecto);
            if (proyecto == null) throw new ServiceException("Proyecto no encontrado");

            Evento evento = proyecto.evento;
            if (evento == null || !UsuarioEsOrganizadorEnEvento(evento.IdEvento))
                throw new ServiceException("No eres el organizador de este evento");

            proyecto.Nombre = nombre.Trim();
            proyecto.Descripcion = descripcion?.Trim() ?? string.Empty;

            if (participantesAdicionales != null)
            {
                string leadUsername = proyecto.competidor?.usuario?.Username ?? "";
                var adicionales = participantesAdicionales.Select(u => u.Trim())
                    .Where(u => !string.IsNullOrEmpty(u) && u != leadUsername)
                    .Distinct()
                    .ToList();
                proyecto.ParticipantesAdicionales = ConstruirParticipantesAdicionales(ObtenerCategoriaDeProyecto(proyecto), adicionales);
            }
            Commit();
        }

        public void EliminarProyecto(int idProyecto)
        {
            RequireUsuarioLogueado();
            Proyecto proyecto = _proyectoRepository.GetById(idProyecto);
            if (proyecto == null) throw new ServiceException("Proyecto no encontrado");

            Evento evento = proyecto.evento;
            if (evento == null || !UsuarioEsOrganizadorEnEvento(evento.IdEvento))
                throw new ServiceException("No eres el organizador de este evento");

            // Eliminar votos del proyecto
            var votos = _votoRepository.GetWhere(v => v.ProyectoId == idProyecto).ToList();
            foreach (var voto in votos) _votoRepository.Delete(voto);

            _proyectoRepository.Delete(proyecto);
            Commit();
        }
        public void TogglePausarVotacion(int idVotacion)
        {
            RequireUsuarioLogueado();
            Votacion votacion = ObtenerVotacionOFallar(idVotacion);

            if (!UsuarioPuedeGestionarVotacion(votacion))
                throw new ServiceException("No tienes permisos para gestionar esta votación");

            if (votacion.FechaFin <= DateTime.Now && !votacion.Estado)
                throw new ServiceException("La votación está finalizada y no puede reanudarse");

            votacion.Estado = !votacion.Estado;
            Commit();
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

        private void ValidarLongitudComentario(string? comentario)
        {
            if (comentario != null && comentario.Length > MaxLongitudComentario)
                throw new ServiceException($"El comentario no puede superar los {MaxLongitudComentario} caracteres");
        }

        private void ValidarPesosResultados(int pesoJurado, int pesoPublico)
        {
            if (pesoJurado < 0 || pesoJurado > 100 || pesoPublico < 0 || pesoPublico > 100)
                throw new ServiceException("Los pesos de jurado y público deben estar entre 0 y 100");

            if (pesoJurado + pesoPublico != 100)
                throw new ServiceException("Los pesos de jurado y público deben sumar 100");
        }

        private Votacion ObtenerVotacionOFallar(int idVotacion)
        {
            Votacion votacion = _votacionRepository.GetById(idVotacion);
            if (votacion == null)
                throw new ServiceException("La votación no existe");
            return votacion;
        }

        private Proyecto ObtenerProyectoOFallar(int idProyecto)
        {
            Proyecto proyecto = _proyectoRepository.GetById(idProyecto);
            if (proyecto == null)
                throw new ServiceException("El proyecto no existe");
            return proyecto;
        }

        public void EliminarEvento(int idVotacion)
        {
            RequireUsuarioLogueado();
            Votacion votacion = ObtenerVotacionOFallar(idVotacion);

            bool esOrganizador = UsuarioEsOrganizadorEnEvento(votacion.EventoId);
            bool esEncargado = _encargadoRepository.GetWhere(e => e.UsuarioId == usuario!.Id && e.EventoId == votacion.EventoId).Any();

            if (!esOrganizador && !esEncargado)
                throw new ServiceException("No tienes permisos para eliminar este evento");

            Evento evento = _eventoRepository.GetById(votacion.EventoId);
            if (evento == null)
                throw new ServiceException("El evento no existe");

            _eventoRepository.Delete(evento);
            Commit();
        }

        private Evento ObtenerEventoDeVotacionOFallar(Votacion votacion)
        {
            if (votacion.evento == null)
                throw new ServiceException("La votación no está asociada a ningún evento");
            return votacion.evento;
        }

        private bool UsuarioPuedeGestionarVotacion(Votacion votacion)
            => UsuarioEsOrganizadorEnEvento(votacion.EventoId) || UsuarioEsEncargadoEnEvento(votacion.EventoId);

        private bool UsuarioEsEncargadoEnEvento(int eventoId)
            => _encargadoRepository.GetWhere(r => r.UsuarioId == usuario!.Id && r.EventoId == eventoId).Any();

        private bool UsuarioEsOrganizadorEnEvento(int eventoId)
            => _organizadorRepository.GetWhere(r => r.UsuarioId == usuario!.Id && r.EventoId == eventoId).Any();

        private static string ObtenerCategoriaDeVotacion(Votacion votacion)
            => votacion.Titulo?.Trim() ?? string.Empty;

        private static string ObtenerCategoriaDeProyecto(Proyecto proyecto)
        {
            if (string.IsNullOrWhiteSpace(proyecto.ParticipantesAdicionales))
                return string.Empty;

            return proyecto.ParticipantesAdicionales
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .FirstOrDefault(p => p.StartsWith(PrefijoCategoriaProyecto, StringComparison.OrdinalIgnoreCase))?
                .Substring(PrefijoCategoriaProyecto.Length)
                .Trim()
                ?? string.Empty;
        }

        private static IEnumerable<string> ObtenerParticipantesProyecto(string? participantesAdicionales)
        {
            if (string.IsNullOrWhiteSpace(participantesAdicionales))
                return Enumerable.Empty<string>();

            return participantesAdicionales
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrEmpty(p) && !p.StartsWith(PrefijoCategoriaProyecto, StringComparison.OrdinalIgnoreCase));
        }

        private static string ConstruirParticipantesAdicionales(string categoria, IEnumerable<string> participantes)
        {
            var valores = new List<string>();

            if (!string.IsNullOrWhiteSpace(categoria))
                valores.Add($"{PrefijoCategoriaProyecto}{categoria.Trim()}");

            valores.AddRange(participantes
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrEmpty(p) && !p.StartsWith(PrefijoCategoriaProyecto, StringComparison.OrdinalIgnoreCase)));

            return string.Join(",", valores.Distinct(StringComparer.OrdinalIgnoreCase));
        }

        private static bool ProyectoPerteneceAVotacion(Proyecto proyecto, Votacion votacion)
        {
            var categoriaProyecto = ObtenerCategoriaDeProyecto(proyecto);
            if (string.IsNullOrWhiteSpace(categoriaProyecto))
                return true;

            return string.Equals(categoriaProyecto, ObtenerCategoriaDeVotacion(votacion), StringComparison.OrdinalIgnoreCase);
        }

        private static (string Nombre, string? CriteriosCodificados) DescomponerCategoria(string categoria)
        {
            if (string.IsNullOrWhiteSpace(categoria))
                return (string.Empty, null);

            var valor = categoria.Trim();
            int idx = valor.IndexOf(PrefijoCriteriosCategoria, StringComparison.Ordinal);
            if (idx < 0)
                return (valor, null);

            string nombre = valor[..idx].Trim();
            string metadata = valor[(idx + PrefijoCriteriosCategoria.Length)..].Trim();
            return (nombre, string.IsNullOrWhiteSpace(metadata) ? null : metadata);
        }

        private static string ConstruirDescripcionVotacion(string descripcionVisible, string? criteriosCodificados)
        {
            string visible = descripcionVisible?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(criteriosCodificados))
                return visible;

            return string.IsNullOrWhiteSpace(visible)
                ? $"{PrefijoCriteriosDescripcion}{criteriosCodificados}"
                : $"{visible}{PrefijoCriteriosDescripcion}{criteriosCodificados}";
        }

        // ── Historial de eventos del usuario ────────────────────────────

        public HistorialEventosResultado GetHistorialDelUsuario()
        {
            RequireUsuarioLogueado();
            int uid = usuario!.Id;

            var roles = _rolRepository.GetWhere(r => r.UsuarioId == uid).ToList();
            var rolIdsUsuario = roles.Select(r => r.Id).ToHashSet();
            var eventoIds = roles.Select(r => r.EventoId).Distinct().ToList();

            if (!eventoIds.Any())
                return new HistorialEventosResultado();

            var eventos = _eventoRepository.GetWhere(e => eventoIds.Contains(e.IdEvento))
                .OrderByDescending(e => e.FechaIni)
                .ToList();

            var votosUsuario = _votoRepository.GetWhere(v => rolIdsUsuario.Contains(v.VotanteId)).ToList();
            int totalVotosEmitidos = votosUsuario.Count;

            var items = new List<HistorialEventoItem>();
            foreach (var evento in eventos)
            {
                var rolEnEvento = roles.FirstOrDefault(r => r.EventoId == evento.IdEvento);
                var item = new HistorialEventoItem
                {
                    Evento = evento,
                    TipoRol = rolEnEvento?.TipoRol
                };

                var proyectosEvento = _proyectoRepository.GetWhere(p => p.EventoId == evento.IdEvento).ToList();
                item.TotalProyectos = proyectosEvento.Count;

                Proyecto? proyectoDestacado = null;
                if (rolEnEvento is Competidor)
                {
                    proyectoDestacado = proyectosEvento.FirstOrDefault(p => p.CompetidorId == rolEnEvento.Id);
                }

                var votosEnEvento = votosUsuario
                    .Where(v => v.votacion?.EventoId == evento.IdEvento)
                    .ToList();
                item.Voto = votosEnEvento.Any();

                if (proyectoDestacado == null && item.Voto)
                {
                    int idMejorVotado = votosEnEvento.OrderByDescending(v => v.Valor).First().ProyectoId;
                    proyectoDestacado = proyectosEvento.FirstOrDefault(p => p.Id == idMejorVotado);
                }

                if (proyectoDestacado != null)
                {
                    item.ProyectoDestacado = proyectoDestacado;
                    item.PosicionProyecto = CalcularPosicionProyecto(proyectoDestacado, proyectosEvento);
                }

                items.Add(item);
            }

            return new HistorialEventosResultado
            {
                EventosParticipados = eventos.Count,
                VotosEmitidos = totalVotosEmitidos,
                Eventos = items
            };
        }

        private int? CalcularPosicionProyecto(Proyecto proyecto, List<Proyecto> proyectosEvento)
        {
            if (!proyectosEvento.Any()) return null;

            var votacionesEvento = _votacionRepository.GetWhere(v => v.EventoId == proyecto.EventoId).ToList();
            if (!votacionesEvento.Any()) return null;

            var votacionPrincipal = votacionesEvento.OrderByDescending(v => v.FechaFin).First();
            var votosVotacion = _votoRepository.GetWhere(v => v.VotacionId == votacionPrincipal.Id).ToList();

            var ranking = proyectosEvento
                .Select(p =>
                {
                    var votosP = votosVotacion.Where(v => v.ProyectoId == p.Id).ToList();
                    var votosJ = votosP.Where(ResultadosVotacionCalculator.EsVotoExperto).ToList();
                    var votosPop = votosP.Where(ResultadosVotacionCalculator.EsVotoPopular).ToList();
                    double? mediaJ = votosJ.Any() ? ResultadosVotacionCalculator.CalcularMedia(votosJ) : null;
                    double? mediaPop = votosPop.Any() ? ResultadosVotacionCalculator.CalcularMedia(votosPop) : null;
                    double media = ResultadosVotacionCalculator.CalcularPuntuacionAjustada(
                        mediaJ, mediaPop, votacionPrincipal.PesoJurado, votacionPrincipal.PesoPublico);
                    return new { p.Id, Media = media };
                })
                .OrderByDescending(x => x.Media)
                .Select((x, i) => new { x.Id, Posicion = i + 1 })
                .ToList();

            return ranking.FirstOrDefault(x => x.Id == proyecto.Id)?.Posicion;
        }

        // ── Reclamaciones ───────────────────────────────────────────────

        public Reclamacion CrearReclamacion(int idEvento, string descripcion)
        {
            RequireUsuarioLogueado();

            string descripcionLimpia = (descripcion ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(descripcionLimpia))
                throw new ServiceException("La descripción de la reclamación no puede estar vacía");
            if (descripcionLimpia.Length > 2000)
                throw new ServiceException("La descripción no puede superar los 2000 caracteres");

            Evento evento = _eventoRepository.GetById(idEvento);
            if (evento == null)
                throw new ServiceException("El evento no existe");

            Rol? rolEnEvento = BuscarRolEnEvento(idEvento);
            if (rolEnEvento == null)
                throw new ServiceException("Solo puedes reclamar en eventos en los que has participado");

            int uid = usuario!.Id;
            bool yaReclamado = _reclamacionRepository.GetWhere(r =>
                r.EventoId == idEvento &&
                r.UsuarioId == uid &&
                r.Estado == Reclamacion.EstadoPendiente).Any();
            if (yaReclamado)
                throw new ServiceException("Ya tienes una reclamación pendiente para este evento");

            Reclamacion reclamacion = new Reclamacion(idEvento, uid, descripcionLimpia)
            {
                evento = evento,
                usuario = usuario
            };
            _reclamacionRepository.Insert(reclamacion);
            Commit();
            return reclamacion;
        }

        public IEnumerable<Reclamacion> GetReclamacionesDelUsuario()
        {
            RequireUsuarioLogueado();
            int uid = usuario!.Id;
            return _reclamacionRepository.GetWhere(r => r.UsuarioId == uid)
                .OrderByDescending(r => r.FechaCreacion)
                .ToList();
        }

        public IEnumerable<Reclamacion> GetReclamacionesComoOrganizador()
        {
            RequireUsuarioLogueado();
            int uid = usuario!.Id;
            var eventoIds = _organizadorRepository.GetWhere(o => o.UsuarioId == uid)
                .Select(o => o.EventoId)
                .Distinct()
                .ToList();
            if (!eventoIds.Any()) return Enumerable.Empty<Reclamacion>();

            return _reclamacionRepository.GetWhere(r => eventoIds.Contains(r.EventoId))
                .OrderByDescending(r => r.FechaCreacion)
                .ToList();
        }

        public Reclamacion ResponderReclamacion(int idReclamacion, string estado, string? respuesta)
        {
            RequireUsuarioLogueado();
            Reclamacion reclamacion = _reclamacionRepository.GetById(idReclamacion);
            if (reclamacion == null)
                throw new ServiceException("La reclamación no existe");

            if (!UsuarioEsOrganizadorEnEvento(reclamacion.EventoId))
                throw new ServiceException("No eres el organizador de este evento");

            string estadoNormalizado = (estado ?? string.Empty).Trim().ToUpperInvariant();
            if (estadoNormalizado != Reclamacion.EstadoResuelta && estadoNormalizado != Reclamacion.EstadoRechazada)
                throw new ServiceException("Estado inválido. Debe ser RESUELTA o RECHAZADA");

            reclamacion.Estado = estadoNormalizado;
            reclamacion.RespuestaOrganizador = string.IsNullOrWhiteSpace(respuesta) ? null : respuesta!.Trim();
            reclamacion.FechaRespuesta = DateTime.Now;
            Commit();
            return reclamacion;
        }
    }
}
