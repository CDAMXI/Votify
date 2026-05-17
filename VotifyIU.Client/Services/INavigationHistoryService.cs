namespace VotifyIU.Client.Services;

/// <summary>
/// Gestor LIFO del historial de navegación. Captura la ruta anterior antes de cada
/// transición para que un botón "Volver" pueda resolver dinámicamente su destino sin
/// rutas hardcodeadas. Se reinicia con <see cref="Clear"/> al iniciar nueva sesión.
/// </summary>
public interface INavigationHistoryService
{
    void Push(string url);
    string? Pop();
    void Clear();
    int Count { get; }

    /// <summary>
    /// Suprime el siguiente push automático del listener global. Lo invoca el botón
    /// "Volver" antes de navegar para evitar que la ruta saliente vuelva a apilarse.
    /// </summary>
    void SuprimirProximoPush();

    /// <summary>True si se debe ignorar el siguiente push y se consume la marca.</summary>
    bool DeberiaIgnorarProximoPush();
}
