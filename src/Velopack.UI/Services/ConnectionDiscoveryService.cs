// <copyright file="ConnectionDiscoveryService.cs" company="Chris Pulman">
// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
// </copyright>

using System.Reflection;
using System.Runtime.Versioning;

namespace Velopack.UI;

/// <summary>
/// The connection discovery service.
/// </summary>
[SupportedOSPlatform("windows10.0.19041.0")]
public class ConnectionDiscoveryService : IConnectionDiscoveryService
{
    private Dictionary<string, WebConnectionBase>? availableConnections;

    /// <summary>
    /// Gets the available connections.
    /// </summary>
    /// <value>The available connections.</value>
    /// <remarks>This will cache all available connections on first request.</remarks>
    public IEnumerable<WebConnectionBase> AvailableConnections =>
        this.availableConnections?.Values ??
        (this.availableConnections =
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
        if (this.availableConnections == null)
        {
            _ = this.AvailableConnections.ToList();
        }

        if (string.IsNullOrWhiteSpace(connectionName))
        {
            return default;
        }

        return this.availableConnections!.TryGetValue(connectionName, out var value) ? value : default;
    }
}
