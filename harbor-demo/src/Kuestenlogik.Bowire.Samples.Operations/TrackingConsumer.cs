// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using System.Net.WebSockets;
using System.Text.Json;
using Kuestenlogik.Bowire.Samples.Shared;
using Microsoft.AspNetCore.SignalR;

namespace Kuestenlogik.Bowire.Samples.Operations;

/// <summary>
/// The anticorruption edge: consumes Tracking's raw AIS WebSocket ingress and
/// re-emits each position to SignalR operators — a per-ship group send, an
/// all-clients broadcast, and the shared <see cref="PositionFeed"/> that backs
/// the hub's StreamPositions stream. Resilient: retries while Tracking is down,
/// so Operations still starts and its hub stays discoverable.
/// </summary>
public sealed class TrackingConsumer(
    IHubContext<OpsHub> hub,
    PositionFeed feed,
    ILogger<TrackingConsumer> logger) : BackgroundService
{
    private static readonly Uri TrackingUri = new("ws://localhost:5154/ais");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var ws = new ClientWebSocket();
                ws.Options.AddSubProtocol("ais+json");
                await ws.ConnectAsync(TrackingUri, stoppingToken);
                logger.LogInformation(
                    "Connected to Tracking at {Uri}; re-emitting positions to operators.", TrackingUri);

                var buffer = new byte[8 * 1024];
                while (ws.State == WebSocketState.Open && !stoppingToken.IsCancellationRequested)
                {
                    var result = await ws.ReceiveAsync(buffer, stoppingToken);
                    if (result.MessageType == WebSocketMessageType.Close) break;

                    var pos = JsonSerializer.Deserialize<AisPosition>(buffer.AsSpan(0, result.Count));
                    if (pos is null) continue;

                    feed.Publish(pos);
                    await hub.Clients.Group($"ship-{pos.ShipId}").SendAsync("Position", pos, stoppingToken);
                    await hub.Clients.All.SendAsync("Position", pos, stoppingToken);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Tracking not up yet (run it first) — keep the hub alive and retry.
                logger.LogDebug(ex, "Tracking ingress unavailable — retrying in 2s");
                try { await Task.Delay(2000, stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }
    }
}
