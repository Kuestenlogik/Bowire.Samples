# Kuestenlogik.Bowire.Samples.Vulnerable

> **INTENTIONALLY VULNERABLE BY DESIGN — DO NOT DEPLOY.**
> This sample is the canonical learning-target for the `bowire scan` subcommand and the upcoming `kuestenlogik/bowire-vulndb` CI validation harness. Bind it to `localhost` only and shut it down when you're done.

Hosts an ASP.NET Core 10 web app on `https://localhost:5140` that is misconfigured in every way the built-in scanner checks for, plus every way the three seed templates in `Bowire/docs/security/examples/` detect. Running `bowire scan` against it should produce a finding-per-misconfig, which lets the security-testing-lane docs and (later) the vulndb-corpus CI gate point at a deterministic reproducer instead of a hand-rolled mock.

## Seeded vulnerabilities

| # | Misconfig | Scanner finding id | Where it lives in `Program.cs` |
|---|---|---|---|
| 1 | No CSP / HSTS / X-Frame-Options / X-Content-Type-Options | `BWR-REST-001` (from `rest-missing-security-headers.json`) | Absence of any security-headers middleware |
| 2 | GraphQL `__schema` introspection enabled | `BWR-GRAPHQL-001` (from `graphql-introspection.json`) | `AlwaysAllowIntrospectionInterceptor` registered via `AddHttpRequestInterceptor<...>` |
| 3 | gRPC Server Reflection enabled | `BWR-GRPC-001` (from `grpc-server-reflection.json`) | `AddGrpcReflection()` + `MapGrpcReflectionService()` |
| 4a | `X-Powered-By: ASP.NET` on every response | `BWR-BUILTIN-BANNER-XPOWEREDBY` | Header-stamping middleware |
| 4b | `X-AspNet-Version: 10.0.0` on every response | `BWR-BUILTIN-BANNER-XASPNETVERSION` | Same middleware |
| 5 | Always-on developer exception page + `/oops` thrower | `BWR-BUILTIN-ERROR-001` | `UseDeveloperExceptionPage()` + `MapGet("/oops", () => throw …)` |
| 6a | Kestrel accepts TLS 1.0 | `BWR-BUILTIN-TLS-TLS10` | `ConfigureKestrel(... SslProtocols = Tls\|Tls11\|Tls12\|Tls13)` |
| 6b | Kestrel accepts TLS 1.1 | `BWR-BUILTIN-TLS-TLS11` | Same |
| 7 | SignalR hub negotiate reachable anonymously | `BWR-SIGNALR-001` (from `signalr-anonymous-negotiate.json`) | `AddSignalR()` + `MapHub<ProbeHub>("/hubs/probe")` with no `.RequireAuthorization()` |
| 8 | OData `$metadata` (CSDL) reachable without auth | `BWR-ODATA-001` (from `odata-metadata-exposed.json`) | Hand-rolled `MapGet("/$metadata", …)` returning the edmx envelope |
| 9 | OData entity readable without object-level authz (BOLA/IDOR) | `BWR-ODATA-002` (from `odata-entity-object-idor.json`) | `MapGet("/odata/Orders({id:int})", …)` returns any order (incl. another owner's) with no ownership check |
| 10 | Permissive CORS: reflected Origin + `Allow-Credentials: true` | `BWR-REST-003` (from `permissive-cors.json`) | Middleware echoing `Origin` into `Access-Control-Allow-Origin` with credentials enabled |
| 11 | Socket.IO handshake open to anonymous callers | `BWR-SOCKETIO-001` (from `socketio-anonymous-handshake.json`) | Hand-rolled `MapGet("/socket.io/", …)` returning the Engine.IO open packet |
| 12 | MCP server answers `tools/list` without auth | `BWR-MCP-001` (from `mcp-anonymous-tools-list.json`) | `MapPost("/mcp", …)` returns a JSON-RPC tools/list result to any caller |
| 13 | Server-Sent Events stream open to anonymous callers | `BWR-SSE-001` (from `sse-unauthenticated-stream.json`) | `MapGet("/events", …)` streams `text/event-stream` with a `data:` frame, no auth |
| 14 | Open redirect via unvalidated `?url=` | `BWR-REST-004` (from `open-redirect.json`) | `MapGet("/redirect", (url) => Results.Redirect(url))` — no allow-list |
| 15 | Verbose OData error leaks parser type + stack trace | `BWR-ODATA-003` (from `odata-verbose-error.json`) | `MapGet("/odata/Orders", …)` returns a detailed `ODataException` on a malformed `$filter` |

## Run it

```powershell
dotnet run --project src/Kuestenlogik.Bowire.Samples.Vulnerable
```

The app listens on `https://localhost:5140`. The dev cert from `dotnet dev-certs https --trust` is used by default, so `bowire scan` needs `--allow-self-signed-certs` if you haven't trusted it.

## Scan it

```powershell
bowire scan --target https://localhost:5140 --corpus docs/security/examples --allow-self-signed-certs
```

(Run from the main Bowire repo root so the relative `--corpus` path resolves; or pass an absolute path.)

### Expected output

`bowire scan` should report roughly **8 findings**, grouped by rule id:

- 1× `BWR-REST-001` (missing security headers)
- 1× `BWR-GRAPHQL-001` (introspection)
- 1× `BWR-GRPC-001` (reflection)
- 1× `BWR-BUILTIN-BANNER-XPOWEREDBY` (low severity)
- 1× `BWR-BUILTIN-BANNER-XASPNETVERSION` (medium severity)
- 1× `BWR-BUILTIN-ERROR-001` (verbose error, medium severity)
- 2× `BWR-BUILTIN-TLS-TLS10` / `BWR-BUILTIN-TLS-TLS11` (high severity — see platform note below)

Plus info-only findings for TLS 1.2 / 1.3 acceptance.

To emit SARIF for a CI dashboard:

```powershell
bowire scan --target https://localhost:5140 --corpus docs/security/examples --allow-self-signed-certs --out vulnerable-sample-findings.sarif
```

## Platform notes — TLS 1.0 / 1.1

Modern Windows 11 and current OpenSSL builds disable TLS 1.0 / 1.1 at the operating-system / cryptographic-stack layer (per Microsoft's IIS/SChannel hardening defaults and OpenSSL 3.0+ provider config). On those platforms, Kestrel's `SslProtocols` request for `Tls | Tls11` is silently dropped during the handshake and the server will respond only on TLS 1.2 / 1.3 regardless of this sample's config.

In that scenario `bowire scan` will:

- Skip the TLS 1.0 / 1.1 findings (handshake refused at the OS layer).
- Still emit every other finding above.

To exercise the TLS 1.0 / 1.1 findings end-to-end, run the sample on an OS / container where the legacy ciphers are still in the SChannel / OpenSSL allowlist (older Windows Server LTSC images, or a custom OpenSSL build with the `legacy` provider enabled).

## What this sample is NOT

- **Not** a copy-paste starter. The `Kuestenlogik.Bowire.Samples.Rest`, `.Grpc`, `.GraphQL` projects are the safe starters.
- **Not** a Bowire-instrumented service. It doesn't reference `Kuestenlogik.Bowire` packages because the point is the external `bowire scan` subcommand probing it from the outside.
- **Not** a comprehensive vuln list. The corpus is meant to grow in `kuestenlogik/bowire-vulndb`; this sample only covers what the current builtin checks + seed templates detect.
