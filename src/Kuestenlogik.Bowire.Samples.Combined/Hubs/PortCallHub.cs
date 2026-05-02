// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Samples.Shared;
using Microsoft.AspNetCore.SignalR;

namespace Kuestenlogik.Bowire.Samples.Combined.Hubs;

/// <summary>
/// SignalR hub that pushes port-call status changes to every connected
/// dispatcher UI. Callers can also invoke methods directly — that's the
/// "SignalR invoke" call type that Bowire renders alongside the
/// streaming ones.
/// </summary>
public sealed class PortCallHub(HarborStore store) : Hub
{
    /// Unary-ish: look up a single port call.
    public PortCall? GetPortCall(int id)
        => store.PortCalls.TryGetValue(id, out var pc) ? pc : null;

    /// Server streaming: yield every port-call change as it happens.
    public async IAsyncEnumerable<PortCall> SubscribeToChanges(
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken ct = default)
    {
        var queue = System.Threading.Channels.Channel.CreateUnbounded<PortCall>();
        void Handler(PortCall pc) => queue.Writer.TryWrite(pc);
        store.PortCallChanged += Handler;

        try
        {
            await foreach (var pc in queue.Reader.ReadAllAsync(ct))
                yield return pc;
        }
        finally
        {
            store.PortCallChanged -= Handler;
        }
    }

    /// Client streaming: dispatcher pushes ETA updates while the ship
    /// approaches. Hub stores the latest one in the PortCall's Notes.
    public async Task PushEtaUpdates(IAsyncEnumerable<string> updates)
    {
        await foreach (var note in updates)
        {
            // No-op persistence — the point is to demonstrate the call type.
            // A real impl would match the update to a port call by client id.
            Context.Items["last-eta"] = note;
        }
    }
}
