// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Samples.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Results;
using Microsoft.AspNetCore.OData.Routing.Controllers;

namespace Kuestenlogik.Bowire.Samples.OData.Controllers;

// Each OData controller returns IQueryable<T> and wears [EnableQuery]
// so the framework parses $filter / $expand / $select and runs them
// against the source. All controllers share the seeded HarborStore.

public sealed class ShipsController(HarborStore store) : ODataController
{
    [EnableQuery(MaxExpansionDepth = 4)]
    public IQueryable<Ship> Get() => store.Ships.Values.AsQueryable();

    [EnableQuery]
    public SingleResult<Ship> Get([FromRoute] int key)
        => SingleResult.Create(store.Ships.Values.Where(s => s.Id == key).AsQueryable());
}

public sealed class DocksController(HarborStore store) : ODataController
{
    [EnableQuery]
    public IQueryable<Dock> Get() => store.Docks.Values.AsQueryable();

    [EnableQuery]
    public SingleResult<Dock> Get([FromRoute] int key)
        => SingleResult.Create(store.Docks.Values.Where(d => d.Number == key).AsQueryable());
}

public sealed class CranesController(HarborStore store) : ODataController
{
    [EnableQuery]
    public IQueryable<Crane> Get() => store.Cranes.Values.AsQueryable();
}

public sealed class PortCallsController(HarborStore store) : ODataController
{
    [EnableQuery(MaxExpansionDepth = 4)]
    public IQueryable<PortCall> Get() => store.PortCalls.Values.AsQueryable();

    [EnableQuery]
    public SingleResult<PortCall> Get([FromRoute] int key)
        => SingleResult.Create(store.PortCalls.Values.Where(pc => pc.Id == key).AsQueryable());
}

public sealed class ContainersController(HarborStore store) : ODataController
{
    [EnableQuery]
    public IQueryable<Container> Get() => store.Containers.Values.AsQueryable();
}
