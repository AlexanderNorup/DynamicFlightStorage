#ifndef FLIGHT_SYSTEM_H
#define FLIGHT_SYSTEM_H

#include "flight.h"
#include <vector>
#include <unordered_map>
#include <thrust/device_vector.h>

// Class to manage a persistent flight system in GPU memory
class FlightSystem {
public:
	FlightSystem();
	~FlightSystem();

	// Initialize the flight system with host flights
	bool initialize(Flight* hostFlights, int count);

	// Add new flights to the system
	bool addFlights(Flight* newFlights, int count);

	// Remove flights by ids
	bool removeFlights(int* ids, int count);

	// Update specific flights
	bool updateFlights(int* ids, FlightPosition* newPositions, int* newDurations, int updateCount);

	// Detect collisions with a bounding box. Will automatically call sortFlightByX if needed
	int* detectCollisions(const BoundingBox& box, bool autoSetRecalculating);

	// Release collision results
	bool releaseCollisionResults(int* results);

	// Get index for a flight ID (returns -1 if not found)
	int getIndexFromId(int flightId) const;

	// Get flight indicies by ID
	bool getIndicesFromIds(int* ids, int count, int* indices);

	// Sort flights by X. Needs to be done before collision detection
	void sortFlightsByX();

	// Free GPU resources
	void cleanup();

	// Get the number of flights
	int getFlightCount() const { return numFlights; }

	int* ZeroCollisionResults; // Static Zero collision results 

private:
	Flight* d_flights;         // Device flights array
	int* d_indices;            // Device flight indices for sorting
	int* d_collisionResults;   // Device collision results
	int* d_collisionFlag;      // Device collision flag
	int numFlights;            // Total flight count
	int allocatedFlights;      // Number of flights allocated in GPU memory
	bool initialized;          // Whether system is initialized
	int deviceId;              // CUDA device ID
	int longestFlightDuration; // Longest flight duration
	bool flightIdMapDirty;     // Whether the flight ID map is dirty
	bool indicesDirty;         // Whether the indices are dirty
	std::unordered_map<int, int> flightIdToIndex; // Map of flight ID to index

	void findLongestFlightDuration();

	// Update the ID to index mapping
	void updateIdToIndexMap();

	int* minMaxResult; // MinMax result array
	void calculateMinMaxIndex(int min, int max);

	// Allocate or reallocate device memory
	bool allocateDeviceMemory(int requiredSize);
	thrust::device_vector<Airport> d_flightAirportData; // Device flight Airport data

	void copyZDataToDeviceManaged(Flight* hostFlights, int count);

	void debug();
};

#endif // FLIGHT_SYSTEM_H