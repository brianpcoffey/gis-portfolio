using Microsoft.Extensions.Logging;
using Portfolio.Common.DTOs;
using Portfolio.Common.Models;
using Portfolio.Repositories.Interfaces;
using Portfolio.Services.Interfaces;
using Portfolio.Services.Native;

namespace Portfolio.Services.Services;

public class HomeScoringService : IHomeScoringService
{
    private readonly IPropertyRepository _propertyRepo;
    private readonly ILogger<HomeScoringService> _logger;
    private const decimal AnnualInterestRate = 0.065m;
    private const int LoanTermMonths = 360; // 30 years

    public HomeScoringService(
        IPropertyRepository propertyRepo,
        ILogger<HomeScoringService> logger)
    {
        _propertyRepo = propertyRepo;
        _logger = logger;
        NativeScoringBridge.LogAvailability(_logger);
    }

    public async Task<List<ScoredPropertyDto>> GetTopPropertiesAsync(
        HomeSearchPreferencesDto prefs,
        int top = 10,
        CancellationToken cancellationToken = default)
    {
        // 1. Load and apply hard filters via repository
        var properties = await _propertyRepo.GetFilteredAsync(
            prefs.MaxPrice,
            prefs.MinBedrooms,
            prefs.MinBathrooms,
            prefs.MinSqft,
            prefs.MaxCommuteMin,
            cancellationToken);

        if (properties.Count == 0)
            return [];

        // 2. Native fast path: delegate entire scoring loop to the C++ kernel.
        if (NativeScoringBridge.IsAvailable)
            return ScoreWithNativeKernel(properties, prefs, top);

        // 3. Managed fallback: normalize weights and score in-process.
        var totalWeight = prefs.WeightAffordability + prefs.WeightNeighborhood
            + prefs.WeightSize + prefs.WeightAppreciation + prefs.WeightCondition
            + prefs.WeightCommute + prefs.WeightAmenities + prefs.WeightTaxUtilities
            + prefs.WeightResale + prefs.WeightEnvironment;

        if (totalWeight <= 0) totalWeight = 1.0;

        double Norm(double w) => w / totalWeight;

        // Score each property
        var scored = properties.Select(p =>
        {
            var monthlyCost = EstimateMonthlyCost(p);

            var affordability = ScoreAffordability(monthlyCost, prefs.MaxMonthlyBudget);
            var neighborhood = ScoreNeighborhood(p);
            var size = ScoreSize(p, prefs);
            var appreciation = Clamp(p.FutureAppreciation);
            var condition = ScoreCondition(p);
            var commute = ScoreCommute(p.CommuteMin, prefs.MaxCommuteMin);
            var amenities = Clamp(p.AmenitiesScore);
            var taxUtil = ScoreTaxUtilities(p);
            var resale = Clamp(p.ResalePotential);
            var environment = ScoreEnvironment(p);

            var composite =
                Norm(prefs.WeightAffordability) * affordability
              + Norm(prefs.WeightNeighborhood) * neighborhood
              + Norm(prefs.WeightSize) * size
              + Norm(prefs.WeightAppreciation) * appreciation
              + Norm(prefs.WeightCondition) * condition
              + Norm(prefs.WeightCommute) * commute
              + Norm(prefs.WeightAmenities) * amenities
              + Norm(prefs.WeightTaxUtilities) * taxUtil
              + Norm(prefs.WeightResale) * resale
              + Norm(prefs.WeightEnvironment) * environment;

            return new ScoredPropertyDto
            {
                PropertyId = p.Id,
                Street = p.Street,
                City = p.City,
                ZipCode = p.ZipCode,
                Price = p.Price,
                Bedrooms = p.Bedrooms,
                Bathrooms = p.Bathrooms,
                LotSqft = p.LotSqft,
                Latitude = p.Latitude,
                Longitude = p.Longitude,
                EstimatedMonthlyCost = monthlyCost,
                AffordabilityScore = affordability,
                NeighborhoodScore = neighborhood,
                SizeScore = size,
                AppreciationScore = appreciation,
                ConditionScore = condition,
                CommuteScore = commute,
                AmenitiesScore = amenities,
                TaxUtilitiesScore = taxUtil,
                ResaleScore = resale,
                EnvironmentScore = environment,
                CompositeScore = Math.Round(composite, 2)
            };
        })
        .OrderByDescending(s => s.CompositeScore)
        .Take(top)
        .ToList();

        // Assign rank
        for (int i = 0; i < scored.Count; i++)
            scored[i].Rank = i + 1;

        return scored;
    }

