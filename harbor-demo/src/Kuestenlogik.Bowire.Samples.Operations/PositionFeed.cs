// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Samples.Shared;

namespace Kuestenlogik.Bowire.Samples.Operations;

/// <summary>
/// The in-process bridge between the <c>TrackingConsumer</c> (which ingests
/// Tracking's WebSocket feed) and the <c>OpsHub.StreamPositions</c> SignalR
/// stream. Singleton; the consumer publishes, each active stream subscribes.
/// </summary>
public sealed class PositionFeed
{
    public event Action<AisPosition>? OnPosition;

    public void Publish(AisPosition position) => OnPosition?.Invoke(position);
}
