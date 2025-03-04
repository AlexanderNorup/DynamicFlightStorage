#pragma once

#ifdef FLIGHTSYSTEMWRAPPER_EXPORTS
#define FLIGHT_API __declspec(dllexport)
#else
#define FLIGHT_API __declspec(dllimport)
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
		float* positions, int count);

	// Remove flights by indices
	FLIGHT_API bool RemoveFlights(void* flightSystem,
		int* indices, int count);

	// Update flight positions
	FLIGHT_API bool UpdateFlights(void* flightSystem,
		int* indices, int* newPositions, int* newDurations,
		int updateCount);

	// Detect collisions with a bounding box
	FLIGHT_API bool DetectCollisions(void* flightSystem,
		float* boxMin, float* boxMax,
		int* results);

	// Get the number of flights
	FLIGHT_API int GetFlightCount(void* flightSystem);
}