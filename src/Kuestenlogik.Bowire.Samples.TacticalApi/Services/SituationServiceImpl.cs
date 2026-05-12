// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Rheinmetall.TacticalApi.V0;

namespace Kuestenlogik.Bowire.Samples.TacticalApi.Services;

/// <summary>
/// Minimal in-memory backend for Rheinmetall's <c>Situation</c> service. Holds
/// a fixed seed of three MIL-2525C symbols along the German North Sea coast so
/// the Bowire workbench has something visually meaningful to show when a
/// developer hits <c>GetSituationObjects</c>. The Add / Delete RPCs accept
/// writes against the same store so a follow-up <c>GetSituationObjects</c> reflects
/// them; <c>SubscribeSituationObjectEvents</c> emits the current snapshot once
/// and then idles (the streaming + delta-event model is deliberately out of scope
/// for the screenshot sample — a real adapter would push every store mutation).
/// </summary>
internal sealed class SituationServiceImpl : Situation.SituationBase
{
    private readonly object _gate = new();
    private readonly Dictionary<string, SituationObject> _objects;

    public SituationServiceImpl()
    {
        _objects = SeededSituation.Build();
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
        // Initial snapshot. The contract says "all non-deleted existing
        // situation objects are returned for every call" — sample serves
        // that, then idles until the caller cancels. A real adapter would
        // hook into the store's mutation events and push deltas; for the
        // screenshot demo, the initial snapshot is the visually meaningful
        // part.
        var initial = new SubscribeSituationObjectEventsResponse
        {
            Header = OkHeader(),
        };
        lock (_gate)
        {
            foreach (var obj in _objects.Values)
                initial.SituationObjects.Add(obj);
        }
        await responseStream.WriteAsync(initial).ConfigureAwait(false);

        // Idle until cancelled — Task.Delay with the call's CancellationToken
        // is the canonical pattern for "stream stays open, no further frames".
        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, context.CancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Caller closed the stream — graceful exit.
        }
    }

    public override Task<AddOrUpdateSituationObjectsResponse> AddOrUpdateSituationObjects(
        AddOrUpdateSituationObjectsRequest request, ServerCallContext context)
    {
        // The Update*-typed request types carry partial updates; for the
        // screenshot sample we accept them as no-ops with an OK header.
        // A real adapter would translate UpdateSymbol → Symbol diffs and
        // mutate the store. Keeping this stubbed means the workbench can
        // try the RPC end-to-end without write-side surprises.
        _ = request;
        return Task.FromResult(new AddOrUpdateSituationObjectsResponse { Header = OkHeader() });
    }

    public override Task<DeleteSituationObjectsResponse> DeleteSituationObjects(
        DeleteSituationObjectsRequest request, ServerCallContext context)
    {
        lock (_gate)
        {
            foreach (var del in request.SituationObjects)
            {
                if (del.Identity?.UuidIdentity is { Length: > 0 } id)
                    _objects.Remove(id);
            }
        }
        return Task.FromResult(new DeleteSituationObjectsResponse { Header = OkHeader() });
    }

    private static ResponseHeader OkHeader() => new()
    {
        Success = true,
    };
}
