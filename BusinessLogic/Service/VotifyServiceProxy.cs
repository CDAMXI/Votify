using System;
using System.Collections.Generic;
using System.Linq;
using Votify.Entities;

namespace Votify.BusinessLogic.Service
{
    /// <summary>
    /// Protection Proxy de VotifyService. Controla acceso a operaciones sensibles
    /// antes de delegar en el servicio real.
    /// </summary>
    public class VotifyServiceProxy : IVotifyService
    {
        private const string MensajeNoUsuarioLogueado = "No hay ningun usuario logueado";

        private readonly VotifyService _realService;
        private readonly VotifyRepositories _repositories;

        public VotifyServiceProxy(VotifyService realService, VotifyRepositories repositories)
        {
            _realService = realService;
            _repositories = repositories;
        }

        public void LogIn(string username, string password) => _realService.LogIn(username, password);
        public void LogOut() => _realService.LogOut();
        public void RestoreSession(string username) => _realService.RestoreSession(username);
        public void Registrar(RegistroUsuarioRequest request) => _realService.Registrar(request);

        public Usuario GetUsuarioActual() => _realService.GetUsuarioActual();
        public (string Username, string Email, string? FotoPerfil) GetPerfil() => _realService.GetPerfil();
        public void UpdateEmail(string nuevoEmail) => _realService.UpdateEmail(nuevoEmail);
        public void UpdatePassword(string passwordActual, string nuevaPassword) => _realService.UpdatePassword(passwordActual, nuevaPassword);
        public void UpdateFotoPerfil(string base64Foto) => _realService.UpdateFotoPerfil(base64Foto);

        public string GeneratePasswordResetToken(string email) => _realService.GeneratePasswordResetToken(email);
        public void ResetPassword(string token, string nuevaPassword) => _realService.ResetPassword(token, nuevaPassword);

        public void GuardarVoto(int idVotacion, int idProyecto, double puntuacion, string? comentario)
            => _realService.GuardarVoto(idVotacion, idProyecto, puntuacion, comentario);

        public void ModificarVoto(int idVotacion, int idProyecto, double puntuacion, string? comentario)
            => _realService.ModificarVoto(idVotacion, idProyecto, puntuacion, comentario);

        public bool HasVotadoEnEvento(int idEvento) => _realService.HasVotadoEnEvento(idEvento);
        public Voto? GetMiVotoEnProyecto(int idVotacion, int idProyecto) => _realService.GetMiVotoEnProyecto(idVotacion, idProyecto);
        public List<int> GetMisVotos(int idVotacion) => _realService.GetMisVotos(idVotacion);
        public void Commit() => _realService.Commit();

        public int CrearVotacion(CrearVotacionRequest request) => _realService.CrearVotacion(request);
        public Votacion GetVotacion(int idVotacion) => _realService.GetVotacion(idVotacion);
        public IEnumerable<Votacion> GetMisVotaciones() => _realService.GetMisVotaciones();
        public IEnumerable<Votacion> GetAllVotaciones() => _realService.GetAllVotaciones();
        public IEnumerable<Votacion> GetVotacionesByEvento(int idEvento) => _realService.GetVotacionesByEvento(idEvento);

        public void EliminarEvento(int idVotacion)
        {
            VerificarPuedeGestionarVotacion(idVotacion, "eliminar este evento");
            _realService.EliminarEvento(idVotacion);
        }

        public void ModificarVotacion(int idVotacion, DateTime nuevaFechaFin, string estado)
        {
            VerificarPuedeGestionarVotacion(idVotacion, "modificar esta votacion");
            _realService.ModificarVotacion(idVotacion, nuevaFechaFin, estado);
        }

        public void CerrarVotacion(int idVotacion)
        {
            VerificarPuedeGestionarVotacion(idVotacion, "cerrar esta votacion");
            _realService.CerrarVotacion(idVotacion);
        }

        public void TogglePausarVotacion(int idVotacion)
        {
            VerificarPuedeGestionarVotacion(idVotacion, "gestionar esta votacion");
            _realService.TogglePausarVotacion(idVotacion);
        }

        public Rol GetRolEnEvento(int idEvento) => _realService.GetRolEnEvento(idEvento);
        public void AsignarRolEnEvento(string tipoRol, int idEvento, string? codigoAcceso = null)
            => _realService.AsignarRolEnEvento(tipoRol, idEvento, codigoAcceso);
        public string? GetTipoRolEnEvento(int idEvento) => _realService.GetTipoRolEnEvento(idEvento);
        public string? GetTipoRolDeUsuario(int idUsuario, int idEvento) => _realService.GetTipoRolDeUsuario(idUsuario, idEvento);

        public Proyecto CrearProyecto(int idVotacion, string nombre, string? descripcion, string? usernameCompetidor = null)
            => _realService.CrearProyecto(idVotacion, nombre, descripcion, usernameCompetidor);

        public void ModificarProyecto(int idProyecto, string nombre, string? descripcion, List<string>? participantesAdicionales)
        {
            VerificarOrganizadorDeProyecto(idProyecto, "modificar este proyecto");
            _realService.ModificarProyecto(idProyecto, nombre, descripcion, participantesAdicionales);
        }

