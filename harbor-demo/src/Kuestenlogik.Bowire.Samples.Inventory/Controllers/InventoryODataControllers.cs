// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Samples.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Results;
using Microsoft.AspNetCore.OData.Routing.Controllers;

namespace Kuestenlogik.Bowire.Samples.Inventory.Controllers;

// OData controllers over Inventory's own docks + cranes. [EnableQuery] gives
// $filter / $orderby / $select / $expand / $count for free against the seed.

public sealed class DocksController(InventoryStore store) : ODataController
{
    [EnableQuery]
    public IQueryable<Dock> Get() => store.Docks.AsQueryable();

    [EnableQuery]
    public SingleResult<Dock> Get([FromRoute] int key)
        => SingleResult.Create(store.Docks.Where(d => d.Number == key).AsQueryable());
}

public sealed class CranesController(InventoryStore store) : ODataController
{
    [EnableQuery]
    public IQueryable<Crane> Get() => store.Cranes.AsQueryable();

    [EnableQuery]
    public SingleResult<Crane> Get([FromRoute] int key)
        => SingleResult.Create(store.Cranes.Where(c => c.Id == key).AsQueryable());
}
