using System.Collections.Generic;
namespace VotifyIU.Client.Services
{
    public class CategoriasPorDefecto
    {
        public static IReadOnlyCollection<String> Todas { get; } = new[] {
            "Medio Ambiente"
            , "Educación"
            , "Salud"
            , "IA"
            , "Fintech"
        };
        }
    }

