// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Grpc.Core;
using Harbor.Fleet.V1;
using Kuestenlogik.Bowire.Samples.Shared;

namespace Kuestenlogik.Bowire.Samples.Fleet.Services;

/// <summary>
/// Fleet gRPC service — serves the vessel registry from this service's own
/// private seed (<see cref="HarborSeed.Ships"/>). No shared store: other
/// contexts that need a ship reach across the wire to <c>GetShip</c>.
/// </summary>
public sealed class FleetServiceImpl : Harbor.Fleet.V1.Fleet.FleetBase
{
    private readonly IReadOnlyList<Shared.Ship> _ships = HarborSeed.Ships();

    public override Task<Harbor.Fleet.V1.Ship> GetShip(GetShipRequest request, ServerCallContext context)
    {
        var ship = _ships.FirstOrDefault(s => s.Id == request.Id);
        return ship is null
            ? throw new RpcException(new Status(StatusCode.NotFound, $"ship {request.Id} not found"))
            : Task.FromResult(ToProto(ship));
    }

    public override async Task ListShips(ListShipsRequest request, IServerStreamWriter<Harbor.Fleet.V1.Ship> responseStream, ServerCallContext context)
    {
        foreach (var ship in _ships)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            await responseStream.WriteAsync(ToProto(ship)).ConfigureAwait(false);
        }
    }

    private static Harbor.Fleet.V1.Ship ToProto(Shared.Ship s) => new()
    {
        Id = s.Id,
        Name = s.Name,
        Flag = s.Flag,
        LengthMeters = s.LengthMeters,
        // Domain ShipType (Container=0/Bulk=1/Tanker=2) → proto (UNSPECIFIED=0 shifts everything by one).
        Type = (Harbor.Fleet.V1.ShipType)((int)s.Type + 1),
    };
}
