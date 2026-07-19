#include "cat_risk_kernel.h"

#include <cmath>

namespace
{
	constexpr double kEarthRadiusKm = 6371.0;
	constexpr double kKmPerLatDegree = 111.32;
	constexpr double kDegToRad = 3.14159265358979323846 / 180.0;

	double haversine_km(double lat1, double lon1, double lat2, double lon2)
	{
		const double dLat = (lat2 - lat1) * kDegToRad;
		const double dLon = (lon2 - lon1) * kDegToRad;
		const double sinLat = std::sin(dLat * 0.5);
		const double sinLon = std::sin(dLon * 0.5);
		const double a = sinLat * sinLat +
			std::cos(lat1 * kDegToRad) * std::cos(lat2 * kDegToRad) * sinLon * sinLon;
		return 2.0 * kEarthRadiusKm * std::asin(std::sqrt(a < 1.0 ? a : 1.0));
	}

	// Conservative bounding-box reject: returns true when the pair is provably farther
	// apart than radiusKm, so the caller can skip the trigonometry entirely. Never
	// rejects a pair that is actually within the radius.
	bool outside_bounding_box(double lat1, double lon1, double lat2, double lon2, double radiusKm)
	{
		const double dLat = std::fabs(lat2 - lat1);
		if (dLat > radiusKm / kKmPerLatDegree)
			return true;

		// Longitude degrees shrink with latitude. Use the higher-magnitude latitude so the
		// permitted delta is the largest (and therefore safest) of the pair.
		const double maxAbsLat = std::fabs(lat1) > std::fabs(lat2) ? std::fabs(lat1) : std::fabs(lat2);
		const double cosLat = std::cos(maxAbsLat * kDegToRad);
		if (cosLat < 1e-6)
			return false;

		const double dLon = std::fabs(lon2 - lon1);
		return dLon > radiusKm / (kKmPerLatDegree * cosLat);
	}

	// Gross loss for one location under one event, after deductible and limit.
	// Mirrored line-for-line by CatRiskService.GrossLoss on the managed path.
	double gross_loss(const CatLocationNative& location, const CatEventNative& event, double alpha, double distanceKm)
	{
		if (event.radiusKm <= 0.0 || distanceKm >= event.radiusKm)
			return 0.0;

		const double decay = 1.0 - (distanceKm / event.radiusKm);
		const double siteIntensity = event.intensity * decay * location.siteHazard;
		if (siteIntensity <= 0.0)
			return 0.0;

		const double mdr = 1.0 - std::exp(-alpha * siteIntensity);
		const double groundUp = location.insuredValue * mdr;
		const double retained = location.insuredValue * location.deductibleRate;
		const double cap = location.insuredValue * location.limitRate;

		double net = groundUp - retained;
		if (net <= 0.0)
			return 0.0;
		return net > cap ? cap : net;
	}
}

extern "C" CAT_API int Cat_ComputeRingAccumulation(
	const CatLocationNative* locations,
	int locationCount,
	double radiusKm,
	double* outRingTiv,
	int* outNeighborCount,
	int outputLength)
{
	try
	{
		if (!locations || !outRingTiv || !outNeighborCount)
			return -1;
		if (locationCount <= 0 || outputLength < locationCount)
			return -1;
		if (!(radiusKm > 0.0))
			return -1;

		for (int i = 0; i < locationCount; ++i)
		{
			double ringTiv = 0.0;
			int neighbors = 0;
			const double lat = locations[i].latitude;
			const double lon = locations[i].longitude;

			for (int j = 0; j < locationCount; ++j)
			{
				const double otherLat = locations[j].latitude;
				const double otherLon = locations[j].longitude;

				if (i != j && outside_bounding_box(lat, lon, otherLat, otherLon, radiusKm))
					continue;

				if (i == j || haversine_km(lat, lon, otherLat, otherLon) <= radiusKm)
				{
					ringTiv += locations[j].insuredValue;
					++neighbors;
				}
			}

			outRingTiv[i] = ringTiv;
			outNeighborCount[i] = neighbors;
		}

		return 0;
	}
	catch (...)
	{
		return -99;
	}
}

extern "C" CAT_API int Cat_SimulateEventLosses(
	const CatLocationNative* locations,
	int locationCount,
	const CatEventNative* events,
	int eventCount,
	double vulnerabilityAlpha,
	double* outEventLosses,
	int* outAffectedCounts,
	int outputLength)
{
	try
	{
		if (!locations || !events || !outEventLosses || !outAffectedCounts)
			return -1;
		if (locationCount <= 0 || eventCount <= 0 || outputLength < eventCount)
			return -1;
		if (!(vulnerabilityAlpha > 0.0))
			return -1;

		for (int e = 0; e < eventCount; ++e)
		{
			const CatEventNative& event = events[e];
			double total = 0.0;
			int affected = 0;

			for (int l = 0; l < locationCount; ++l)
			{
				const CatLocationNative& location = locations[l];

				// Cheap reject before any trigonometry — most locations lie outside
				// most event footprints on a spatially clustered book.
				if (outside_bounding_box(event.latitude, event.longitude,
					location.latitude, location.longitude, event.radiusKm))
					continue;

				const double distanceKm = haversine_km(event.latitude, event.longitude,
					location.latitude, location.longitude);

				const double loss = gross_loss(location, event, vulnerabilityAlpha, distanceKm);
				if (loss > 0.0)
				{
					total += loss;
					++affected;
				}
			}

			outEventLosses[e] = total;
			outAffectedCounts[e] = affected;
		}

		return 0;
	}
	catch (...)
	{
		return -99;
	}
}
