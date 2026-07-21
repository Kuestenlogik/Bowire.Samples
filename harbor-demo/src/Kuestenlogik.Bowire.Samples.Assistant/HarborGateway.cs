// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using System.Net.Http.Json;
using System.Text.Json;

namespace Kuestenlogik.Bowire.Samples.Assistant;

/// <summary>
/// The Assistant's window onto the rest of the harbor. The MCP tools front
/// the running services over their own wires — PortCalls (GraphQL BFF) for
/// port-call reads/mutations and the ship/container fan-out, and Gate (REST)
/// for container queries — so the AI surface genuinely drives the landscape
/// rather than a private copy of the data. Every call degrades gracefully when
/// an upstream isn't running, so the MCP server (and its tool list) stays up.
/// </summary>
public sealed class HarborGateway
{
    private static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web);

    private readonly HttpClient _portCalls =
        new() { BaseAddress = new Uri("http://localhost:5153/") };   // PortCalls GraphQL
    private readonly HttpClient _gate =
        new() { BaseAddress = new Uri("http://localhost:5152/") };   // Gate REST

    /// <summary>Run a GraphQL query/mutation against PortCalls; returns the raw <c>data</c> node.</summary>
    public async Task<JsonElement> GraphQLAsync(string query, CancellationToken ct)
    {
        try
        {
            var resp = await _portCalls.PostAsJsonAsync("graphql", new { query }, Json, ct);
            var doc = await resp.Content.ReadFromJsonAsync<JsonElement>(Json, ct);
            return doc.TryGetProperty("data", out var data) ? data : default;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            return default;   // PortCalls down — tool returns "unavailable"
        }
    }

    /// <summary>Fetch containers currently on a ship from Gate (REST).</summary>
    public async Task<JsonElement> ContainersOnShipAsync(int shipId, CancellationToken ct)
    {
        try
        {
            return await _gate.GetFromJsonAsync<JsonElement>($"containers?onShipId={shipId}", Json, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            return default;
        }
    }
}
