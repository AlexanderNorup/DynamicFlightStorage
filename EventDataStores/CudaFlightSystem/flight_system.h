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

	// Remove flights by indices
	bool removeFlights(int* indices, int count);

	// Update specific flights
	bool updateFlights(int* ids, FlightPosition* newPositions, int* newDurations, int updateCount);

	// Detect collisions with a bounding box
	bool detectCollisions(const BoundingBox& box, bool autoSetRecalculating, int* collisionResults);

	// Get index for a flight ID (returns -1 if not found)
	int getIndexFromId(int flightId) const;

	// Get flight indicies by ID
	bool getIndicesFromIds(int* ids, int count, int* indices);

	// Free GPU resources
	void cleanup();

	// Get the number of flights
	int getFlightCount() const { return numFlights; }

private:
	Flight* d_flights;         // Device flights array
	int* d_indices;            // Device flight indices for sorting
	int* d_collisionResults;   // Device collision results
	int numFlights;            // Total flight count
	int allocatedFlights;      // Number of flights allocated in GPU memory
	bool initialized;          // Whether system is initialized
	int deviceId;              // CUDA device ID
	int longestFlightDuration; // Longest flight duration
	bool flightIdMapDirty;     // Whether the flight ID map is dirty
	std::unordered_map<int, int> flightIdToIndex; // Map of flight ID to index

	// Private method to sort flights by X coordinate
	void sortFlightsByX();
	void findLongestFlightDuration();

	// Update the ID to index mapping
	void updateIdToIndexMap();

	int* getMinMaxIndex(int min, int max);

	// Allocate or reallocate device memory
	bool allocateDeviceMemory(int requiredSize);
	thrust::device_vector<int> d_flightZData; // Device flight Z data

	void copyZDataToDeviceManaged(Flight* hostFlights, int count);

	void debug();
};

#endif // FLIGHT_SYSTEM_H