// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Kuestenlogik.Bowire.Samples.Shared;
using Microsoft.AspNetCore.SignalR;

namespace Kuestenlogik.Bowire.Samples.SignalR.Hubs;

/// <summary>
/// SignalR hub showing the features that are hard to model on top of
/// plain HTTP: <b>invoke</b>, <b>server-streaming</b>, <b>client-
/// streaming</b>, <b>groups</b> (per-dock fan-out), <b>users</b> (per-
/// dispatcher notifications), and <b>broadcast to all</b>.
/// </summary>
public sealed class PortCallHub(HarborStore store) : Hub
{
    // ------- Invoke (unary-ish) -------
    public PortCall? GetPortCall(int id)
        => store.PortCalls.TryGetValue(id, out var pc) ? pc : null;

    // ------- Server streaming -------
    /// <summary>Yield every future port-call change. Lives until caller disconnects.</summary>
    public async IAsyncEnumerable<PortCall> SubscribeToChanges(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var queue = Channel.CreateUnbounded<PortCall>();
        void Handler(PortCall pc) => queue.Writer.TryWrite(pc);
        store.PortCallChanged += Handler;
        try
        {
            await foreach (var pc in queue.Reader.ReadAllAsync(ct))
                yield return pc;
        }
        finally { store.PortCallChanged -= Handler; }
    }

    // ------- Client streaming -------
    public async Task PushEtaUpdates(IAsyncEnumerable<string> updates)
    {
        await foreach (var note in updates) Context.Items["last-eta"] = note;
    }

    // ------- Groups — subscribe to a single dock -------
    /// <summary>
    /// Join a dock-specific group so the caller only sees updates for
    /// that dock. A dispatcher watching Dock 3 doesn't need the noise
    /// from the container terminal on Dock 1.
    /// </summary>
    public Task JoinDock(int dockNumber)
        => Groups.AddToGroupAsync(Context.ConnectionId, $"dock-{dockNumber}");

    public Task LeaveDock(int dockNumber)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, $"dock-{dockNumber}");

    // ------- Broadcast a pseudo-event -------
    /// <summary>
    /// Fake a status change for a port call and fan the event out:
    /// <list type="bullet">
    /// <item>to the dock group the port call is sitting on,</item>
    /// <item>to the connection's identity (<c>Clients.User</c>),</item>
    /// <item>to everyone else on the hub (<c>Clients.All</c>).</item>
    /// </list>
    /// In real code, status changes would be driven by business logic
    /// rather than a manual method — but for the demo this makes all
    /// three fan-out targets observable from the Bowire UI.
    /// </summary>
    public async Task FakeStatusChange(int portCallId, PortCallStatus newStatus)
    {
        if (!store.PortCalls.TryGetValue(portCallId, out var pc)) return;
        var updated = pc with { Status = newStatus };
        store.PortCalls[portCallId] = updated;

        await Clients.Group($"dock-{pc.DockNumber}").SendAsync("PortCallInDock", updated);
        await Clients.User(Context.UserIdentifier ?? "anonymous").SendAsync("YourPortCall", updated);
        await Clients.All.SendAsync("PortCallChanged", updated);

        store.RaisePortCallChanged(updated);
    }
}
