using System;
using System.Collections.Generic;
using Votify.Entities;

namespace Votify.BusinessLogic.Service
{
    public class HistorialEventoItem
    {
        public Evento Evento { get; set; } = null!;
        public string? TipoRol { get; set; }
        public bool Voto { get; set; }
        public Proyecto? ProyectoDestacado { get; set; }
        public int? PosicionProyecto { get; set; }
        public int? TotalProyectos { get; set; }
    }

    public class HistorialEventosResultado
    {
        public int EventosParticipados { get; set; }
        public int VotosEmitidos { get; set; }
        public List<HistorialEventoItem> Eventos { get; set; } = new();
    }
    public class VotoUsuarioDetalle
    {
        public int ProyectoId { get; set; }
        public double Puntuacion { get; set; }
        public string Comentario { get; set; } = string.Empty;
        public Dictionary<string, double> PuntuacionesCriterios { get; set; } = new();
    }
    public interface IVotifyService
    {
        // Autenticación
        void LogIn(string username, string password);
        void LogOut();
        void RestoreSession(string username);
        void Registrar(RegistroUsuarioRequest request);

        // Perfil
        Usuario GetUsuarioActual();
        (string Username, string Email, string? FotoPerfil) GetPerfil();
        void UpdateEmail(string nuevoEmail);
        void UpdatePassword(string passwordActual, string nuevaPassword);
        void UpdateFotoPerfil(string base64Foto);

        // Recuperación de contraseña
        string GeneratePasswordResetToken(string email);
        void ResetPassword(string token, string nuevaPassword);

        // Votos
        void GuardarVoto(int idVotacion, int idProyecto, double puntuacion, string? comentario);
        void ModificarVoto(int idVotacion, int idProyecto, double puntuacion, string? comentario);
        bool HasVotadoEnEvento(int idEvento);
        Voto? GetMiVotoEnProyecto(int idVotacion, int idProyecto);
        List<int> GetMisVotos(int idVotacion);        
        void Commit();

        // Votaciones
        int CrearVotacion(CrearVotacionRequest request);
        Votacion GetVotacion(int idVotacion);
        IEnumerable<Votacion> GetMisVotaciones();
        IEnumerable<Votacion> GetAllVotaciones(); 
        IEnumerable<Votacion> GetVotacionesByEvento(int idEvento);
        void EliminarEvento(int idVotacion);
        void ModificarVotacion(int idVotacion, DateTime nuevaFechaFin, string estado);
        void CerrarVotacion(int idVotacion);
        void TogglePausarVotacion(int idVotacion);

        // Roles
        Rol GetRolEnEvento(int idEvento);
        void AsignarRolEnEvento(string tipoRol, int idEvento, string? codigoAcceso = null);
        string? GetTipoRolEnEvento(int idEvento); 
        string? GetTipoRolDeUsuario(int idUsuario, int idEvento); 

        // Proyectos 
        Proyecto CrearProyecto(int idVotacion, string nombre, string? descripcion, string? usernameCompetidor = null); 
        void ModificarProyecto(int idProyecto, string nombre, string? descripcion, List<string>? participantesAdicionales); 
        void EliminarProyecto(int idProyecto); 

        // Historial y reclamaciones
        HistorialEventosResultado GetHistorialDelUsuario();
        Reclamacion CrearReclamacion(int idEvento, string descripcion);
        IEnumerable<Reclamacion> GetReclamacionesDelUsuario();
        IEnumerable<Reclamacion> GetReclamacionesComoOrganizador();
        Reclamacion ResponderReclamacion(int idReclamacion, string estado, string? respuesta);

        // Notificaciones
        IEnumerable<Notificacion> GetNotificacionesRecibidas();
        IEnumerable<Notificacion> GetNotificacionesEnviadas();
        int GetCantidadNotificacionesNoLeidas();
        void MarcarNotificacionComoLeida(int idNotificacion);
        void EnviarMensajeOrganizadorEnEvento(int idEvento, string asunto, string mensaje);
        void InvitarUsuarioEnEventoPorEmail(int idEvento, string email, string tipoRol);
    }
}
