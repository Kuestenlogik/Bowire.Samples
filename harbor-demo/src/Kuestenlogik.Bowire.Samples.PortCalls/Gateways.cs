// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using System.Net.Http.Json;
using Grpc.Core;
using Grpc.Net.Client;
using Kuestenlogik.Bowire.Samples.Shared;

namespace Kuestenlogik.Bowire.Samples.PortCalls;

// The three cross-context gateways behind the PortCalls BFF. Each fetches a
// slice of a port call from the service that OWNS it, over that service's own
// wire — so resolving one `portCall(id)` visibly crosses gRPC + OData + REST.
// All three degrade gracefully (return null/empty) when the upstream service
// isn't running, so PortCalls still starts and its schema stays introspectable.

/// <summary>Ship master data from Fleet over gRPC (h2c on :5150).</summary>
public sealed class FleetGateway : IDisposable
{
    private readonly GrpcChannel _channel = GrpcChannel.ForAddress("http://localhost:5150");

    public async Task<Ship?> GetShipAsync(int shipId, CancellationToken ct)
    {
        try
        {
            var client = new Harbor.Fleet.V1.Fleet.FleetClient(_channel);
            var s = await client.GetShipAsync(
                new Harbor.Fleet.V1.GetShipRequest { Id = shipId }, cancellationToken: ct);
            // proto ShipType is 1-based (UNSPECIFIED=0), Shared.ShipType 0-based.
            return new Ship(s.Id, s.Name, s.Flag, s.LengthMeters, (ShipType)((int)s.Type - 1));
        }
        catch (RpcException)
        {
            return null;
        }
    }

    public void Dispose() => _channel.Dispose();
}

/// <summary>
/// Dock configuration from Inventory over OData (HTTPS on :5151). The dev-cert
/// bypass is fine for a localhost sample — never do this in production.
/// </summary>
public sealed class InventoryGateway
{
    private readonly HttpClient _http = new(new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback =
            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
    })
    { BaseAddress = new Uri("https://localhost:5151/") };

    public async Task<Dock?> GetDockAsync(int dockNumber, CancellationToken ct)
    {
        try
        {
            // OData single-entity read: /odata/Docks(1) returns the entity
            // (plus an @odata.context field that just deserialises away).
            return await _http.GetFromJsonAsync<Dock>($"odata/Docks({dockNumber})", ct);
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }
}

/// <summary>Containers on a ship from Gate over REST (HTTP on :5152).</summary>
public sealed class GateGateway
{
    private readonly HttpClient _http = new() { BaseAddress = new Uri("http://localhost:5152/") };

    public async Task<IReadOnlyList<Container>> GetContainersOnShipAsync(int shipId, CancellationToken ct)
    {
        try
        {
            return await _http.GetFromJsonAsync<List<Container>>(
                $"containers?onShipId={shipId}", ct) ?? [];
        }
        catch (HttpRequestException)
        {
            return [];
        }
    }
}
