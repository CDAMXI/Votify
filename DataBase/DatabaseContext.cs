using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Npgsql;

class DatabaseContext
{
    private class AppSettings
    {
        public ConnectionStrings? ConnectionStrings { get; set; }
    }

    private class ConnectionStrings
    {
        public string? Supabase { get; set; }
    }

    static async Task<int> Main(string[] args)
    {
        const string configFile = "appsettings.Development.json";

        if (!File.Exists(configFile))
        {
            Console.Error.WriteLine($"Archivo de configuración no encontrado: {configFile}");
            return 1;
        }

        AppSettings? settings;
        try
        {
            var json = await File.ReadAllTextAsync(configFile);
            settings = JsonSerializer.Deserialize<AppSettings>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error de parsing: {ex.Message}");
            return 1;
        }

        var connString = settings?.ConnectionStrings?.Supabase;
        if (string.IsNullOrWhiteSpace(connString))
        {
            Console.Error.WriteLine("No se encontró la cadena de conexión en el JSON.");
            return 1;
        }

        try
        {
            await using var conn = new NpgsqlConnection(connString);
            await conn.OpenAsync();

            // 1. Crear tabla
            var createTableSql = @"
                CREATE TABLE IF NOT EXISTS public.sample_items (
                    id UUID PRIMARY KEY,
                    name TEXT NOT NULL,
                    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
                );";

            await using (var createCmd = new NpgsqlCommand(createTableSql, conn))
            {
                await createCmd.ExecuteNonQueryAsync();
            }

            // 2. Insertar fila (Corregido el manejo de parámetros)
            var insertSql = "INSERT INTO public.sample_items (id, name) VALUES (@id, @name) RETURNING id, created_at;";

            await using (var insertCmd = new NpgsqlCommand(insertSql, conn))
            {
                var newId = Guid.NewGuid();
                // Es más seguro especificar el nombre del parámetro sin el @ aquí si usas versiones nuevas
                insertCmd.Parameters.AddWithValue("id", newId);
                insertCmd.Parameters.AddWithValue("name", "Elemento de ejemplo desde app");

                await using var reader = await insertCmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var id = reader.GetGuid(0);
                    var createdAt = reader.GetDateTime(1); // DateTimeOffset es compatible con TIMESTAMPTZ
                    Console.WriteLine($"Insertado: id={id}, created_at={createdAt}");
                }
            } // El reader debe cerrarse antes de la siguiente consulta, 'using' se encarga

            // 3. Leer filas
            var selectSql = "SELECT id, name, created_at FROM public.sample_items ORDER BY created_at DESC LIMIT 10;";
            await using (var selectCmd = new NpgsqlCommand(selectSql, conn))
            {
                await using var reader = await selectCmd.ExecuteReaderAsync();
                Console.WriteLine("\nÚltimas filas en public.sample_items:");
                while (await reader.ReadAsync())
                {
                    var id = reader.GetGuid(0);
                    var name = reader.GetString(1);
                    var createdAt = reader.GetDateTime(2);
                    Console.WriteLine($"- {id} | {name} | {createdAt}");
                }
            }

            await conn.CloseAsync();
            return 0;
        }
        catch (NpgsqlException pgEx)
        {
            Console.Error.WriteLine($"Error de Postgres: {pgEx.Message}");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error general: {ex.Message}");
            return 1;
        }
    }
}