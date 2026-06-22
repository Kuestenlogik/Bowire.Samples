// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Samples.Grpc.Protos;
using Kuestenlogik.Bowire.Samples.Shared;
using Grpc.Core;

// The generated proto enums live in the same simple name as the domain
// ones — alias to keep the method bodies unambiguous.
using DomainPortCall         = Kuestenlogik.Bowire.Samples.Shared.PortCall;
using DomainPortCallStatus   = Kuestenlogik.Bowire.Samples.Shared.PortCallStatus;
using DomainContainer        = Kuestenlogik.Bowire.Samples.Shared.Container;
using DomainContainerStatus  = Kuestenlogik.Bowire.Samples.Shared.ContainerStatus;
using DomainCargoOperation   = Kuestenlogik.Bowire.Samples.Shared.CargoOperation;

namespace Kuestenlogik.Bowire.Samples.Grpc.Services;

/// <summary>
/// All four gRPC call types on a single service. Bowire shows one
/// HarborService entry in the sidebar with four method badges (U, SS,
/// CS, DX). The service also reads a pair of custom metadata headers
/// — <c>x-dispatcher-id</c> and <c>x-dispatcher-role</c> — that appear
/// in the Bowire request-form so you can test header-based auth
/// patterns against the built-in UI.
/// </summary>
public sealed class HarborGrpcService(HarborStore store, ILogger<HarborGrpcService> log)
    : HarborService.HarborServiceBase
{
    // ---------- Unary ----------

    public override Task<SchedulePortCallReply> SchedulePortCall(
        SchedulePortCallRequest request, ServerCallContext context)
    {
        RequireDispatcher(context);

        if (!store.Ships.ContainsKey(request.ShipId))
            throw new RpcException(new Status(StatusCode.NotFound, $"Ship {request.ShipId} unknown"));
        if (!store.Docks.ContainsKey(request.DockNumber))
            throw new RpcException(new Status(StatusCode.NotFound, $"Dock {request.DockNumber} unknown"));

        var id = store.NextPortCallId();
        var pc = new DomainPortCall(
            Id: id,
            ShipId: request.ShipId,
            DockNumber: request.DockNumber,
            ScheduledArrival: DateTimeOffset.FromUnixTimeSeconds(request.ScheduledArrivalUnixS),
            ActualArrival: null,
            ScheduledDeparture: null,
            ActualDeparture: null,
            Status: DomainPortCallStatus.Scheduled,
            CargoOperation: Enum.TryParse<DomainCargoOperation>(request.CargoOperation, true, out var op)
                ? op : DomainCargoOperation.None,
            Notes: null);
        store.PortCalls[id] = pc;
        store.RaisePortCallChanged(pc);

        return Task.FromResult(new SchedulePortCallReply { PortCall = ToMsg(pc) });
    }

    public override Task<PortCallMsg> GetPortCall(GetPortCallRequest request, ServerCallContext context)
        => store.PortCalls.TryGetValue(request.Id, out var pc)
            ? Task.FromResult(ToMsg(pc))
            : throw new RpcException(new Status(StatusCode.NotFound, $"PortCall {request.Id} unknown"));

    public override Task<ListPortCallsReply> ListPortCalls(ListPortCallsRequest request, ServerCallContext context)
    {
        var reply = new ListPortCallsReply();
        foreach (var pc in store.PortCalls.Values)
        {
            if (request.FilterStatus != Protos.PortCallStatus.Unspecified &&
                (int)pc.Status != (int)request.FilterStatus) continue;
            reply.PortCalls.Add(ToMsg(pc));
        }
        return Task.FromResult(reply);
    }

    // ---------- Server streaming ----------

    public override async Task WatchCrane(
        WatchCraneRequest request,
        IServerStreamWriter<CraneTick> responseStream,
        ServerCallContext context)
    {
        if (!store.Cranes.TryGetValue(request.CraneId, out var crane))
            throw new RpcException(new Status(StatusCode.NotFound, $"Crane {request.CraneId} unknown"));

        var rng = new Random(crane.Id);
        var boom = 45.0;
        var load = 0.0;

        while (!context.CancellationToken.IsCancellationRequested)
        {
            boom = Math.Clamp(boom + (rng.NextDouble() - 0.5) * 6, 10, 80);
            load = Math.Clamp(load + (rng.NextDouble() - 0.5) * 4, 0, (double)crane.MaxLiftTonnes);

            await responseStream.WriteAsync(new CraneTick
            {
                CraneId         = crane.Id,
                Status          = (Protos.CraneStatus)crane.Status,
                BoomAngleDeg    = boom,
                LoadTonnes      = load,
                TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            }, context.CancellationToken);

            try { await Task.Delay(250, context.CancellationToken); }
            catch (OperationCanceledException) { return; }
        }
    }

    // ---------- Client streaming ----------

    public override async Task<UploadManifestReply> UploadManifest(
        IAsyncStreamReader<ContainerEntry> requestStream,
        ServerCallContext context)
    {
        int received = 0, accepted = 0, duplicates = 0;

        await foreach (var entry in requestStream.ReadAllAsync(context.CancellationToken))
        {
            received++;
            if (store.Containers.ContainsKey(entry.ContainerId)) { duplicates++; continue; }

            store.Containers[entry.ContainerId] = new DomainContainer(
                Id: entry.ContainerId,
                WeightKg: (decimal)entry.WeightKg,
                Owner: entry.Owner,
                Status: DomainContainerStatus.Stored,
                OnShipId: entry.ForShipId == 0 ? null : entry.ForShipId);
            accepted++;
        }

        log.LogInformation("Manifest upload: {Received} entries, {Accepted} accepted, {Duplicates} duplicates",
            received, accepted, duplicates);

        // Trailer metadata — surfaced in Bowire's response-metadata tab.
        context.ResponseTrailers.Add("x-received-count", received.ToString());
        context.ResponseTrailers.Add("x-accepted-count", accepted.ToString());

        return new UploadManifestReply { Received = received, Accepted = accepted, Duplicates = duplicates };
    }

    // ---------- Bidirectional streaming ----------

    public override async Task HarborRadio(
        IAsyncStreamReader<RadioMessage> requestStream,
        IServerStreamWriter<RadioMessage> responseStream,
        ServerCallContext context)
    {
        await responseStream.WriteAsync(new RadioMessage
        {
            Speaker         = "Harbor",
            Text            = "Harbor radio online. Identify yourself.",
            TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        });

        await foreach (var msg in requestStream.ReadAllAsync(context.CancellationToken))
        {
            await responseStream.WriteAsync(new RadioMessage
            {
                Speaker         = "Harbor",
                Text            = $"Copy {msg.Speaker}: \"{msg.Text}\"",
                TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            }, context.CancellationToken);
        }
    }

    // ---------- Helpers ----------

    /// <summary>
    /// Demo metadata check: require an <c>x-dispatcher-id</c> header.
    /// The Bowire request editor lets users add such headers via the
    /// Metadata tab, so this is the easiest thing to test from the UI.
    /// The header value is mirrored back as a response trailer so the
    /// caller can see the echo.
    /// </summary>
    static void RequireDispatcher(ServerCallContext ctx)
    {
        var id = ctx.RequestHeaders.GetValue("x-dispatcher-id");
        if (string.IsNullOrWhiteSpace(id))
            throw new RpcException(new Status(StatusCode.Unauthenticated,
                "Missing x-dispatcher-id header — add it in the Metadata tab."));
        ctx.ResponseTrailers.Add("x-echoed-dispatcher", id);
    }

    static PortCallMsg ToMsg(DomainPortCall pc) => new()
    {
        Id                      = pc.Id,
        ShipId                  = pc.ShipId,
        DockNumber              = pc.DockNumber,
        ScheduledArrivalUnixS   = pc.ScheduledArrival.ToUnixTimeSeconds(),
        Status                  = (Protos.PortCallStatus)pc.Status,
        Notes                   = pc.Notes ?? string.Empty
    };
}
