using System.Reflection;

namespace Velopack.UI;

/// <summary>
/// The connection discovery service.
/// </summary>
public class ConnectionDiscoveryService : IConnectionDiscoveryService
{
    private Dictionary<string, WebConnectionBase>? _availableConnections;

    /// <summary>
    /// Gets the available connections.
    /// </summary>
    /// <value>The available connections.</value>
    /// <remarks>This will cache all available connections on first request.</remarks>
    public IEnumerable<WebConnectionBase> AvailableConnections =>
        _availableConnections?.Values ??
        (_availableConnections =
            Assembly
                .GetExecutingAssembly()
                .GetExportedTypes()
                .Where(type => type.IsClass && !type.IsAbstract && typeof(WebConnectionBase).IsAssignableFrom(type))
                .Select(connType => (WebConnectionBase)Activator.CreateInstance(connType)!)
                .Where(conn => !string.IsNullOrWhiteSpace(conn.ConnectionName))
                .ToDictionary(conn => conn.ConnectionName!, conn => conn)).Values;

    /// <summary>
    /// Gets the connection with specified name.
    /// </summary>
    /// <param name="connectionName">Name of the connection.</param>
    /// <returns>
    /// Returns the connection with specified name; <c>null</c> if no connection with specified
    /// name is found.
    /// </returns>
    public WebConnectionBase? GetByName(string connectionName)
    {
        if (_availableConnections == null) {
            return default;
        }

        if (connectionName == null
            || !_availableConnections.TryGetValue(connectionName, out var value)) {
            return default;
        }

        return value;
    }
}
