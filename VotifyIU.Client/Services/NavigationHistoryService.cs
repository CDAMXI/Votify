namespace VotifyIU.Client.Services;

/// <summary>
/// Implementación basada en <see cref="Stack{T}"/>. Aplica dedupe de URLs consecutivas
/// para tolerar dobles clics y excluye rutas de autenticación para que "Volver" nunca
/// devuelva al usuario a la pantalla de login.
/// </summary>
public sealed class NavigationHistoryService : INavigationHistoryService
{
    private const int CapacidadMaxima = 50;

    private static readonly HashSet<string> RutasExcluidas =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "/",
            "/login",
            "/registro",
            "/recuperar-password",
            "/reset-password"
        };

    private readonly Stack<string> _historial = new();
    private bool _ignorarProximoPush;

    public int Count => _historial.Count;

    public void Push(string url)
    {
        if (!EsApilable(url)) return;
        if (_historial.Count > 0 && _historial.Peek() == url) return;

        _historial.Push(url);

        while (_historial.Count > CapacidadMaxima)
        {
            // Stack<T> no permite eliminar por la base sin reconstruir.
            var conservadas = _historial.Take(CapacidadMaxima).Reverse().ToArray();
            _historial.Clear();
            foreach (var entrada in conservadas) _historial.Push(entrada);
        }
    }

    public string? Pop() => _historial.Count == 0 ? null : _historial.Pop();

    public void Clear()
    {
        _historial.Clear();
        _ignorarProximoPush = false;
    }

    public void SuprimirProximoPush() => _ignorarProximoPush = true;

    public bool DeberiaIgnorarProximoPush()
    {
        if (!_ignorarProximoPush) return false;
        _ignorarProximoPush = false;
        return true;
    }

    private static bool EsApilable(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
        return !RutasExcluidas.Contains(uri.AbsolutePath);
    }
}
