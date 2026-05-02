// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Samples.Shared;
using Microsoft.AspNetCore.OData;
using Microsoft.OData.ModelBuilder;

// OData is discovered externally: Bowire reads the service's
// $metadata document (CSDL/XML). This sample only hosts OData;
// browse it with a standalone Bowire:
//   bowire --url https://localhost:5116/odata/$metadata

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(HarborStore.CreateSeeded());

// Build the EDM model — ships, docks, cranes, port calls. Every entity
// set gets $filter / $expand / $select / $orderby / $top / $skip for
// free once we opt in via AddOData below.
var edm = new ODataConventionModelBuilder();
edm.EntitySet<Ship>("Ships");
edm.EntitySet<Dock>("Docks").EntityType.HasKey(d => d.Number);
edm.EntitySet<Crane>("Cranes");
edm.EntitySet<PortCall>("PortCalls");
edm.EntitySet<Container>("Containers").EntityType.HasKey(c => c.Id);

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
app.Run();
