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
        private const string MarcadorDetalleVoto = "\n||VOTO_DETALLE||:";

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
        private readonly IDAL<Notificacion>? _notificacionRepository;

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
            _notificacionRepository = repositories.Notificaciones;
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
            string comentarioVisible = ObtenerComentarioVisible(comentario);
            ValidarLongitudComentario(comentarioVisible);

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

            Voto? votoExistente = _votoRepository.GetWhere(v =>
                v.VotanteId == rolEvento.Id &&
                v.VotacionId == votacion.Id &&
                v.ProyectoId == proyecto.Id
           ).FirstOrDefault();

            if (votoExistente != null)
            {
                votoExistente.Valor = puntuacion;
                votoExistente.Comentario = comentario ?? string.Empty;
                votoExistente.Fecha = DateTime.Now;
                Commit();
                return;
            }

            bool yaVoto = false;

            if (yaVoto)
                throw new ServiceException("Ya has votado en este proyecto para esta votación");

            Voto voto = new Voto(puntuacion, comentarioVisible, DateTime.Now)
            {
                votacion = votacion,
                proyecto = proyecto,
                votante = rolEvento
            };
            voto.Comentario = comentario ?? string.Empty;
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
        public void ModificarVoto(int idVotacion, int idProyecto, double puntuacion, string? comentario)
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

            Rol? rolEvento = BuscarRolEnEvento(evento.IdEvento);
            if (rolEvento == null)
                throw new ServiceException("No tienes un rol asignado en este evento");

            if (rolEvento is Organizador || rolEvento is EncargadoVotacion)
                throw new ServiceException("El rol actual no puede votar");

            if (rolEvento is Competidor && !evento.PermiteCompetidoresVotar)
                throw new ServiceException("Los competidores no pueden votar en este evento");

            Voto? votoExistente = _votoRepository.GetWhere(v =>
                v.VotanteId == rolEvento.Id &&
                v.VotacionId == votacion.Id &&
                v.ProyectoId == proyecto.Id
            ).FirstOrDefault();

            if (votoExistente == null)
                throw new ServiceException("No has votado en este proyecto todavía. Usa la opción de votar primero.");

            if (puntuacion < 0 || puntuacion > 10)
                throw new ServiceException("El valor debe estar entre 0 y 10");

            votoExistente.Valor = puntuacion;
            votoExistente.Comentario = comentario ?? string.Empty;
            votoExistente.Fecha = DateTime.Now;

            Commit();
        }

        public Voto? GetMiVotoEnProyecto(int idVotacion, int idProyecto)
        {
            RequireUsuarioLogueado();
            Votacion votacion = ObtenerVotacionOFallar(idVotacion);
            if (votacion.evento == null) return null;

            Rol? rol = BuscarRolEnEvento(votacion.evento.IdEvento);
            if (rol == null) return null;

            return _votoRepository.GetWhere(v =>
                v.VotanteId == rol.Id &&
                v.VotacionId == idVotacion &&
                v.ProyectoId == idProyecto
            ).FirstOrDefault();
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

            CrearInvitacionesPorCorreo(evento, request, nombre);

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

        public IEnumerable<Notificacion> GetNotificacionesRecibidas()
        {
            RequireUsuarioLogueado();
            if (_notificacionRepository == null)
                return Enumerable.Empty<Notificacion>();

            return _notificacionRepository.GetWhere(n => n.DestinatarioId == usuario!.Id)
                .OrderByDescending(n => n.FechaCreacion)
                .ToList();
        }

        public IEnumerable<Notificacion> GetNotificacionesEnviadas()
        {
            RequireUsuarioLogueado();
            if (_notificacionRepository == null)
                return Enumerable.Empty<Notificacion>();

            return _notificacionRepository.GetWhere(n => n.RemitenteId == usuario!.Id)
                .OrderByDescending(n => n.FechaCreacion)
                .ToList();
        }

        public int GetCantidadNotificacionesNoLeidas()
        {
            RequireUsuarioLogueado();
            if (_notificacionRepository == null)
                return 0;

            return _notificacionRepository.GetWhere(n => n.DestinatarioId == usuario!.Id && !n.Leida).Count();
        }

        public void MarcarNotificacionComoLeida(int idNotificacion)
        {
            RequireUsuarioLogueado();
            if (_notificacionRepository == null)
                throw new ServiceException("No hay notificaciones disponibles");

            var notificacion = _notificacionRepository.GetById(idNotificacion);
            if (notificacion == null)
                throw new ServiceException("Notificación no encontrada");

            if (notificacion.DestinatarioId != usuario!.Id)
                throw new ServiceException("No puedes modificar esta notificación");

            if (!notificacion.Leida)
            {
                notificacion.Leida = true;
                Commit();
            }
        }

        public void EnviarMensajeOrganizadorEnEvento(int idEvento, string asunto, string mensaje)
        {
            RequireUsuarioLogueado();
            if (_notificacionRepository == null)
                throw new ServiceException("No hay notificaciones disponibles");

            Evento evento = _eventoRepository.GetById(idEvento);
            if (evento == null)
                throw new ServiceException("El evento no existe");

            if (!UsuarioEsOrganizadorEnEvento(idEvento))
                throw new ServiceException("Solo el organizador puede enviar mensajes desde aquí");

            asunto = (asunto ?? string.Empty).Trim();
            mensaje = (mensaje ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(asunto))
                throw new ServiceException("El asunto no puede estar vacío");

            if (string.IsNullOrWhiteSpace(mensaje))
                throw new ServiceException("El mensaje no puede estar vacío");

            var destinatarioIds = _juradoRepository.GetWhere(r => r.EventoId == idEvento).Select(r => r.UsuarioId)
                .Concat(_publicoRepository.GetWhere(r => r.EventoId == idEvento).Select(r => r.UsuarioId))
                .Concat(_competidorRepository.GetWhere(r => r.EventoId == idEvento).Select(r => r.UsuarioId))
                .Concat(_encargadoRepository.GetWhere(r => r.EventoId == idEvento).Select(r => r.UsuarioId))
                .Concat(_organizadorRepository.GetWhere(r => r.EventoId == idEvento).Select(r => r.UsuarioId))
                .Where(uid => uid != usuario!.Id)
                .Distinct()
                .ToList();

            if (!destinatarioIds.Any())
                return;

            foreach (var destinatarioId in destinatarioIds)
            {
                var destinatario = _usuarioRepository.GetById(destinatarioId);
                if (destinatario == null)
                    continue;

                _notificacionRepository.Insert(new Notificacion
                {
                    RemitenteId = usuario!.Id,
                    DestinatarioId = destinatario.Id,
                    Asunto = asunto,
                    Mensaje = mensaje,
                    FechaCreacion = DateTime.Now,
                    Leida = false,
                    remitente = usuario,
                    destinatario = destinatario
                });
            }

            Commit();
        }

        public void InvitarUsuarioEnEventoPorEmail(int idEvento, string email, string tipoRol)
        {
            RequireUsuarioLogueado();
            if (_notificacionRepository == null)
                throw new ServiceException("No hay notificaciones disponibles");

            Evento evento = _eventoRepository.GetById(idEvento);
            if (evento == null)
                throw new ServiceException("El evento no existe");

            if (!UsuarioEsOrganizadorEnEvento(idEvento))
                throw new ServiceException("Solo el organizador puede invitar usuarios desde aquí");

            email = (email ?? string.Empty).Trim();
            tipoRol = (tipoRol ?? string.Empty).Trim().ToUpperInvariant();

            if (string.IsNullOrWhiteSpace(email))
                throw new ServiceException("El correo no puede estar vacío");

            if (tipoRol != "JURADO" && tipoRol != "ENCARGADO")
                throw new ServiceException("Tipo de invitación no válido");

            var destinatario = _usuarioRepository.GetWhere(u => u.Email.ToLower() == email.ToLower()).FirstOrDefault();
            if (destinatario == null)
                throw new ServiceException("No existe ningún usuario con ese correo");

            string codigoAcceso = tipoRol == "JURADO"
                ? (evento.codigoJurado?.Trim() ?? string.Empty)
                : (evento.codigoEncargado?.Trim() ?? string.Empty);

            if (string.IsNullOrWhiteSpace(codigoAcceso))
                throw new ServiceException(tipoRol == "JURADO"
                    ? "El evento no tiene código de jurado configurado"
                    : "El evento no tiene código de encargado configurado");

            string rolTexto = tipoRol == "JURADO" ? "jurado" : "encargado";
            string asunto = $"Invitación como {rolTexto} en {evento.Nombre}";
            string mensaje = $"De: {usuario.Username}, {usuario.Email}\n\n" +
                             $"Estimado/a usuario/a,\n\n" +
                             $"Le invitamos a participar en el evento {evento.Nombre} como {rolTexto}.\n" +
                             $"El código de acceso asociado a este rol es: {codigoAcceso}\n\n" +
                             $"Esperamos contar con su participación.\n\n" +
                             $"Reciba un cordial saludo.";

            _notificacionRepository.Insert(new Notificacion
            {
                RemitenteId = usuario.Id,
                DestinatarioId = destinatario.Id,
                Asunto = asunto,
                Mensaje = mensaje,
                FechaCreacion = DateTime.Now,
                Leida = false,
                remitente = usuario,
                destinatario = destinatario
            });

            Commit();
        }

        private void CrearInvitacionesPorCorreo(Evento evento, CrearVotacionRequest request, string nombreEvento)
        {
            if (_notificacionRepository == null || usuario == null)
                return;

            CrearInvitacionesParaRol(request.CorreosJurados, "jurado", request.CodigoJurado?.Trim() ?? string.Empty, nombreEvento);
            CrearInvitacionesParaRol(request.CorreosEncargados, "encargado", request.CodigoEncargado?.Trim() ?? string.Empty, nombreEvento);
            Commit();
        }

        private void CrearInvitacionesParaRol(IEnumerable<string>? correos, string rolDestino, string codigo, string nombreEvento)
        {
            if (_notificacionRepository == null || usuario == null || string.IsNullOrWhiteSpace(codigo) || correos == null)
                return;

            var remitenteNombre = usuario.Username?.Trim() ?? "Organización del evento";
            var remitenteCorreo = usuario.Email?.Trim() ?? "sin-correo";
            var asunto = $"Invitación a {nombreEvento}";

            foreach (var correo in correos
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(c => c.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var destinatario = _usuarioRepository.GetWhere(u => u.Email == correo).FirstOrDefault();
                if (destinatario == null)
                    continue;

                var mensaje = $"De: {remitenteNombre}, {remitenteCorreo}\n\n" +
                              $"Estimado/a usuario/a,\n\n" +
                              $"Nos complace invitarle a participar en el evento {nombreEvento} con el rol de {rolDestino}.\n" +
                              $"El código de acceso asociado a este rol es: {codigo}\n\n" +
                              $"Esperamos contar con su participación.\n\n" +
                              $"Reciba un cordial saludo.";

                _notificacionRepository.Insert(new Notificacion
                {
                    RemitenteId = usuario.Id,
                    DestinatarioId = destinatario.Id,
                    Asunto = asunto,
                    Mensaje = mensaje,
                    FechaCreacion = DateTime.Now,
                    Leida = false,
                    remitente = usuario,
                    destinatario = destinatario
                });
            }
        }
        // ── Helpers privados ────────────────────────────────────────────────
        private void CargarRolDeUsuario(Usuario usuario)
        {
            int uid = usuario.Id;
            rol =
                (Rol?)_juradoRepository.GetWhere(r => r.UsuarioId == uid).FirstOrDefault() ??
                (Rol?)_publicoRepository.GetWhere(r => r.UsuarioId == uid).FirstOrDefault() ??
                (Rol?)_competidorRepository.GetWhere(r => r.UsuarioId == uid).FirstOrDefault() ??
                (Rol?)_organizadorRepository.GetWhere(r => r.UsuarioId == uid).FirstOrDefault() ??
                (Rol?)_encargadoRepository.GetWhere(r => r.UsuarioId == uid).FirstOrDefault();
        }

        private void RequireUsuarioLogueado()
        {
            if (usuario == null)
                throw new ServiceException(MensajeNoUsuarioLogueado);
        }

        private void ValidarLongitudComentario(string? comentario)
        {
            if (!string.IsNullOrEmpty(comentario) && comentario.Length > MaxLongitudComentario)
                throw new ServiceException($"El comentario no puede tener más de {MaxLongitudComentario} caracteres");
        }

        public static string ObtenerComentarioVisible(string? comentario)
        {
            if (string.IsNullOrEmpty(comentario))
                return string.Empty;

            int idx = comentario.IndexOf(MarcadorDetalleVoto, StringComparison.Ordinal);
            return idx < 0 ? comentario : comentario[..idx].TrimEnd();
        }

        public static Dictionary<string, double> ObtenerDetalleCriterios(string? comentario)
        {
            if (string.IsNullOrWhiteSpace(comentario))
                return new Dictionary<string, double>();

            int idx = comentario.IndexOf(MarcadorDetalleVoto, StringComparison.Ordinal);
            if (idx < 0)
                return new Dictionary<string, double>();

            string encoded = comentario[(idx + MarcadorDetalleVoto.Length)..].Trim();
            if (string.IsNullOrWhiteSpace(encoded))
                return new Dictionary<string, double>();

            try
            {
                string json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
                return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, double>>(json)
                    ?? new Dictionary<string, double>();
            }
            catch
            {
                return new Dictionary<string, double>();
            }
        }

        public static string ConstruirComentarioConDetalle(string? comentario, Dictionary<string, double>? puntuacionesCriterios)
        {
            string visible = comentario?.Trim() ?? string.Empty;
            if (puntuacionesCriterios == null || !puntuacionesCriterios.Any())
                return visible;

            string json = System.Text.Json.JsonSerializer.Serialize(puntuacionesCriterios);
            string encoded = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json));
            return $"{visible}{MarcadorDetalleVoto}{encoded}";
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
            Votacion? votacion = _votacionRepository.GetById(idVotacion);
            if (votacion == null)
                throw new ServiceException("La votación no existe o ha sido eliminada");
            return votacion;
        }

        private Proyecto ObtenerProyectoOFallar(int idProyecto)
        {
            Proyecto? proyecto = _proyectoRepository.GetById(idProyecto);
            if (proyecto == null)
                throw new ServiceException("El proyecto no existe o ha sido eliminado");
            return proyecto;
        }

        private Evento ObtenerEventoDeVotacionOFallar(Votacion votacion)
        {
            Evento? evento = _eventoRepository.GetById(votacion.EventoId);
            if (evento == null)
                throw new ServiceException("El evento asociado a esta votación no existe");
            return evento;
        }

        private bool UsuarioPuedeGestionarVotacion(Votacion votacion)
        {
            if (votacion.Encargado == null) return false;
            int uid = usuario!.Id;
            return votacion.Encargado.UsuarioId == uid;
        }

        private bool ProyectoPerteneceAVotacion(Proyecto proyecto, Votacion votacion)
        {
            string prefijoCategoria = PrefijoCategoriaProyecto;
            string categoriaProyecto = (prefijoCategoria + proyecto.Id).ToLowerInvariant();

            return votacion.Descripcion?.Contains(categoriaProyecto) == true;
        }

        private (string Nombre, string? CriteriosCodificados) DescomponerCategoria(string categoria)
        {
            if (!categoria.StartsWith(PrefijoCategoriaProyecto, StringComparison.OrdinalIgnoreCase))
                return (categoria, null);

            string nombre = categoria.Substring(PrefijoCategoriaProyecto.Length);
            return (nombre, string.Empty);
        }

        private string ConstruirDescripcionVotacion(string descripcionBase, string? criteriosCodificados)
        {
            if (string.IsNullOrWhiteSpace(criteriosCodificados))
                return descripcionBase.Trim();

            string descripcionCriterios = $"{PrefijoCriteriosDescripcion}{criteriosCodificados}";
            return (descripcionBase + "\n" + descripcionCriterios).Trim();
        }

        private string ObtenerCategoriaDeVotacion(Votacion votacion)
        {
            string? descripcion = votacion.Descripcion;
            if (string.IsNullOrWhiteSpace(descripcion))
                return string.Empty;

            string prefijo = PrefijoCategoriaProyecto;
            int inicio = descripcion.IndexOf(prefijo, StringComparison.OrdinalIgnoreCase);
            if (inicio < 0) return string.Empty;

            int fin = descripcion.IndexOf('\n', inicio);
            if (fin < 0) fin = descripcion.Length;

            string lineaCategoria = descripcion.Substring(inicio, fin - inicio);
            return lineaCategoria.Substring(prefijo.Length).Trim();
        }

        private string ObtenerCategoriaDeProyecto(Proyecto proyecto)
        {
            string? descripcion = proyecto.evento?.Descripcion;
            if (string.IsNullOrWhiteSpace(descripcion))
                return string.Empty;

            string prefijo = PrefijoCategoriaProyecto;
            int inicio = descripcion.IndexOf(prefijo, StringComparison.OrdinalIgnoreCase);
            if (inicio < 0) return string.Empty;

            int fin = descripcion.IndexOf('\n', inicio);
            if (fin < 0) fin = descripcion.Length;

            string lineaCategoria = descripcion.Substring(inicio, fin - inicio);
            return lineaCategoria.Substring(prefijo.Length).Trim();
        }

        private string ConstruirParticipantesAdicionales(string categoria, IEnumerable<string> usernames)
        {
            var valores = new List<string>();

            if (!string.IsNullOrWhiteSpace(categoria))
                valores.Add($"{PrefijoCategoriaProyecto}{categoria.Trim()}");

            valores.AddRange(usernames
                .Select(u => u.Trim())
                .Where(u => !string.IsNullOrWhiteSpace(u) && !u.StartsWith(PrefijoCategoriaProyecto, StringComparison.OrdinalIgnoreCase)));

            return string.Join(",", valores.Distinct(StringComparer.OrdinalIgnoreCase));
        }

        private bool UsuarioEsOrganizadorEnEvento(int idEvento)
            => _organizadorRepository.GetWhere(r => r.UsuarioId == usuario!.Id && r.EventoId == idEvento).Any();

        private bool UsuarioEsEncargadoEnEvento(int idEvento)
            => _encargadoRepository.GetWhere(r => r.UsuarioId == usuario!.Id && r.EventoId == idEvento).Any();

        public void EliminarEvento(int idVotacion)
        {
            RequireUsuarioLogueado();
            Votacion votacion = ObtenerVotacionOFallar(idVotacion);

            bool esOrganizador = UsuarioEsOrganizadorEnEvento(votacion.EventoId);
            bool esEncargado = UsuarioEsEncargadoEnEvento(votacion.EventoId);

            if (!esOrganizador && !esEncargado)
                throw new ServiceException("No tienes permisos para eliminar este evento");

            Evento evento = _eventoRepository.GetById(votacion.EventoId);
            if (evento == null)
                throw new ServiceException("El evento no existe");

            _eventoRepository.Delete(evento);
            Commit();
        }

        public HistorialEventosResultado GetHistorialDelUsuario()
        {
            RequireUsuarioLogueado();
            int uid = usuario!.Id;

            var rolesUsuario = _rolRepository.GetWhere(r => r.UsuarioId == uid).ToList();
            var eventoIds = rolesUsuario.Select(r => r.EventoId).Distinct().ToList();
            var votosUsuario = _votoRepository.GetWhere(v => rolesUsuario.Select(r => r.Id).Contains(v.VotanteId)).ToList();
            var eventos = _eventoRepository.GetWhere(e => eventoIds.Contains(e.IdEvento)).ToList();
            var proyectos = _proyectoRepository.GetWhere(p => eventoIds.Contains(p.EventoId)).ToList();

            var items = eventos.Select(e => new HistorialEventoItem
            {
                Evento = e,
                TipoRol = rolesUsuario.FirstOrDefault(r => r.EventoId == e.IdEvento)?.TipoRol,
                Voto = votosUsuario.Any(v => v.votacion != null ? v.votacion.EventoId == e.IdEvento : false),
                ProyectoDestacado = proyectos.FirstOrDefault(p => p.EventoId == e.IdEvento && p.competidor?.UsuarioId == uid),
                PosicionProyecto = null,
                TotalProyectos = proyectos.Count(p => p.EventoId == e.IdEvento)
            }).ToList();

            return new HistorialEventosResultado
            {
                EventosParticipados = items.Count,
                VotosEmitidos = votosUsuario.Count,
                Eventos = items
            };
        }

        public Reclamacion CrearReclamacion(int idEvento, string descripcion)
        {
            RequireUsuarioLogueado();
            var evento = _eventoRepository.GetById(idEvento);
            if (evento == null)
                throw new ServiceException("El evento no existe");

            var reclamacion = new Reclamacion
            {
                EventoId = idEvento,
                UsuarioId = usuario!.Id,
                Descripcion = descripcion?.Trim() ?? string.Empty,
                FechaCreacion = DateTime.Now,
                Estado = Reclamacion.EstadoPendiente,
                evento = evento,
                usuario = usuario!
            };

            _reclamacionRepository.Insert(reclamacion);
            Commit();
            return reclamacion;
        }

        public IEnumerable<Reclamacion> GetReclamacionesDelUsuario()
        {
            RequireUsuarioLogueado();
            return _reclamacionRepository.GetWhere(r => r.UsuarioId == usuario!.Id)
                .OrderByDescending(r => r.FechaCreacion)
                .ToList();
        }

        public IEnumerable<Reclamacion> GetReclamacionesComoOrganizador()
        {
            RequireUsuarioLogueado();
            var eventosOrganizados = _eventoRepository.GetWhere(e => e.OrganizadorId == usuario!.Id)
                .Select(e => e.IdEvento)
                .ToList();

            return _reclamacionRepository.GetWhere(r => eventosOrganizados.Contains(r.EventoId))
                .OrderByDescending(r => r.FechaCreacion)
                .ToList();
        }

        public Reclamacion ResponderReclamacion(int idReclamacion, string estado, string? respuesta)
        {
            RequireUsuarioLogueado();
            var reclamacion = _reclamacionRepository.GetById(idReclamacion);
            if (reclamacion == null)
                throw new ServiceException("La reclamación no existe");

            var evento = _eventoRepository.GetById(reclamacion.EventoId);
            if (evento == null || evento.OrganizadorId != usuario!.Id)
                throw new ServiceException("No tienes permisos para responder esta reclamación");

            reclamacion.Estado = estado?.Trim().ToUpperInvariant() ?? Reclamacion.EstadoPendiente;
            reclamacion.RespuestaOrganizador = respuesta?.Trim();
            reclamacion.FechaRespuesta = DateTime.Now;
            Commit();
            return reclamacion;
        }
    }
}
