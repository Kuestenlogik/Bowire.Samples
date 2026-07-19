// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Samples.Shared;

namespace Kuestenlogik.Bowire.Samples.Arrivals;

/// <summary>
/// The read-side projector. Seeds the board with each port call's current
/// status, then walks them round-robin through the lifecycle
/// (Scheduled → Approaching → Docked → Departing → Completed) as synthetic
/// arrival events, so the SSE stream always has fresh milestones to push.
/// </summary>
public sealed class ArrivalsProjector(ArrivalsFeed feed) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var calls = HarborSeed.PortCalls(DateTimeOffset.UtcNow).ToList();
        var status = calls.ToDictionary(pc => pc.Id, pc => pc.Status);

        // Seed the board with the current status of each port call.
        foreach (var pc in calls)
            feed.Emit(pc.Id, pc.ShipId, pc.Status);

        var idx = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await Task.Delay(3000, stoppingToken); }
            catch (OperationCanceledException) { break; }

            var pc = calls[idx++ % calls.Count];
            var next = Advance(status[pc.Id]);
            if (next == status[pc.Id]) continue;   // terminal — nothing to emit
            status[pc.Id] = next;
            feed.Emit(pc.Id, pc.ShipId, next);
        }
    }

    private static PortCallStatus Advance(PortCallStatus s) => s switch
    {
        PortCallStatus.Scheduled => PortCallStatus.Approaching,
        PortCallStatus.Approaching => PortCallStatus.Docked,
        PortCallStatus.Docked => PortCallStatus.Departing,
        PortCallStatus.Departing => PortCallStatus.Completed,
        _ => s,
    };
}
