#include "FlightSystemWrapper.h"
#include "flight_system.h"
#include <vector>
#include <memory>

#define SHOULD_LOG true

#if SHOULD_LOG
template<typename T>
std::ostream& operator<<(std::ostream& os, std::vector<T> vec)
{
	os << "{";
	if (vec.size() != 0)
	{
		std::copy(vec.begin(), vec.end() - 1, std::ostream_iterator<T>(os, " "));
		os << vec.back();
	}
	os << "}";
	return os;
}
#endif 

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
bool InitializeFlights(void* flightSystem) {
	if (!flightSystem) {
		return false;
	}

	try {
		FlightSystem* system = static_cast<FlightSystem*>(flightSystem);

		return system->initialize(nullptr, 0);
	}
	catch (...) {
		return false;
	}
}

// Add new flights to the system
bool AddFlights(void* flightSystem, int* ids, int* positions, int* durations, int flightCount, int positionCount) {
	// Position works as follows: There are 2 ints for x and y. Then any amount of z's up to a negative number.
	// The negative numbers must be included in the positionCount-count.
	if (!flightSystem || !ids || !positions || !durations || flightCount <= 0 || positionCount <= 0) {
		return false;
	}

	try {
		FlightSystem* system = static_cast<FlightSystem*>(flightSystem);
		std::vector<Flight> flights(flightCount);

		int positionCounter = 0;

		// Convert flat float array into flights
		for (int i = 0; i < flightCount; i++) {
			flights[i].position.x = positions[positionCounter++];
			flights[i].position.y = positions[positionCounter++];

			std::vector<int> z;
			while (positionCounter < positionCount && positions[positionCounter] >= 0) {
				z.push_back(positions[positionCounter++]);
			}

			flights[i].position.z = z.data();
			flights[i].position.zLength = z.size();

			flights[i].flightDuration = durations[i];
			flights[i].id = ids[i];
#if SHOULD_LOG
			std::cout << "Adding Flight #" << i << " ID: " << flights[i].id << " Position: " << flights[i].position.x << ", " << flights[i].position.y << ", " << z << " Duration: " << flights[i].flightDuration << std::endl;
#endif
		}

		return system->addFlights(flights.data(), flightCount);
	}
	catch (...) {
		return false;
	}
}

// Remove flights by ids
bool RemoveFlights(void* flightSystem, int* ids, int count) {
	if (!flightSystem || !ids || count <= 0) {
		return false;
	}

	try {
		FlightSystem* system = static_cast<FlightSystem*>(flightSystem);
		return system->removeFlights(ids, count);
	}
	catch (...) {
		return false;
	}
}

// Update specific flights
bool UpdateFlights(void* flightSystem, int* ids, int* newPositions, int* newDurations, int updateCount, int positionCount) {
	if (!flightSystem || !ids || !newPositions || !newDurations || updateCount <= 0 || positionCount <= 0) {
		return false;
	}

	try {
		FlightSystem* system = static_cast<FlightSystem*>(flightSystem);
		std::vector<FlightPosition> positions(updateCount);
		std::vector<int> durations(updateCount);

		int positionCounter = 0;

		// Convert flat float array into Vec3 positions
		for (int i = 0; i < updateCount; i++) {
			positions[i].x = newPositions[positionCounter++];
			positions[i].y = newPositions[positionCounter++];

			std::vector<int> z;
			while (positionCounter < positionCount && newPositions[positionCounter] >= 0) {
				z.push_back(newPositions[positionCounter++]);
			}

			positions[i].z = z.data();
			positions[i].zLength = z.size();

			durations[i] = newDurations[i];
#if SHOULD_LOG
			std::cout << "Updating Flight #" << i << " ID: " << ids[i] << " Position: " << positions[i].x << ", " << positions[i].y << ", " << z << " Duration: " << durations[i] << std::endl;
#endif
		}

		return system->updateFlights(ids, positions.data(), durations.data(), updateCount);
	}
	catch (...) {
		return false;
	}
}

// Detect collisions with a bounding box
int* DetectCollisions(void* flightSystem, int* boxMin, int* boxMax) {
	if (!flightSystem || !boxMin || !boxMax) {
		return nullptr;
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

#if SHOULD_LOG
		std::cout << "Detecting collision with box: " << box.min.x << ", " << box.min.y << ", " << box.min.z << " - " << box.max.x << ", " << box.max.y << ", " << box.max.z << std::endl;
#endif

		return system->detectCollisions(box, true);
	}
	catch (...) {
		return nullptr;
	}
}

bool ReleaseCollisionResults(void* flightSystem, int* results)
{
	if (!results)
	{
		return false;
	}
	try {
		FlightSystem* system = static_cast<FlightSystem*>(flightSystem);
		return system->releaseCollisionResults(results);
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