#include "FlightSystemWrapper.h"
#include "flight_system.h"
#include <vector>
#include <memory>

// Create a new flight system
void* CreateFlightSystem() {
	try {
		return new FlightSystem();
	}
	catch (...) {
		return nullptr;
	}
}

// Destroy a flight system
void DestroyFlightSystem(void* flightSystem) {
	if (flightSystem) {
		FlightSystem* system = static_cast<FlightSystem*>(flightSystem);
		delete system;
	}
}

// Initialize flights in the system
bool InitializeFlights(void* flightSystem, float* positions, int count) {
	if (!flightSystem || !positions || count <= 0) {
		return false;
	}

	try {
		FlightSystem* system = static_cast<FlightSystem*>(flightSystem);
		std::vector<Flight> flights(count);

		// Convert flat float array into flights
		for (int i = 0; i < count; i++) {
			flights[i].position.x = positions[i * 3];
			flights[i].position.y = positions[i * 3 + 1];
			flights[i].position.z = positions[i * 3 + 2];
			flights[i].id = i;
		}

		return system->initialize(flights.data(), count);
	}
	catch (...) {
		return false;
	}
}

// Add new flights to the system
bool AddFlights(void* flightSystem, float* positions, int count) {
	if (!flightSystem || !positions || count <= 0) {
		return false;
	}

	try {
		FlightSystem* system = static_cast<FlightSystem*>(flightSystem);
		std::vector<Flight> flights(count);

		// Get the current flight count for ID assignment
		int startId = system->getFlightCount();

		// Convert flat float array into flights
		for (int i = 0; i < count; i++) {
			flights[i].position.x = positions[i * 3];
			flights[i].position.y = positions[i * 3 + 1];
			flights[i].position.z = positions[i * 3 + 2];
			flights[i].id = startId + i;
		}

		return system->addFlights(flights.data(), count);
	}
	catch (...) {
		return false;
	}
}

// Remove flights by indices
bool RemoveFlights(void* flightSystem, int* indices, int count) {
	if (!flightSystem || !indices || count <= 0) {
		return false;
	}

	try {
		FlightSystem* system = static_cast<FlightSystem*>(flightSystem);
		return system->removeFlights(indices, count);
	}
	catch (...) {
		return false;
	}
}

// Update specific flights
bool UpdateFlights(void* flightSystem, int* indices, int* newPositions, int* newDurations, int updateCount) {
	if (!flightSystem || !indices || !newPositions || !newDurations || updateCount <= 0) {
		return false;
	}

	try {
		FlightSystem* system = static_cast<FlightSystem*>(flightSystem);
		std::vector<Vec3> positions(updateCount);
		std::vector<int> durations(updateCount);

		// Convert flat float array into Vec3 positions
		for (int i = 0; i < updateCount; i++) {
			positions[i].x = newPositions[i * 3];
			positions[i].y = newPositions[i * 3 + 1];
			positions[i].z = newPositions[i * 3 + 2];
			durations[i] = newDurations[i];
		}

		return system->updateFlights(indices, positions.data(), durations.data(), updateCount);
	}
	catch (...) {
		return false;
	}
}

// Detect collisions with a bounding box
bool DetectCollisions(void* flightSystem, float* boxMin, float* boxMax, int* results) {
	if (!flightSystem || !boxMin || !boxMax || !results) {
		return false;
	}

	try {
		FlightSystem* system = static_cast<FlightSystem*>(flightSystem);

		// Create bounding box
		BoundingBox box;
		box.min.x = boxMin[0];
		box.min.y = boxMin[1];
		box.min.z = boxMin[2];
		box.max.x = boxMax[0];
		box.max.y = boxMax[1];
		box.max.z = boxMax[2];

		return system->detectCollisions(box, results);
	}
	catch (...) {
		return false;
	}
}

// Get the number of flights in the system
int GetFlightCount(void* flightSystem) {
	if (!flightSystem) {
		return 0;
	}

	try {
		FlightSystem* system = static_cast<FlightSystem*>(flightSystem);
		return system->getFlightCount();
	}
	catch (...) {
		return 0;
	}
}