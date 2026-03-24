using System;
using System.Collections.Generic;
using System.Text;
using Votify.Persistence;

namespace Votify
{
    internal class DBTest
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Intentando conectar con Supabase...");

            try
            {
                // 1. Instanciar el contexto
                using (var db = new VotifyDBContext())
                {
                    // 2. Forzar la apertura de la conexión y realizar una consulta simple
                    // Esto verificará si el ConnectionString y el Provider están bien configurados
                    int conteoUsuarios = db.Usuarios.Count();

                    Console.WriteLine("------------------------------------------");
                    Console.WriteLine("¡CONEXIÓN EXITOSA!");
                    Console.WriteLine($"Usuarios actuales en la base de datos: {conteoUsuarios}");
                    Console.WriteLine("------------------------------------------");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("------------------------------------------");
                Console.WriteLine("ERROR AL CONECTAR:");
                // Mostramos la InnerException porque suele dar el detalle real del error de red/login
                Console.WriteLine(ex.InnerException?.Message ?? ex.Message);
                Console.WriteLine("------------------------------------------");
            }

            Console.WriteLine("Presiona cualquier tecla para salir...");
            Console.ReadKey();
        }
    }
}