        public void EliminarProyecto(int idProyecto)
        {
            VerificarOrganizadorDeProyecto(idProyecto, "eliminar este proyecto");
            _realService.EliminarProyecto(idProyecto);
        }

        public HistorialEventosResultado GetHistorialDelUsuario() => _realService.GetHistorialDelUsuario();
        public Reclamacion CrearReclamacion(int idEvento, string descripcion) => _realService.CrearReclamacion(idEvento, descripcion);
        public IEnumerable<Reclamacion> GetReclamacionesDelUsuario() => _realService.GetReclamacionesDelUsuario();
        public IEnumerable<Reclamacion> GetReclamacionesComoOrganizador() => _realService.GetReclamacionesComoOrganizador();

        public Reclamacion ResponderReclamacion(int idReclamacion, string estado, string? respuesta)
        {
            VerificarPuedeResponderReclamacion(idReclamacion);
            return _realService.ResponderReclamacion(idReclamacion, estado, respuesta);
        }

        public IEnumerable<Notificacion> GetNotificacionesRecibidas() => _realService.GetNotificacionesRecibidas();
        public IEnumerable<Notificacion> GetNotificacionesEnviadas() => _realService.GetNotificacionesEnviadas();
        public int GetCantidadNotificacionesNoLeidas() => _realService.GetCantidadNotificacionesNoLeidas();
        public void MarcarNotificacionComoLeida(int idNotificacion) => _realService.MarcarNotificacionComoLeida(idNotificacion);

        public void EnviarMensajeOrganizadorEnEvento(int idEvento, string asunto, string mensaje)
        {
            VerificarOrganizadorDeEvento(idEvento, "enviar mensajes en este evento");
            _realService.EnviarMensajeOrganizadorEnEvento(idEvento, asunto, mensaje);
        }

        public void InvitarUsuarioEnEventoPorEmail(int idEvento, string email, string tipoRol)
        {
            VerificarOrganizadorDeEvento(idEvento, "invitar usuarios en este evento");
            _realService.InvitarUsuarioEnEventoPorEmail(idEvento, email, tipoRol);
        }

        private Usuario UsuarioActual()
        {
            Usuario? usuario = _realService.GetUsuarioActual();
            if (usuario == null)
                throw new ServiceException(MensajeNoUsuarioLogueado);

            return usuario;
        }

        private Votacion ObtenerVotacionOFallar(int idVotacion)
        {
            var votacion = _repositories.Votaciones.GetById(idVotacion);
            if (votacion == null)
                throw new ServiceException("La votacion no existe o ha sido eliminada");

            return votacion;
        }

        private void VerificarPuedeGestionarVotacion(int idVotacion, string accion)
        {
            var usuario = UsuarioActual();
            var votacion = ObtenerVotacionOFallar(idVotacion);

            if (!UsuarioPuedeGestionarVotacion(usuario.Id, votacion))
                throw new ServiceException($"No tienes permisos para {accion}");
        }

        private bool UsuarioPuedeGestionarVotacion(int usuarioId, Votacion votacion)
        {
            bool esEncargado = (votacion.Encargado != null && votacion.Encargado.UsuarioId == usuarioId)
                || _repositories.Encargados.GetWhere(r =>
                    r.UsuarioId == usuarioId &&
                    (r.Id == votacion.EncargadoId || r.EventoId == votacion.EventoId)).Any();

            bool esOrganizador = _repositories.Organizadores.GetWhere(r =>
                r.UsuarioId == usuarioId && r.EventoId == votacion.EventoId).Any();

            if (!esOrganizador)
            {
                var evento = _repositories.Eventos.GetById(votacion.EventoId);
                esOrganizador = evento != null && evento.OrganizadorId == usuarioId;
            }

            return esEncargado || esOrganizador;
        }

        private void VerificarOrganizadorDeProyecto(int idProyecto, string accion)
        {
            var usuario = UsuarioActual();
            var proyecto = _repositories.Proyectos.GetById(idProyecto);
            if (proyecto == null)
                throw new ServiceException("Proyecto no encontrado");

            if (!UsuarioEsOrganizadorDeEvento(usuario.Id, proyecto.EventoId))
                throw new ServiceException($"No tienes permisos para {accion}");
        }

        private void VerificarPuedeResponderReclamacion(int idReclamacion)
        {
            var usuario = UsuarioActual();
            var reclamacion = _repositories.Reclamaciones.GetById(idReclamacion);
            if (reclamacion == null)
                throw new ServiceException("La reclamacion no existe");

            if (!UsuarioEsOrganizadorDeEvento(usuario.Id, reclamacion.EventoId))
                throw new ServiceException("No tienes permisos para responder esta reclamacion");
        }

        private void VerificarOrganizadorDeEvento(int idEvento, string accion)
        {
            var usuario = UsuarioActual();
            if (!UsuarioEsOrganizadorDeEvento(usuario.Id, idEvento))
                throw new ServiceException($"No tienes permisos para {accion}");
        }

        private bool UsuarioEsOrganizadorDeEvento(int usuarioId, int idEvento)
        {
            var evento = _repositories.Eventos.GetById(idEvento);
            return (evento != null && evento.OrganizadorId == usuarioId)
                || _repositories.Organizadores.GetWhere(r => r.UsuarioId == usuarioId && r.EventoId == idEvento).Any();
        }
    }
}
