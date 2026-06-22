// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Samples.Shared;
using Kuestenlogik.Bowire;

// Isolated REST sample — every HTTP verb on the port-calls resource,
// plus OpenAPI schema, multipart file upload, query-parameter filters,
// and ProblemDetails for 4xx responses.

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(HarborStore.CreateSeeded());
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// AddBowire picks up Kuestenlogik.Bowire.Protocol.Rest from the csproj and wires
// up OpenAPI-based discovery (and, in embedded mode, ApiExplorer
// metadata scanning too).
builder.Services.AddBowire();

var app = builder.Build();

// Expose the OpenAPI document at /openapi/v1.json. The REST plugin reads
// this endpoint to enumerate paths and produce method entries in the
// Bowire sidebar.
app.MapOpenApi();

app.MapControllers();
app.MapBowire();

app.Run();
