# Harbor demo — microservices redesign (design record)

**Status:** Proposed · **Scope:** `Bowire.Samples/harbor-demo`

## Context

Today the harbor demo is a *monolith* story. `Combined` hosts every protocol
in one process against one mutable `HarborStore`, and each per-protocol sample
re-serves that same store over a single wire. So protocol choice teaches
nothing — any store over any protocol — and Bowire only ever "renders one
store nine ways."

The goal: a **realistic, complex harbor split into bounded-context
microservices, each speaking the protocol its domain part actually wants**, so
Bowire's real USP shows: **discovering and driving one heterogeneous *system*
across many wires** — and correlating a single business event across all of
them.

## Decision

Adopt a **bounded-context-per-service** decomposition (protocol follows
domain), discovered through Bowire's shipped catalogue-provider seam. Mainline
uses **only plugins shipped in 2.3.0**.

### Service map (core tier)

| Service | Bounded context | Protocol | Port | Headline ops / Bowire feature shown |
|---|---|---|---|---|
| **Fleet** | Vessel registry / master data (owns `Ship`, the `ShipId` other services key on) | gRPC (+ gRPC-Web) | 5150 | `GetShip`, `ListShips` (server-stream), register/amend — reflection discovery, all 4 call types, trailer auth, dual transport |
| **Inventory** | Physical assets — `Dock`, static `Crane` config | OData v4 | 5151 | `$filter/$orderby/$expand/$count` over docks+cranes — `$metadata` discovery + query-option builder |
| **Gate** | Container gate-in/out, `Container` lifecycle | REST / OpenAPI | 5152 | verbs + `201`/`409` `ProblemDetails` — OpenAPI discovery, try-it forms |
| **PortCalls** | Port-call orchestration aggregate (the saga) | GraphQL (BFF) | 5153 | `portCall(id)` resolvers fan out → ship (Fleet/gRPC) + dock/containers (Inventory/Gate); mutations = state machine; `portCallChanged` subscription |
| **Tracking** | AIS *ingress* — raw position frames from the antenna/simulator | WebSocket | 5154 | binary+text frames, sub-protocol negotiation — anticorruption edge |
| **Operations** | Operator console *egress* | SignalR | 5155 | groups (watch one fairway), streaming, user broadcast — consumes Tracking, re-emits |
| **Arrivals** | Public arrivals board (CQRS read-model) | SSE | 5156 | resumable `Last-Event-ID` over a bounded replay buffer |
| **Telemetry** | Equipment device bus — live `Crane` status + sensors | MQTT (embedded broker :1883) + AsyncAPI | 5157 | retained status, QoS1, LWT — topic-tree discovery; AsyncAPI doc for schema-driven discovery |
| **Assistant** | AI ops assistant | MCP (HTTP/SSE) | 5158 | `[McpServerTool]` over the services — AI-invocable surface |

**Extended tier (documented, not mainline):** **Customs** — SOAP / national
single-window (genuinely how customs works, and SOAP ships) · backbone
swap-ins (NATS / Pulsar when their plugins ship; Kafka / AMQP later).

### Core decisions

1. **Ownership by ID, private stores.** Delete the god `HarborStore` from
   `Shared`. `Shared` becomes **dependency-free contracts + enums + a pure
   `HarborSeed` constants/factory**. Each service owns a *private* in-memory
   store seeded from `HarborSeed`; cross-context references are **by id only**,
   so a read genuinely crosses the wire. Marquee teaching moment: the **`Crane`
   shared-kernel split** — same `CraneId`, static config owned by *Inventory*,
   live status owned by *Telemetry*.

2. **Telemetry is three edges, on purpose** (the sharpest contrast): raw AIS
   **ingress → WebSocket** (no framework on the far end) vs framework-rich
   operator **egress → SignalR** vs **device bus → MQTT**. WS-vs-SignalR
   becomes a deliberate teaching point, not three flat "live" services.

3. **CQRS seam:** write side = GraphQL mutations on `PortCalls`; public read
   side = SSE `Arrivals` projection with a bounded replay buffer so
   `Last-Event-ID` resume is demonstrable.

