// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Grpc.Core;
using Kuestenlogik.Bowire.Samples.Vulnerable.Protos;

namespace Kuestenlogik.Bowire.Samples.Vulnerable.Services;

/// <summary>
/// Trivial single-RPC gRPC service. Only exists so the sample has at
/// least one service in the reflection catalogue for
/// <c>bowire scan</c> to enumerate via Server Reflection.
/// </summary>
public sealed class ProbeGrpcService : ProbeService.ProbeServiceBase
{
    public override Task<PingReply> Ping(PingRequest request, ServerCallContext context)
        => Task.FromResult(new PingReply { Echo = request.Note ?? string.Empty });
}
