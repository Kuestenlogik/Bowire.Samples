// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Samples.Shared;
using Kuestenlogik.Bowire.Samples.SignalR.Hubs;
using Kuestenlogik.Bowire;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(HarborStore.CreateSeeded());
builder.Services.AddSignalR();
builder.Services.AddBowire();

var app = builder.Build();

app.MapHub<PortCallHub>("/hubs/port-calls");
app.MapBowire();

app.Run();
