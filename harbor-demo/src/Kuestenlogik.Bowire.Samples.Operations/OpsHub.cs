// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Kuestenlogik.Bowire.Samples.Shared;
using Microsoft.AspNetCore.SignalR;

namespace Kuestenlogik.Bowire.Samples.Operations;

/// <summary>
/// The operator console hub — the framework-rich egress edge (contrast with
/// Tracking's bare WebSocket ingress). Shows the SignalR feature set Bowire
/// discovers: per-ship groups, a broadcast, and a server-to-client stream.
/// </summary>
public sealed class OpsHub(PositionFeed feed) : Hub
{
    /// <summary>Watch one vessel's track (join its group).</summary>
    public Task WatchShip(int shipId) => Groups.AddToGroupAsync(Context.ConnectionId, $"ship-{shipId}");

    public Task UnwatchShip(int shipId) => Groups.RemoveFromGroupAsync(Context.ConnectionId, $"ship-{shipId}");

    /// <summary>Push an operator notice to everyone.</summary>
    public Task Broadcast(string message) => Clients.All.SendAsync("Notice", message);

    /// <summary>Server-to-client stream of the live position feed.</summary>
    public async IAsyncEnumerable<AisPosition> StreamPositions([EnumeratorCancellation] CancellationToken ct)
    {
        var channel = Channel.CreateBounded<AisPosition>(
            new BoundedChannelOptions(100) { FullMode = BoundedChannelFullMode.DropOldest });

        void Handler(AisPosition p) => channel.Writer.TryWrite(p);
        feed.OnPosition += Handler;
        try
        {
            await foreach (var p in channel.Reader.ReadAllAsync(ct))
                yield return p;
        }
        finally
        {
            feed.OnPosition -= Handler;
        }
    }
}
