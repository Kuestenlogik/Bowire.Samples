// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Samples.Inventory;
using Kuestenlogik.Bowire.Samples.Shared;
using Microsoft.AspNetCore.OData;
using Microsoft.OData.ModelBuilder;

// Inventory — physical harbor assets (docks + static crane config) over
// OData v4. A pure server; the Harbor.Gateway discovers it via the catalogue,
// or point a standalone workbench at odata@https://localhost:5151/odata.

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<InventoryStore>();

// EDM model — this service owns only Docks + Cranes (its bounded context).
var edm = new ODataConventionModelBuilder();
edm.EntitySet<Dock>("Docks").EntityType.HasKey(d => d.Number);
edm.EntitySet<Crane>("Cranes");

builder.Services.AddControllers().AddOData(opt => opt
    .Select()
    .Filter()
    .OrderBy()
    .Expand()
    .Count()
    .SetMaxTop(100)
    .AddRouteComponents("odata", edm.GetEdmModel())
);

var app = builder.Build();

app.UseRouting();
app.MapControllers();
app.MapGet("/", () => Results.Redirect("/odata/$metadata"));

app.Run();
