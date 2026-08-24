using System.Runtime.CompilerServices;
using TruckNav.Connector.Core.Interfaces;
using TruckNav.Connector.Core.Models;

namespace TruckNav.Connector.Navigation.Routing;

public sealed class ExactDestinationResolver : IDestinationResolver
{
    private readonly ConditionalWeakTable<RoadGraph, DestinationLookups> _lookups = new();

    public ResolvedDestination? Resolve(TelemetrySnapshot telemetry, RoadGraph graph)
    {
        if (!telemetry.HasNavigationTarget)
        {
            return null;
        }

        var lookups = _lookups.GetValue(graph, static value => new DestinationLookups(value.Destinations));
        var pickup = telemetry.IsNavigatingToPickup;
        var target = pickup
            ? (CityId: telemetry.SourceCityId, CityName: telemetry.SourceCityName, CompanyId: telemetry.SourceCompanyId, CompanyName: telemetry.SourceCompanyName)
            : (CityId: telemetry.DestinationCityId, CityName: telemetry.DestinationCityName, CompanyId: telemetry.DestinationCompanyId, CompanyName: telemetry.DestinationCompanyName);
        var cityId = Normalize(target.CityId);
        var companyId = Normalize(target.CompanyId);
        if (cityId is not null && companyId is not null &&
            lookups.ByToken.TryGetValue((cityId, companyId), out var tokenMatches))
        {
            return ToResolved(ChooseStable(tokenMatches), pickup);
        }

        var cityName = Normalize(target.CityName);
        var companyName = Normalize(target.CompanyName);
        if (cityName is null || companyName is null ||
            !lookups.ByDisplayName.TryGetValue((cityName, companyName), out var nameMatches) ||
            nameMatches.Count != 1)
        {
            return null;
        }

        return ToResolved(nameMatches[0], pickup);
    }

    private static RoadDestination ChooseStable(IReadOnlyList<RoadDestination> matches) =>
        matches.OrderBy(item => item.NodeIndex).First();

    private static ResolvedDestination ToResolved(RoadDestination destination, bool pickup)
    {
        var city = destination.CityName ?? destination.CityToken;
        var company = destination.CompanyName ?? destination.CompanyToken;
        return new ResolvedDestination(
            destination.CityToken,
            destination.CompanyToken,
            pickup ? $"Pickup: {company}, {city}" : $"{company}, {city}",
            destination.NodeIndex,
            destination.Position);
    }

    private static string? Normalize(string? value)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private sealed class DestinationLookups
    {
        public DestinationLookups(IReadOnlyList<RoadDestination> destinations)
        {
            foreach (var destination in destinations)
            {
                Add(ByToken, (Normalize(destination.CityToken)!, Normalize(destination.CompanyToken)!), destination);
                var cityName = Normalize(destination.CityName);
                var companyName = Normalize(destination.CompanyName);
                if (cityName is not null && companyName is not null)
                {
                    Add(ByDisplayName, (cityName, companyName), destination);
                }
            }
        }

        public Dictionary<(string City, string Company), List<RoadDestination>> ByToken { get; } = [];

        public Dictionary<(string City, string Company), List<RoadDestination>> ByDisplayName { get; } = [];

        private static void Add(
            Dictionary<(string City, string Company), List<RoadDestination>> dictionary,
            (string City, string Company) key,
            RoadDestination destination)
        {
            if (!dictionary.TryGetValue(key, out var entries))
            {
                entries = [];
                dictionary.Add(key, entries);
            }

            entries.Add(destination);
        }
    }
}
