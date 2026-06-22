// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using System.Threading.Channels;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Rheinmetall.TacticalApi.V0;

namespace Kuestenlogik.Bowire.Samples.TacticalApi.Services;

/// <summary>
/// In-memory backend for Rheinmetall's <c>Situation</c> service. Seeds three
/// MIL-2525C symbols along the German North Sea coast so the Bowire workbench
/// has something visually meaningful on <c>GetSituationObjects</c>, then keeps
/// the world alive: a background mover nudges each object's position every
/// ~2 seconds and the mutation pushes into every active
/// <c>SubscribeSituationObjectEvents</c> stream so subscribers see live
/// updates. The Add / Update / Delete RPCs apply against the same store and
/// fan out through the same broadcast so a workbench round-trip
/// (subscribe + add) shows the freshly-written symbol arrive in real time.
/// </summary>
internal sealed class SituationServiceImpl : Situation.SituationBase, IDisposable
{
    private readonly object _gate = new();
    private readonly Dictionary<string, SituationObject> _objects;
    private readonly List<ChannelWriter<SubscribeSituationObjectEventsResponse>> _subscribers = [];
    private readonly CancellationTokenSource _moverCts = new();
    private readonly Task _moverTask;

    public SituationServiceImpl()
    {
        _objects = SeededSituation.Build();
        _moverTask = Task.Run(() => RunMoverAsync(_moverCts.Token));
    }

    public override Task<GetSituationObjectsResponse> GetSituationObjects(
        GetSituationObjectsRequest request, ServerCallContext context)
    {
        var response = new GetSituationObjectsResponse
        {
            Header = OkHeader(),
        };
        lock (_gate)
        {
            foreach (var obj in _objects.Values)
                response.SituationObjects.Add(obj);
        }
        return Task.FromResult(response);
    }

