// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;
using System.Runtime.Versioning;

namespace Velopack.UI;

/// <summary>
/// The connection discovery service.
/// </summary>
[SupportedOSPlatform("windows10.0.19041.0")]
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
                .Where(type =>
                    type.IsClass &&
                    !type.IsAbstract &&
                    typeof(WebConnectionBase).IsAssignableFrom(type) &&
                    type != typeof(VelopackModel) && // exclude model to avoid recursion
                    type.Name.EndsWith("Connection", StringComparison.Ordinal))
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
        // Ensure cache is populated
        if (_availableConnections == null)
        {
            _ = AvailableConnections.ToList();
        }

        if (string.IsNullOrWhiteSpace(connectionName))
        {
            return default;
        }

        return _availableConnections!.TryGetValue(connectionName, out var value) ? value : default;
    }
}
