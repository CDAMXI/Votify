using System;
using System.Collections.Generic;
using Votify.Entities;

namespace Votify.BusinessLogic.Service
{
    public interface IVotifyService
    {
        // Autenticación
        void LogIn(string username, string password);
        void LogOut();
        void RestoreSession(string username);
        void Registrar(string username, string email, string password);

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
        bool HasVotadoEnEvento(int idEvento);
        List<int> GetMisVotos(int idVotacion); 
        void Commit();

        // Votaciones
        int CrearVotacion(string titulo, string? descripcion, DateTime fechaFin, bool activa, bool permiteCompetidoresVotar = false, int pesoJurado = 70, int pesoPublico = 30);
        Votacion GetVotacion(int idVotacion);
        IEnumerable<Votacion> GetMisVotaciones();
        IEnumerable<Votacion> GetAllVotaciones(); 
        void EliminarEvento(int idVotacion);
        void ModificarVotacion(int idVotacion, DateTime nuevaFechaFin, bool estado);
        void CerrarVotacion(int idVotacion);
        void TogglePausarVotacion(int idVotacion);

        // Roles
        Rol GetRolEnEvento(int idEvento);
        void AsignarRolEnEvento(string tipoRol, int idEvento);
        string? GetTipoRolEnEvento(int idEvento); 
        string? GetTipoRolDeUsuario(int idUsuario, int idEvento); 

        // Proyectos 
        Proyecto CrearProyecto(int idVotacion, string nombre, string? descripcion, string usernameCompetidor); 
        void ModificarProyecto(int idProyecto, string nombre, string? descripcion, List<string>? participantesAdicionales); 
        void EliminarProyecto(int idProyecto); 
    }
}
