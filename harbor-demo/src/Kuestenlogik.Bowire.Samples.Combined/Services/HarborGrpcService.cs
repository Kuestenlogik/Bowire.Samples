// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Samples.Combined.Grpc;
using Kuestenlogik.Bowire.Samples.Shared;
using Grpc.Core;

// Both namespaces define a PortCallStatus enum (one generated from the
// proto, one from the Shared domain). Alias the domain one so method
// bodies can reference it unambiguously.
using DomainPortCall       = Kuestenlogik.Bowire.Samples.Shared.PortCall;
using DomainPortCallStatus = Kuestenlogik.Bowire.Samples.Shared.PortCallStatus;
using DomainCraneStatus    = Kuestenlogik.Bowire.Samples.Shared.CraneStatus;
using DomainContainer      = Kuestenlogik.Bowire.Samples.Shared.Container;
using DomainContainerStatus = Kuestenlogik.Bowire.Samples.Shared.ContainerStatus;
using DomainCargoOperation = Kuestenlogik.Bowire.Samples.Shared.CargoOperation;

namespace Kuestenlogik.Bowire.Samples.Combined.Services;

/// <summary>
/// gRPC implementation of the harbor surface — covers all four call
/// types (Unary / Server / Client / Bidi) on a single service so the
/// Bowire sidebar shows one gRPC service with four streaming badges.
/// </summary>
public sealed class HarborGrpcService(HarborStore store, ILogger<HarborGrpcService> log)
    : HarborService.HarborServiceBase
{
    // ---------- Unary ----------
    public override Task<SchedulePortCallReply> SchedulePortCall(
        SchedulePortCallRequest request, ServerCallContext context)
    {
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
        log.LogInformation("Scheduled PortCall {Id} for ship {Ship} at dock {Dock}", id, request.ShipId, request.DockNumber);

        return Task.FromResult(new SchedulePortCallReply { PortCall = ToMsg(pc) });
    }

    // ---------- Server streaming ----------
    public override async Task WatchCrane(WatchCraneRequest request,
        IServerStreamWriter<CraneTick> responseStream, ServerCallContext context)
    {
        if (!store.Cranes.TryGetValue(request.CraneId, out var crane))
            throw new RpcException(new Status(StatusCode.NotFound, $"Crane {request.CraneId} unknown"));

        var rng = new Random(crane.Id);
        var boom = 45.0;
        var load = 0.0;

        // Emit a fresh tick every ~250 ms until the caller cancels.
        while (!context.CancellationToken.IsCancellationRequested)
        {
            boom = Math.Clamp(boom + (rng.NextDouble() - 0.5) * 6, 10, 80);
            load = Math.Clamp(load + (rng.NextDouble() - 0.5) * 4, 0, (double)crane.MaxLiftTonnes);

            await responseStream.WriteAsync(new CraneTick
            {
                CraneId        = crane.Id,
                Status         = (Grpc.CraneStatus)crane.Status,
                BoomAngleDeg   = boom,
                LoadTonnes     = load,
                TimestampUnixMs= DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            }, context.CancellationToken);

            try { await Task.Delay(250, context.CancellationToken); }
            catch (OperationCanceledException) { return; }
        }
    }

    // ---------- Client streaming ----------
    public override async Task<UploadManifestReply> UploadManifest(
        IAsyncStreamReader<ContainerEntry> requestStream, ServerCallContext context)
    {
        int received = 0, accepted = 0, duplicates = 0;

        await foreach (var entry in requestStream.ReadAllAsync(context.CancellationToken))
        {
            received++;
            if (store.Containers.ContainsKey(entry.ContainerId))
            {
                duplicates++;
                continue;
            }

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
        return new UploadManifestReply { Received = received, Accepted = accepted, Duplicates = duplicates };
    }

    // ---------- Bidi ----------
    public override async Task HarborRadio(IAsyncStreamReader<RadioMessage> requestStream,
        IServerStreamWriter<RadioMessage> responseStream, ServerCallContext context)
    {
        await responseStream.WriteAsync(new RadioMessage
        {
            Speaker = "Harbor",
            Text    = "Harbor radio online. Identify yourself.",
            TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        });

        await foreach (var msg in requestStream.ReadAllAsync(context.CancellationToken))
        {
            // Echo the captain's message back with a canned harbor reply.
            await responseStream.WriteAsync(new RadioMessage
            {
                Speaker = "Harbor",
                Text    = $"Copy {msg.Speaker}: \"{msg.Text}\"",
                TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            }, context.CancellationToken);
        }
    }

    static PortCallMsg ToMsg(DomainPortCall pc) => new()
    {
        Id                    = pc.Id,
        ShipId                = pc.ShipId,
        DockNumber            = pc.DockNumber,
        ScheduledArrivalUnixS = pc.ScheduledArrival.ToUnixTimeSeconds(),
        Status                = (Grpc.PortCallStatus)pc.Status,
        Notes                 = pc.Notes ?? string.Empty
    };
}
