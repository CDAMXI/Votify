using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Npgsql;

namespace Votify.Tests
{
    internal static class DBTest
    {
        public static (bool Success, string Message) Run()
        {
            try
            {
                string configPath = GetConfigPath();
                string connectionString = GetConnectionString(configPath);

                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();

                    using (var command = new NpgsqlCommand("SELECT 1", connection))
                    {
                        var result = command.ExecuteScalar();
                        return (true, $"Conexion correcta a la base de datos.\nResultado de SELECT 1: {result}");
                    }
                }
            }
            catch (Exception ex)
            {
                return (false, ex.InnerException?.Message ?? ex.Message);
            }
        }

        public static (bool Success, string Message) CheckData()
        {
            try
            {
                string connectionString = GetConnectionString(GetConfigPath());
                string[] tables =
                {
                    "usuario",
                    "evento",
                    "rol_evento",
                    "competidor",
                    "publico",
                    "encargado",
                    "jurado",
                    "organizador",
                    "votacion",
                    "proyecto",
                    "voto",
                    "dashboard",
                    "hoja_ruta"
                };

                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();

                    var lines = tables
                        .Select(table => $"{table}: {GetRowCount(connection, table)} registros")
                        .ToArray();

                    return (true, "Comprobacion de datos:\n" + string.Join("\n", lines));
                }
            }
            catch (Exception ex)
            {
                return (false, ex.InnerException?.Message ?? ex.Message);
            }
        }

        private static string GetConfigPath()
        {
            string[] candidates =
            {
                Path.Combine(AppContext.BaseDirectory, "Votify.dll.config"),
                Path.Combine(Directory.GetCurrentDirectory(), "App.config")
            };

            string? path = candidates.FirstOrDefault(File.Exists);
            if (path is null)
                throw new FileNotFoundException("No se ha encontrado App.config ni Votify.dll.config.");

            return path;
        }

        private static string GetConnectionString(string configPath)
        {
            var document = XDocument.Load(configPath);
            var connectionString = document
                .Descendants("add")
                .FirstOrDefault(x => (string?)x.Attribute("name") == "VotifyDbConnection")
                ?.Attribute("connectionString")
                ?.Value;

            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("No se ha encontrado la cadena VotifyDbConnection en la configuracion.");

            return connectionString;
        }

        private static int GetRowCount(NpgsqlConnection connection, string tableName)
        {
            using (var command = new NpgsqlCommand($"select count(*) from public.{tableName}", connection))
            {
                return Convert.ToInt32(command.ExecuteScalar());
            }
        }
    }
}
