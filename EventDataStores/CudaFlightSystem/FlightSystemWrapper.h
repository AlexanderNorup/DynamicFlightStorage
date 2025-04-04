#pragma once

#if defined(_WIN32)
#ifdef FLIGHTSYSTEMWRAPPER_EXPORTS
#define FLIGHT_API __declspec(dllexport)
#else
#define FLIGHT_API __declspec(dllimport)
#endif
#else
#define FLIGHT_API
#endif

// C-compatible flight system interface
extern "C" {
	// Create and initialize a flight system
	FLIGHT_API void* CreateFlightSystem();

	// Destroy a flight system
	FLIGHT_API void DestroyFlightSystem(void* flightSystem);

	// Initialize flights
	FLIGHT_API bool InitializeFlights(void* flightSystem);

	// Add new flights to the system
	FLIGHT_API bool AddFlights(void* flightSystem,
		int* ids, int* positions, int* durations, int flightCount, int positionCount);

	// Remove flights by ids
	FLIGHT_API bool RemoveFlights(void* flightSystem,
		int* ids, int count);

	// Update flight positions
	FLIGHT_API bool UpdateFlights(void* flightSystem,
		int* ids, int* newPositions, int* newDurations,
		int updateCount, int positionCount);

	// Detect collisions with a bounding box. Remember to release results with ReleaseCollisionResults
	FLIGHT_API int* DetectCollisions(void* flightSystem,
		int* boxMin, int* boxMax);

	// Release collision results
	FLIGHT_API bool ReleaseCollisionResults(void* flightSystem,
		int* results);

	// Get the number of flights
	FLIGHT_API int GetFlightCount(void* flightSystem);
}