4. **Discovery rides shipped machinery — do not invent.** A thin
   **`Harbor.Gateway`** host does `AddBowire()` + `AddBowireCatalogue()` +
   `MapBowire("/bowire")` over a local `harbor-catalogue.json`
   (`LocalCatalogueProvider`); the Consul / HTTP / Kubernetes catalogue
   providers are the documented "grows into production" variants. The browser
   connects **directly to each origin** (no YARP required; single-origin proxy
   only as an optional convenience). Also keep the no-code
   `bowire --url grpc@… --url rest@… …` multi-target path, and let **every
   service also mount its own `/bowire`** for focused single-service study.

5. **The headline demo.** Bowire **recording/replay correlates one port call
   across gRPC + OData + GraphQL + SignalR + WebSocket + SSE + MQTT on a single
   timeline**, keyed by the shared `PortCall.Id` / `Ship.Id`. This is the USP
   money-shot — the redesign exists to make it possible.

## Constraints & discipline

- **Shipped plugins only** on the mainline (gRPC/-Web, REST, OData, GraphQL,
  SignalR, WebSocket, SSE, MQTT, MCP, AsyncAPI). Embedded `MqttServerFactory`
  (:1883) is the event backbone — no external brokers. Kafka / AMQP /
  TacticalApi / DIS are explicitly **out** until (and if) their plugins ship.
- `Shared` stays dependency-free; heavy deps (OData / HotChocolate / MQTTnet /
  gRPC / SOAP) live in each **service's own csproj**.
- **Deterministic seed + frozen-clock demo mode**; a CI test asserts
  cross-service seed consistency; **CI generates + verifies the wire schemas**
  (`.proto` / GraphQL SDL / AsyncAPI) from the kernel so they can't drift.
- **Port map 5150–5158 (+ MQTT 1883)** — avoids the existing 5101–5120 and
  5180–5199 bands.
- **Aspire AppHost** for one-command boot; each service independently runnable;
  **all wired into `Bowire.Samples.slnx`** (mandatory CI build). Update
  `docs/samples/index.md` and the roadmap board.
- Keep `Combined` compiling as the deliberate **"before" foil**.

## Phased plan (each phase independently runnable)

- **Phase 0 — contracts/seed split.** `Shared` → dependency-free contracts +
  `HarborSeed`. `Combined` still builds. Byte-identical screenshots.
- **Phase 1 — on-ramp.** `Fleet` (gRPC) + `Inventory` (OData) + `Harbor.Gateway`
  (catalogue) → Bowire discovers two services across two wires.
- **Phase 2 — cross-service reads.** `Gate` (REST) + `PortCalls` (GraphQL BFF)
  — one GraphQL query visibly resolves across gRPC + OData + REST.
- **Phase 3 — the live cascade.** `Tracking` (WS) + `Operations` (SignalR) +
  `Arrivals` (SSE).
- **Phase 4 — device bus + AI.** `Telemetry` (MQTT + AsyncAPI) + `Assistant`
  (MCP).
- **Phase 5 — the money-shot.** Recording/replay correlating one port call
  across all wires; Aspire AppHost; docs.
- **Optional.** `Customs` (SOAP); backbone swap-ins.

## Consequences

- The harbor demo becomes a believable *system* whose protocol map is an
  argument, not a re-skin — and the recording/replay correlation gives the
  marketing site its strongest single screenshot.
- More moving parts than the monolith; mitigated by the phased, always-runnable
  increments and the Aspire one-command boot.
- Per-protocol "hello-world" minidemos move to each protocol's own repo (see the
  org samples policy); `harbor-demo` keeps only this cohesive system.

---

*Design synthesized from a 3-lens design panel (DDD bounded-context ·
protocol-strength showcase · realistic TOS/C4I). Winner: bounded-context
spine; grafted the TOS "correlated replay" headline + SOAP-customs + CI schema
generation, and the DDD telemetry-edge split + Crane shared-kernel + CQRS/SSE
replay.*
