using System;
using System.Collections.Generic;
using System.Linq;
using Votify.BusinessLogic.Service;
using Votify.Entities;

namespace Votify.Tests
{
    /// <summary>
    /// Pruebas de aceptacion: mejorar la gestion de datos de registro.
    ///
    /// Criterios verificados:
    ///   - Las nuevas contrasenas deben tener 8 caracteres, letras, numeros y caracter especial.
    ///   - El correo electronico debe estar asociado a una unica cuenta.
    ///   - El login acepta nombre de usuario o correo electronico.
    ///   - Los perfiles existentes con contrasenas antiguas siguen pudiendo iniciar sesion.
    /// </summary>
    public static class GestionDatosRegistroTest
    {
        private sealed record Contexto(VotifyService Service, InMemoryDAL<Usuario> Usuarios);

        private static Contexto CrearContexto()
        {
            var usuarioRepo = new InMemoryDAL<Usuario>(u => u.Id);

            var repos = new VotifyRepositories(
                usuarioRepo,
                new InMemoryDAL<Voto>(v => v.Id),
                new InMemoryDAL<Votacion>(v => v.Id),
                new InMemoryDAL<Evento>(e => e.IdEvento),
                new InMemoryDAL<Rol>(r => r.Id),
                new InMemoryDAL<Proyecto>(p => p.Id),
                new InMemoryDAL<Jurado>(j => j.Id),
                new InMemoryDAL<Publico>(p => p.Id),
                new InMemoryDAL<Competidor>(c => c.Id),
                new InMemoryDAL<Organizador>(o => o.Id),
                new InMemoryDAL<EncargadoVotacion>(e => e.Id),
                new InMemoryDAL<Reclamacion>(r => r.Id));

            return new Contexto(new VotifyService(repos), usuarioRepo);
        }

        public static (bool Success, string Message) RechazaPasswordCorta()
            => RegistrarDebeFallar("Ab1!", "Se rechazo contrasena de menos de 8 caracteres");

        public static (bool Success, string Message) RechazaPasswordSinNumero()
            => RegistrarDebeFallar("Abcdefg!", "Se rechazo contrasena sin numeros");

        public static (bool Success, string Message) RechazaPasswordSinLetra()
            => RegistrarDebeFallar("1234567!", "Se rechazo contrasena sin letras");

        public static (bool Success, string Message) RechazaPasswordSinEspecial()
            => RegistrarDebeFallar("Abcdefg1", "Se rechazo contrasena sin caracter especial");

        public static (bool Success, string Message) RegistraPasswordSegura()
        {
            var ctx = CrearContexto();

            ctx.Service.Registrar("ana", "ANA@MAIL.COM", "Abcdef1!");
            var usuario = ctx.Usuarios.GetAll().SingleOrDefault();

            bool ok = usuario != null
                && usuario.Username == "ana"
                && usuario.Email == "ana@mail.com";

            return ok
                ? (true, "Se registro una cuenta con contrasena segura y correo normalizado")
                : (false, "No se registro correctamente la cuenta valida");
        }

        public static (bool Success, string Message) RechazaEmailDuplicado()
        {
            var ctx = CrearContexto();
            ctx.Service.Registrar("ana", "ana@mail.com", "Abcdef1!");

            try
            {
                ctx.Service.Registrar("ana2", "ANA@MAIL.COM", "Xyzabc1!");
                return (false, "Se permitio registrar dos cuentas con el mismo correo");
            }
            catch (ServiceException)
            {
                return (true, "Se rechazo correo duplicado ignorando mayusculas/minusculas");
            }
        }

        public static (bool Success, string Message) RechazaEmailDuplicadoAlActualizarPerfil()
        {
            var ctx = CrearContexto();
            ctx.Usuarios.Insert(new Usuario("ana", "ana@mail.com", "Abcdef1!", 1) { roles = new List<Rol>() });
            ctx.Usuarios.Insert(new Usuario("bea", "bea@mail.com", "Xyzabc1!", 2) { roles = new List<Rol>() });

            ctx.Service.LogIn("bea", "Xyzabc1!");

            try
            {
                ctx.Service.UpdateEmail("ANA@MAIL.COM");
                return (false, "Se permitio asignar un correo ya usado a otro perfil");
            }
            catch (ServiceException)
            {
                return (true, "Se rechazo actualizar perfil con correo duplicado");
            }
        }

