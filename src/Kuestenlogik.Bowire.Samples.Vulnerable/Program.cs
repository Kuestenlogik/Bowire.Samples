// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using System.Security.Authentication;
using HotChocolate.AspNetCore;
using HotChocolate.Execution;
using Kuestenlogik.Bowire.Samples.Vulnerable.Services;

// ============================================================================
// INTENTIONALLY VULNERABLE BY DESIGN.
//
// This is the canonical learning-target for the `bowire scan` subcommand
// and (in the future) the CI-validation harness for the
// kuestenlogik/bowire-vulndb corpus. Every misconfig below is deliberate
// and corresponds to one or more scanner findings — see README.md next
// to this Program.cs for the full inventory.
//
// DO NOT expose this beyond localhost. DO NOT copy these patterns into a
// real service.
// ============================================================================

var builder = WebApplication.CreateBuilder(args);

// ----------------------------------------------------------------------------
// [Finding #6] BWR-BUILTIN-TLS-* — enable deprecated TLS 1.0 / 1.1 on Kestrel.
//
// The scanner enumerates which SSL/TLS protocol versions the target
// accepts and emits findings for handshake acceptance on TLS 1.0 / 1.1.
// We hardcode every version including the deprecated ones; on modern
// Windows / OpenSSL builds the OS may refuse TLS 1.0 / 1.1 at the
// platform level regardless of this setting — in that case the scanner
// will simply report TLS 1.2 / 1.3 as the only accepted protocols. See
// README "Platform notes" for the workaround.
// ----------------------------------------------------------------------------
builder.WebHost.ConfigureKestrel(opts =>
{
    opts.ConfigureHttpsDefaults(httpsOpts =>
    {
#pragma warning disable CA5364, CA5386, CA5397, SYSLIB0039 // intentional — see header comment
        httpsOpts.SslProtocols =
            SslProtocols.Tls
            | SslProtocols.Tls11
            | SslProtocols.Tls12
            | SslProtocols.Tls13;
#pragma warning restore CA5364, CA5386, CA5397, SYSLIB0039
    });
});

// Listen on the agreed-upon port for the vulnerable sample. Port 5140 is
// free across the samples solution (existing samples occupy 5101 + 5110..5120).
builder.WebHost.UseUrls("https://localhost:5140");

// ----------------------------------------------------------------------------
// [Finding #2] BWR-GRAPHQL-001 — GraphQL with introspection enabled.
//
// HotChocolate 15+ blocks `__schema` introspection by default unless an
// HTTP request interceptor explicitly allows it. We install one here
// (`AlwaysAllowIntrospectionInterceptor`) so the sample exposes the
// schema map unconditionally — exactly the production misconfig the
// graphql-introspection.json template detects.
// ----------------------------------------------------------------------------
builder.Services
    .AddGraphQLServer()
    .AddQueryType<TrivialQuery>()
    .AddHttpRequestInterceptor<AlwaysAllowIntrospectionInterceptor>();

// ----------------------------------------------------------------------------
// [Finding #3] BWR-GRPC-001 — gRPC + Server Reflection enabled.
//
// AddGrpcReflection registers the well-known
// `grpc.reflection.v1alpha.ServerReflection` service that the
// grpc-server-reflection.json template probes for.
// ----------------------------------------------------------------------------
builder.Services.AddGrpc();
builder.Services.AddGrpcReflection();

var app = builder.Build();

// ----------------------------------------------------------------------------
// [Finding #5] BWR-BUILTIN-ERROR-001 — verbose developer exception page.
//
// UseDeveloperExceptionPage is normally env-gated to Development. We
// install it unconditionally, AND map an /oops endpoint that throws,
// so the scanner's verbose-error probes (the three random-404 / null-
// byte / malformed-header probes in SecurityBuiltins.cs) will catch
// the leaked stack trace.
// ----------------------------------------------------------------------------
app.UseDeveloperExceptionPage();

// ----------------------------------------------------------------------------
// [Finding #4] BWR-BUILTIN-BANNER-XPOWEREDBY + BWR-BUILTIN-BANNER-XASPNETVERSION
//
// Stamp the canonical version-disclosing headers on every response.
// The scanner's banner-disclosure check enumerates Server / X-Powered-By
// / X-AspNet-Version / X-AspNetMvc-Version / Via and emits one finding
// per disclosed header.
// ----------------------------------------------------------------------------
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Powered-By"]     = "ASP.NET";
    ctx.Response.Headers["X-AspNet-Version"] = "10.0.0";
    await next();
});

// ----------------------------------------------------------------------------
// [Finding #1] BWR-REST-001 — missing security-headers middleware.
//
// Note the deliberate ABSENCE here: no CSP, no HSTS, no X-Frame-Options,
// no X-Content-Type-Options. The rest-missing-security-headers.json
// template GETs / and asserts those four headers are present; with
// none of them wired, the predicate fires.
// ----------------------------------------------------------------------------

// Map the GraphQL endpoint that exposes the introspectable schema.
app.MapGraphQL("/graphql");

// Map the gRPC service + the reflection service the scanner enumerates.
app.MapGrpcService<ProbeGrpcService>();
app.MapGrpcReflectionService();

// Plain GET / so the scanner has something to probe for the security-
// headers + banner checks. Returning a tiny string keeps the response
// boring on purpose.
app.MapGet("/", () => "Intentionally vulnerable sample — see README.md.");

// /oops: always throws. The unconditional UseDeveloperExceptionPage
// above turns the exception into an HTML page with a stack trace, which
// matches the verbose-error regex set (System.*Exception, `at *.cs:line
// N`, <pre>at <namespace>...). Belt-and-braces alongside the random-
// path probes the scanner runs.
app.MapGet("/oops", () =>
{
    throw new InvalidOperationException("simulated production crash");
});

app.Run();

// ----------------------------------------------------------------------------
// HotChocolate request interceptor that unconditionally allows
// introspection. See the AddGraphQLServer wiring above for context.
// ----------------------------------------------------------------------------
internal sealed class AlwaysAllowIntrospectionInterceptor : DefaultHttpRequestInterceptor
{
    public override ValueTask OnCreateAsync(
        HttpContext context,
        IRequestExecutor requestExecutor,
        OperationRequestBuilder requestBuilder,
        CancellationToken cancellationToken)
    {
        requestBuilder.AllowIntrospection();
        return base.OnCreateAsync(context, requestExecutor, requestBuilder, cancellationToken);
    }
}

// ----------------------------------------------------------------------------
// Trivial GraphQL schema. A single field is enough to make `__schema`
// return a populated `types` array, which is what the introspection
// template's `vulnerableWhen` predicate looks for.
// ----------------------------------------------------------------------------
public sealed class TrivialQuery
{
    public string Hello => "world";
}
