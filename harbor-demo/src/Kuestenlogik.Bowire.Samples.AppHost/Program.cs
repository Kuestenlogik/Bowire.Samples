// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

// One-command boot for the whole harbor microservices landscape:
//
//   dotnet run --project harbor-demo/src/Kuestenlogik.Bowire.Samples.AppHost
//
// Aspire starts every service (each binds its fixed catalogue port from its
// own appsettings — 5150..5158 + MQTT 1883 + the gateway on 5159) and gives
// you the dashboard for logs/traces. WaitFor models the dependency edges:
// the BFF fans out to Fleet/Inventory/Gate, Operations consumes Tracking,
// the Assistant fronts PortCalls/Gate, and the Gateway is the workbench over
// everything. Open https://localhost:5159/bowire once it's up.

var builder = DistributedApplication.CreateBuilder(args);

var fleet = builder.AddProject<Projects.Kuestenlogik_Bowire_Samples_Fleet>("fleet");
var inventory = builder.AddProject<Projects.Kuestenlogik_Bowire_Samples_Inventory>("inventory");
var gate = builder.AddProject<Projects.Kuestenlogik_Bowire_Samples_Gate>("gate");

var portCalls = builder.AddProject<Projects.Kuestenlogik_Bowire_Samples_PortCalls>("portcalls")
    .WaitFor(fleet).WaitFor(inventory).WaitFor(gate);

var tracking = builder.AddProject<Projects.Kuestenlogik_Bowire_Samples_Tracking>("tracking");
builder.AddProject<Projects.Kuestenlogik_Bowire_Samples_Operations>("operations")
    .WaitFor(tracking);
builder.AddProject<Projects.Kuestenlogik_Bowire_Samples_Arrivals>("arrivals");

builder.AddProject<Projects.Kuestenlogik_Bowire_Samples_Telemetry>("telemetry");
builder.AddProject<Projects.Kuestenlogik_Bowire_Samples_Assistant>("assistant")
    .WaitFor(portCalls).WaitFor(gate);

builder.AddProject<Projects.Kuestenlogik_Bowire_Samples_Gateway>("gateway")
    .WaitFor(portCalls);

builder.Build().Run();
