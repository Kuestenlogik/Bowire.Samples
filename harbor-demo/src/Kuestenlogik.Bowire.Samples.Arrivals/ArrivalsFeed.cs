// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Samples.Shared;

namespace Kuestenlogik.Bowire.Samples.Arrivals;

/// <summary>
/// The arrivals-board read model: a bounded replay buffer of
/// <see cref="ArrivalEvent"/>s with a monotonic sequence. The bounded buffer
/// is what makes SSE <c>Last-Event-ID</c> resume demonstrable — a client that
/// reconnects with the last id it saw gets everything since, up to the buffer
/// window.
/// </summary>
public sealed class ArrivalsFeed
{
    private const int Capacity = 512;

    private readonly object _gate = new();
    private readonly Queue<ArrivalEvent> _buffer = new();
    private long _seq;

    public event Action<ArrivalEvent>? OnEvent;

    public void Emit(int portCallId, int shipId, PortCallStatus status)
    {
        ArrivalEvent ev;
        lock (_gate)
        {
            ev = new ArrivalEvent(++_seq, portCallId, shipId, status, DateTimeOffset.UtcNow);
            _buffer.Enqueue(ev);
            while (_buffer.Count > Capacity) _buffer.Dequeue();
        }
        OnEvent?.Invoke(ev);
    }

    /// <summary>Buffered events newer than <paramref name="lastSeq"/> — the replay window.</summary>
    public IReadOnlyList<ArrivalEvent> Since(long lastSeq)
    {
        lock (_gate) return _buffer.Where(e => e.Seq > lastSeq).ToArray();
    }
}
