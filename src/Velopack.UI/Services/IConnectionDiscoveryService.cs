// <copyright file="IConnectionDiscoveryService.cs" company="Chris Pulman">
// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
// </copyright>

namespace Velopack.UI;

/// <summary>
/// The connection discovery service.
/// </summary>
public interface IConnectionDiscoveryService
{
    /// <summary>
    /// Gets the available connections.
    /// </summary>
    /// <value>The available connections.</value>
    IEnumerable<WebConnectionBase>? AvailableConnections { get; }

    /// <summary>
    /// Gets the connection with specified name.
    /// </summary>
    /// <param name="connectionName">Name of the connection.</param>
    /// <returns>
    /// Returns the connection with specified name; <c>null</c> if no connection with specified
    /// name is found.
    /// </returns>
    WebConnectionBase? GetByName(string connectionName);
}
