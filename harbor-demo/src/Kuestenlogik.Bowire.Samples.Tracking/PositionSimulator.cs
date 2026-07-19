// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Samples.Shared;

namespace Kuestenlogik.Bowire.Samples.Tracking;

/// <summary>
/// A tiny deterministic AIS simulator: each seeded ship drifts on its own
/// slow circle around the harbor approach (54°N 11.5°E), so the WebSocket
/// ingress always has live position frames. No <see cref="Random"/> — motion
/// is a pure function of elapsed time, so the stream is reproducible.
/// </summary>
public sealed class PositionSimulator
{
    private const double CentreLat = 54.00;
    private const double CentreLon = 11.50;

    private static readonly IReadOnlyList<Ship> Ships = HarborSeed.Ships();
    private readonly DateTimeOffset _start = DateTimeOffset.UtcNow;

    /// <summary>One position frame per ship at the current instant.</summary>
    public IEnumerable<AisPosition> Snapshot()
    {
        var elapsed = (DateTimeOffset.UtcNow - _start).TotalSeconds;
        var now = DateTimeOffset.UtcNow;

        for (var i = 0; i < Ships.Count; i++)
        {
            var ship = Ships[i];
            var radius = 0.03 + i * 0.012;                                  // ~3–6 km rings
            var phase = elapsed / (90.0 + i * 30) * 2 * Math.PI             // one lap every 1.5–2.5 min
                        + i * 2 * Math.PI / Ships.Count;                    // evenly spaced start
            var lat = CentreLat + radius * Math.Cos(phase);
            var lon = CentreLon + radius * Math.Sin(phase);
            var course = (phase * 180 / Math.PI + 90) % 360;               // heading = tangent
            var speed = 8 + i * 2;                                          // knots

            yield return new AisPosition(
                ShipId: ship.Id,
                Latitude: Math.Round(lat, 5),
                Longitude: Math.Round(lon, 5),
                SpeedKnots: speed,
                CourseDegrees: Math.Round(course, 1),
                At: now);
        }
    }
}
