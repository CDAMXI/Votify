using System;
using System.Collections.Generic;
using System.Text;
using Votify.Entities;

public interface IVotifyService
{
    void LogIn(string user, string password);
    void LogOut();
    void RestoreSession(string username);
    void Registrar(string username, string email, string password);
    Usuario GetUsuarioActual();
    void GuardarVoto(int idVotacion, int idCompetidor, double puntuacion, string? comentario);
    void Commit();
    void crearVotoación(DateTime end, bool status);
    void borrarVotacion(int idVotacion);
    void modificarFecha(int votacionId, DateTime newEnd);
    Rol GetRolEnEvento(int idEvento);
    void AsignarRolEnEvento(string tipoRol, int idEvento);
    bool HasVotadoEnEvento(int idEvento);
}
