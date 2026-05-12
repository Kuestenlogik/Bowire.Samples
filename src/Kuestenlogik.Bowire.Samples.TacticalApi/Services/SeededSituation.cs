// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Google.Protobuf.WellKnownTypes;
using Rheinmetall.TacticalApi.V0;

namespace Kuestenlogik.Bowire.Samples.TacticalApi.Services;

/// <summary>
/// Builds the in-memory situation seed served by <see cref="SituationServiceImpl"/>:
/// three MIL-2525C symbols spread along the German North Sea coast. The shapes
/// (Identity → CreationMetaData → DataProperty wrappers → SymbolLocation/Point/GeoPoint)
/// follow exactly the pattern Rheinmetall's testclient program uses, so the JSON
/// the Bowire workbench renders matches what a real TacNet response would look
/// like on the wire — just with seeded values instead of a live C4I store.
/// </summary>
internal static class SeededSituation
{
    public static Dictionary<string, SituationObject> Build()
    {
        var now = Timestamp.FromDateTime(DateTime.UtcNow);
        var reporter = new Identity { StringIdentity = "TacticalAPI.Sample" };

        var result = new Dictionary<string, SituationObject>(StringComparer.Ordinal);
        Add(result, BuildSymbol(
            uuid: "ce4a51f0-3e30-4f6c-b32c-2b48d4a35b1a",
            symbolCode: "SFGPUCI-----***", // Friend ground unit, infantry
            name: "1st Infantry Section",
            latitude: 53.8635, longitude: 8.7066,           // Cuxhaven
            reporter: reporter, now: now));
        Add(result, BuildSymbol(
            uuid: "f5b3e2a6-9d27-4d4f-93c9-1e7b9f4d0c52",
            symbolCode: "SHGPEWA-----***", // Hostile equipment, armored
            name: "Hostile Armored Recon",
            latitude: 53.5396, longitude: 8.5809,           // Bremerhaven
            reporter: reporter, now: now));
        Add(result, BuildSymbol(
            uuid: "9d1f2e0b-c2d4-4a31-89e0-1aef8a8e6021",
            symbolCode: "SNGPUCRRO---***", // Neutral ground unit, recon, observation
            name: "Coastal Observation Post",
            latitude: 53.5189, longitude: 8.1078,           // Wilhelmshaven
            reporter: reporter, now: now));
        return result;
    }

    private static void Add(Dictionary<string, SituationObject> dict, (string id, SituationObject obj) entry)
        => dict[entry.id] = entry.obj;

    private static (string id, SituationObject obj) BuildSymbol(
        string uuid, string symbolCode, string name,
        double latitude, double longitude,
        Identity reporter, Timestamp now)
    {
        var identity = new Identity { UuidIdentity = uuid };
        var creationMeta = new CreationMetaData
        {
            CreationTime = now,
            CreatorIdentity = reporter,
        };
        var symbol = new Symbol
        {
            Identity = identity,
            CreationMetaData = creationMeta,
            Name = new DataPropertyString
            {
                CreationMetaData = creationMeta,
                Content = name,
            },
            SymbolIdentifier = new DataPropertySymbolIdentifier
            {
                CreationMetaData = creationMeta,
                Content = new SymbolIdentifier
                {
                    SymbolCatalog = SymbolCatalog.Mil2525C,
                    StringIdentifier = symbolCode,
                },
            },
            Location = new DataPropertyLocation
            {
                CreationMetaData = creationMeta,
                Content = new SymbolLocation
                {
                    Point = new Point
                    {
                        LocationTime = now,
                        GeoPoint = new GeoPoint
                        {
                            LatitudeCoordinate = latitude,
                            LongitudeCoordinate = longitude,
                        },
                    },
                },
            },
        };
        return (uuid, new SituationObject { Symbol = symbol });
    }
}
