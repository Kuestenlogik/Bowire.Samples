// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Grpc.Core;
using SchemaSemantics;

namespace Kuestenlogik.Bowire.Samples.SchemaSemantics.Services;

/// <summary>
/// Single-method server emitting a small fleet of ship updates around
/// Hamburg Harbour at 1 Hz. Coordinates jitter slightly so the map
/// shows live movement instead of static pins.
/// </summary>
/// <remarks>
/// Three deliberate ships so a Bowire user sees one layer per ship
/// (when discriminator declarations land in a future phase the layer
/// grouping shifts to discriminator-value, but for now the workbench
/// just renders all three streams as one accumulating point cloud).
/// </remarks>
public sealed class ShipsService : Ships.ShipsBase
{
    private static readonly (string Name, double Lat, double Lng, string Status)[] Fleet =
    [
        ("Aurora",            53.541, 9.984, "underway"),
        ("Helgoland-Express", 53.546, 9.971, "anchored"),
        ("Containerschiff-7", 53.553, 9.992, "moored"),
    ];

    public override async Task WatchShips(
        WatchShipsRequest request,
        IServerStreamWriter<ShipUpdate> responseStream,
        ServerCallContext context)
    {
        var random = new Random(4711);
        var tick = 0;
        while (!context.CancellationToken.IsCancellationRequested)
        {
            // Round-robin through the fleet so every Hertz cycle covers
            // every ship — keeps the streaming-frames pane busy and
            // ensures multi-select demos have something to multi-select.
            var (name, lat, lng, status) = Fleet[tick % Fleet.Length];

            var jitterLat = (random.NextDouble() - 0.5) * 0.005;
            var jitterLng = (random.NextDouble() - 0.5) * 0.010;

            await responseStream.WriteAsync(new ShipUpdate
            {
                Ship   = name,
                Lat    = Math.Round(lat + jitterLat, 6),
                Lng    = Math.Round(lng + jitterLng, 6),
                Status = status,
            }, context.CancellationToken);

            tick++;
            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(1000), context.CancellationToken);
            }
            catch (TaskCanceledException)
            {
                // Client closed the subscription — let the await loop
                // unwind cleanly.
                return;
            }
        }
    }
}
