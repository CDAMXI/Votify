using System;
using Npgsql;

class Program
{
    static void Main()
    {
        string connStr = "Host=aws-1-eu-west-1.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.qoahzxuzktsrzleaxjeo;Password=yvuWJPRkV5uWleTN";
        try
        {
            using var conn = new NpgsqlConnection(connStr);
            conn.Open();

            using var cmd = new NpgsqlCommand(@"
                SELECT id_votacion, titulo, estado FROM public.votacion LIMIT 10;
            ", conn);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var id = reader.GetInt64(0);
                var tit = reader.GetString(1);
                var est = reader.IsDBNull(2) ? "NULL" : reader.GetString(2);
                Console.WriteLine($"{id}: {tit} -> {est}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error: " + ex.Message);
        }
    }
}