    public override async Task SubscribeSituationObjectEvents(
        SubscribeSituationObjectEventsRequest request,
        IServerStreamWriter<SubscribeSituationObjectEventsResponse> responseStream,
        ServerCallContext context)
    {
        // Per-subscriber unbounded channel. Bounded would be more defensive
        // but the demo runs with three objects and a 2-second push period;
        // backpressure isn't a concern.
        var channel = Channel.CreateUnbounded<SubscribeSituationObjectEventsResponse>(
            new UnboundedChannelOptions { SingleReader = true });
        lock (_gate)
        {
            _subscribers.Add(channel.Writer);
        }

        // Initial snapshot — the spec says "all non-deleted existing
        // situation objects are returned for every call". Emit synchronously
        // so the subscriber has data before the background mover ticks.
        var initial = BuildSnapshotLocked();
        await responseStream.WriteAsync(initial).ConfigureAwait(false);

        try
        {
            // Pump from the per-subscriber channel until the caller closes.
            await foreach (var update in channel.Reader.ReadAllAsync(context.CancellationToken).ConfigureAwait(false))
            {
                await responseStream.WriteAsync(update).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Caller closed the stream — graceful exit.
        }
        finally
        {
            lock (_gate)
            {
                _subscribers.Remove(channel.Writer);
            }
            channel.Writer.TryComplete();
        }
    }

    public override Task<AddOrUpdateSituationObjectsResponse> AddOrUpdateSituationObjects(
        AddOrUpdateSituationObjectsRequest request, ServerCallContext context)
    {
        // Each request entry is an UpdateSituationObject — a oneof carrying
        // an UpdateSymbol / UpdateActionTask / UpdateActionEvent partial.
        // For the workbench demo we only handle the Symbol branch, which
        // covers the visible map-side mutations. Upserts insert a new
        // SituationObject keyed off UpdateSymbol.Identity.UuidIdentity;
        // for existing rows the symbol slot stays untouched (the
        // background mover keeps pushing the live position regardless).
        // Either way every touched object is fanned out to active
        // subscribers so a workbench round-trip 'AddOrUpdate then watch
        // Subscribe' actually shows the write.
        var touched = new List<SituationObject>();
        lock (_gate)
        {
            foreach (var upd in request.SituationObjects)
            {
                var id = upd.Symbol?.Identity?.UuidIdentity;
                if (string.IsNullOrEmpty(id)) continue;

                if (!_objects.TryGetValue(id, out var existing))
                {
                    // Insert: spin up a fresh SituationObject with a
                    // minimal Symbol so a workbench round-trip
                    // 'add + subscribe' shows the newcomer. The full
                    // UpdateSymbol → Symbol projection (CreationMetaData,
                    // DataProperty wrappers, &c) is out of scope here —
                    // a real adapter would map it 1:1.
                    existing = new SituationObject
                    {
                        Symbol = new Symbol
                        {
                            Identity = new Identity { UuidIdentity = id },
                        },
                    };
                    _objects[id] = existing;
                }
                touched.Add(existing);
            }
        }

        if (touched.Count > 0)
        {
            var broadcast = new SubscribeSituationObjectEventsResponse { Header = OkHeader() };
            broadcast.SituationObjects.AddRange(touched);
            BroadcastToSubscribers(broadcast);
        }

        return Task.FromResult(new AddOrUpdateSituationObjectsResponse { Header = OkHeader() });
    }

    public override Task<DeleteSituationObjectsResponse> DeleteSituationObjects(
        DeleteSituationObjectsRequest request, ServerCallContext context)
    {
        var removed = new List<SituationObject>();
        lock (_gate)
        {
            foreach (var del in request.SituationObjects)
            {
                if (del.Identity?.UuidIdentity is { Length: > 0 } id &&
                    _objects.Remove(id, out var gone))
                {
                    removed.Add(gone);
                }
            }
        }
        // Re-broadcast the current snapshot so subscribers see the
        // deletion via the simplest signal we have: a fresh full list
        // that's smaller than before. (The .proto has no explicit
        // 'deleted' frame today.)
        if (removed.Count > 0)
        {
            BroadcastSnapshot();
        }
        return Task.FromResult(new DeleteSituationObjectsResponse { Header = OkHeader() });
    }

    /// <summary>
    /// Background mover — nudges every object's position by a small
    /// random delta every ~2 s and broadcasts the resulting snapshot so
    /// subscribers see continuous live data without the workbench
    /// having to call AddOrUpdate. RandomNumberGenerator instead of
    /// System.Random because the editorconfig flags CA5394.
    /// </summary>
    private async Task RunMoverAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(2), ct).ConfigureAwait(false);
                lock (_gate)
                {
                    foreach (var obj in _objects.Values)
                    {
                        var geo = obj.Symbol?.Location?.Content?.Point?.GeoPoint;
                        if (geo is null) continue;
                        // ±0.005° (≈ 500 m at this latitude) per tick. Keep
                        // the symbols within the visible Bowire map without
                        // wandering off into the North Atlantic.
                        geo.LatitudeCoordinate += RandomNumberGenerator.GetInt32(-50, 51) / 10000.0;
                        geo.LongitudeCoordinate += RandomNumberGenerator.GetInt32(-50, 51) / 10000.0;
                    }
                }
                BroadcastSnapshot();
            }
        }
        catch (OperationCanceledException)
        {
            // Service shutdown — clean exit.
        }
    }

    private SubscribeSituationObjectEventsResponse BuildSnapshotLocked()
    {
        var snapshot = new SubscribeSituationObjectEventsResponse { Header = OkHeader() };
        foreach (var obj in _objects.Values)
            snapshot.SituationObjects.Add(obj);
        return snapshot;
    }

    private void BroadcastSnapshot()
    {
        SubscribeSituationObjectEventsResponse frame;
        lock (_gate) { frame = BuildSnapshotLocked(); }
        BroadcastToSubscribers(frame);
    }

    private void BroadcastToSubscribers(SubscribeSituationObjectEventsResponse frame)
    {
        ChannelWriter<SubscribeSituationObjectEventsResponse>[] snapshot;
        lock (_gate) { snapshot = [.. _subscribers]; }
        foreach (var writer in snapshot)
        {
            // TryWrite is non-blocking; if the consumer's channel is
            // closed (subscriber cancelled mid-call) the write is a
            // no-op. The finally-block in SubscribeSituationObjectEvents
            // cleans the writer out of _subscribers anyway.
            writer.TryWrite(frame);
        }
    }

    public void Dispose()
    {
        _moverCts.Cancel();
        try { _moverTask.GetAwaiter().GetResult(); }
        catch (OperationCanceledException) { /* expected */ }
        catch (AggregateException) { /* swallow — shutdown */ }
        _moverCts.Dispose();
    }

    private static ResponseHeader OkHeader() => new()
    {
        Success = true,
    };
}
