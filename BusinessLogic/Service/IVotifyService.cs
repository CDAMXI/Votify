using System;
using System.Collections.Generic;
using System.Text;
using Votify.Entities;

internal interface IVotifyService
{
    void LogIn(string user, string password);
    void LogOut();
    void Registrar(string username, string email, string password);
    Usuario GetUsuarioActual();
    void GuardarVoto(int idVotacion, int idCompetidor, double puntuacion);
    void Commit();
}
