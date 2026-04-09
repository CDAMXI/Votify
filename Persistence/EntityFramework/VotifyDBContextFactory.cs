using System.Data.Entity.Infrastructure;
using Npgsql;

namespace Votify.Persistence
{
    public class VotifyDBContextFactory : IDbContextFactory<VotifyDBContext>
    {
        public VotifyDBContext Create()
        {
            const string connectionString =
                "Host=aws-1-eu-west-1.pooler.supabase.com;Port=5432;Database=postgres;" +
                "Username=postgres.qoahzxuzktsrzleaxjeo;Password=yvuWJPRkV5uWleTN";

            return new VotifyDBContext(connectionString);
        }
    }
}