        public static (bool Success, string Message) LoginFuncionaConUsername()
        {
            var ctx = CrearContextoConUsuarioRegistrado();

            ctx.Service.LogIn("ana", "Abcdef1!");

            return ctx.Service.GetUsuarioActual().Username == "ana"
                ? (true, "Login por nombre de usuario sigue funcionando")
                : (false, "Login por nombre de usuario no dejo sesion correcta");
        }

        public static (bool Success, string Message) LoginFuncionaConEmail()
        {
            var ctx = CrearContextoConUsuarioRegistrado();

            ctx.Service.LogIn("ANA@MAIL.COM", "Abcdef1!");

            return ctx.Service.GetUsuarioActual().Username == "ana"
                ? (true, "Login por correo identifica el usuario real")
                : (false, "Login por correo no dejo sesion asociada al username real");
        }

        public static (bool Success, string Message) LoginLegacyNoRevalidaPasswordAntigua()
        {
            var ctx = CrearContexto();
            ctx.Usuarios.Insert(new Usuario("legacy", "legacy@mail.com", "pass", 1) { roles = new List<Rol>() });

            ctx.Service.LogIn("legacy", "pass");

            return ctx.Service.GetUsuarioActual().Username == "legacy"
                ? (true, "Un perfil existente con contrasena antigua puede iniciar sesion")
                : (false, "El login revalido una contrasena antigua y rompio compatibilidad");
        }

        public static (bool Success, string Message) EmailLegacyDuplicadoNoIniciaSesionAmbigua()
        {
            var ctx = CrearContexto();
            ctx.Usuarios.Insert(new Usuario("ana", "duplicado@mail.com", "Abcdef1!", 1) { roles = new List<Rol>() });
            ctx.Usuarios.Insert(new Usuario("bea", "duplicado@mail.com", "Xyzabc1!", 2) { roles = new List<Rol>() });

            ctx.Service.LogIn("ana", "Abcdef1!");
            bool usernameSigueOk = ctx.Service.GetUsuarioActual().Username == "ana";

            try
            {
                ctx.Service.LogIn("duplicado@mail.com", "Abcdef1!");
                return (false, "Se permitio login por correo duplicado y ambiguo");
            }
            catch (ServiceException)
            {
                return usernameSigueOk
                    ? (true, "Un email duplicado legado no inicia una sesion ambigua y username sigue funcionando")
                    : (false, "El username dejo de funcionar en un escenario legado con email duplicado");
            }
        }

        private static Contexto CrearContextoConUsuarioRegistrado()
        {
            var ctx = CrearContexto();
            ctx.Service.Registrar("ana", "ana@mail.com", "Abcdef1!");
            return ctx;
        }

        private static (bool Success, string Message) RegistrarDebeFallar(string password, string mensajeOk)
        {
            var ctx = CrearContexto();

            try
            {
                ctx.Service.Registrar("ana", "ana@mail.com", password);
                return (false, $"Se acepto una contrasena insegura: {password}");
            }
            catch (ServiceException)
            {
                return (true, mensajeOk);
            }
        }

        public static (bool Success, string Message) RunAll() => TestRunner.Run(
            "UT - Mejorar gestion de datos de registro",
            ("Rechaza password corta", RechazaPasswordCorta),
            ("Rechaza password sin numero", RechazaPasswordSinNumero),
            ("Rechaza password sin letra", RechazaPasswordSinLetra),
            ("Rechaza password sin especial", RechazaPasswordSinEspecial),
            ("Registra password segura", RegistraPasswordSegura),
            ("Email unico en registro", RechazaEmailDuplicado),
            ("Email unico en perfil", RechazaEmailDuplicadoAlActualizarPerfil),
            ("Login con username", LoginFuncionaConUsername),
            ("Login con email", LoginFuncionaConEmail),
            ("Login legacy compatible", LoginLegacyNoRevalidaPasswordAntigua),
            ("Email legacy ambiguo", EmailLegacyDuplicadoNoIniciaSesionAmbigua));
    }
}