    // Uses the native C++ scoring kernel to score all properties in one batch call,
    // then maps the output array to ScoredPropertyDto, sorts, and returns the top-N.
    private static List<ScoredPropertyDto> ScoreWithNativeKernel(
        IReadOnlyList<Property> properties,
        HomeSearchPreferencesDto prefs,
        int top)
    {
        var outputs = NativeScoringBridge.ScorePropertyBatch(properties, prefs);

        var scored = new List<ScoredPropertyDto>(properties.Count);
        for (var i = 0; i < properties.Count; i++)
        {
            var p   = properties[i];
            var out_ = outputs[i];
            scored.Add(new ScoredPropertyDto
            {
                PropertyId           = p.Id,
                Street               = p.Street,
                City                 = p.City,
                ZipCode              = p.ZipCode,
                Price                = p.Price,
                Bedrooms             = p.Bedrooms,
                Bathrooms            = p.Bathrooms,
                LotSqft              = p.LotSqft,
                Latitude             = p.Latitude,
                Longitude            = p.Longitude,
                EstimatedMonthlyCost = (decimal)Math.Round(out_.EstimatedMonthlyCost, 2),
                AffordabilityScore   = out_.Affordability,
                NeighborhoodScore    = out_.Neighborhood,
                SizeScore            = out_.Size,
                AppreciationScore    = out_.Appreciation,
                ConditionScore       = out_.Condition,
                CommuteScore         = out_.Commute,
                AmenitiesScore       = out_.Amenities,
                TaxUtilitiesScore    = out_.TaxUtilities,
                ResaleScore          = out_.Resale,
                EnvironmentScore     = out_.Environment,
                CompositeScore       = Math.Round(out_.Composite, 2)
            });
        }

        scored.Sort((a, b) => b.CompositeScore.CompareTo(a.CompositeScore));
        var result = scored.Take(top).ToList();
        for (var i = 0; i < result.Count; i++)
            result[i].Rank = i + 1;

        return result;
    }

    // Gets a single property by ID and maps it to a ScoredPropertyDto (unscored, rank 0).
    public async Task<ScoredPropertyDto?> GetPropertyByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var p = await _propertyRepo.GetByIdAsync(id, cancellationToken);
        if (p is null) return null;

        var monthlyCost = EstimateMonthlyCost(p);
        return new ScoredPropertyDto
        {
            PropertyId = p.Id,
            Street = p.Street,
            City = p.City,
            ZipCode = p.ZipCode,
            Price = p.Price,
            Bedrooms = p.Bedrooms,
            Bathrooms = p.Bathrooms,
            LotSqft = p.LotSqft,
            Latitude = p.Latitude,
            Longitude = p.Longitude,
            EstimatedMonthlyCost = monthlyCost
        };
    }

    // --- Sub-score methods ---

    private static decimal EstimateMonthlyCost(Property p)
    {
        var monthlyRate = AnnualInterestRate / 12m;
        var loanAmount = p.Price * 0.80m; // 20% down
        decimal mortgage;
        if (monthlyRate > 0)
        {
            var factor = (double)monthlyRate * Math.Pow(1 + (double)monthlyRate, LoanTermMonths)
                       / (Math.Pow(1 + (double)monthlyRate, LoanTermMonths) - 1);
            mortgage = loanAmount * (decimal)factor;
        }
        else
        {
            mortgage = loanAmount / LoanTermMonths;
        }

        return Math.Round(mortgage + (p.PropertyTax / 12m) + p.HoaFee + p.Utilities, 2);
    }

    private static double ScoreAffordability(decimal monthlyCost, decimal budget)
    {
        if (budget <= 0) return 50;
        var ratio = (double)(monthlyCost / budget);
        return Clamp((1.0 - ratio) * 100);
    }

    private static double ScoreNeighborhood(Property p)
    {
        return Clamp((p.SchoolRating + p.Walkability + p.TransitAccess + (100 - p.CrimeScore)) / 4.0);
    }

    private static double ScoreSize(Property p, HomeSearchPreferencesDto prefs)
    {
        var sqftRatio = prefs.MinSqft > 0 ? (double)p.LotSqft / prefs.MinSqft : 1.0;
        var bedroomBonus = (p.Bedrooms - prefs.MinBedrooms) * 10.0;
        return Clamp(Math.Min(sqftRatio * 50, 100) + bedroomBonus);
    }

    private static double ScoreCondition(Property p)
    {
        var avg = (p.RoofCondition + p.AcCondition + p.PlumbingCondition + p.ElectricalCondition) / 4.0;
        var reno = p.LastRenovation.HasValue ? Math.Max(0, 10 - (DateTime.UtcNow.Year - p.LastRenovation.Value)) * 2.0 : 0;
        return Clamp(avg + reno);
    }

    private static double ScoreCommute(int commuteMin, int maxCommute)
    {
        if (maxCommute <= 0) return 50;
        return Clamp((1.0 - (double)commuteMin / maxCommute) * 100);
    }

    private static double ScoreTaxUtilities(Property p)
    {
        var annual = (double)(p.PropertyTax + (p.Utilities * 12) + (p.HoaFee * 12));
        return Clamp(Math.Max(0, 100 - (annual / 300.0)));
    }

    private static double ScoreEnvironment(Property p)
    {
        return Clamp((100 - p.FloodRisk + (100 - p.NoiseLevel)) / 2.0);
    }

    private static double Clamp(double value) => Math.Clamp(value, 0, 100);
}