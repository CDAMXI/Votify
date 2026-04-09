using System.Data.Common;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using Npgsql;

namespace Votify.Persistence
{
    public class VotifyDbConfiguration : DbConfiguration
    {
        public VotifyDbConfiguration()
        {
            SetProviderFactory("Npgsql", NpgsqlFactory.Instance);
            SetProviderServices("Npgsql", NpgsqlServices.Instance);
        }
    }
}
