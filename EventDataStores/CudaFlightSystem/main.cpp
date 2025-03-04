#include <iostream>
#include <vector>
#include <random>
#include <chrono>
#include <algorithm>
#include <iomanip>
#include <string>
#include "flight.h"
#include "flight_system.h"

// Utility function to generate random point flights
void generateRandomParticles(std::vector<Flight>& flights, int count, float range) {
	std::random_device rd;
	std::mt19937 gen(rd());
	std::uniform_real_distribution<float> posDist(-range, range);

	flights.resize(count);

	for (int i = 0; i < count; i++) {
		flights[i].position.x = posDist(gen);
		flights[i].position.y = posDist(gen);
		flights[i].position.z = posDist(gen);
		flights[i].id = i;
	}
}

// This is just a test function to demonstrate the usage of the FlightSystem class
int main(int argc, char* argv[]) {
	std::cout << "CUDA Sort and Sweep Box Collision Detection" << std::endl;
	std::cout << "==============================================" << std::endl;

	// Parse command line arguments or use default values
	int numFlights = 10000000;
	if (argc > 1) {
		numFlights = std::atoi(argv[1]);
	}

	// Create a bounding box
	BoundingBox box;
	box.min = { -10.0f, -10.0f, -10.0f };
	box.max = { 10.0f, 10.0f, 10.0f };

	std::cout << "Bounding Box: ["
		<< box.min.x << ", " << box.min.y << ", " << box.min.z << "] to ["
		<< box.max.x << ", " << box.max.y << ", " << box.max.z << "]" << std::endl;

	// Generate random flights
	std::vector<Flight> flights;
	std::cout << "Generating " << numFlights << " random point flights..." << std::endl;
	generateRandomParticles(flights, numFlights, 20.0f);

	// Create and initialize particle system
	FlightSystem flightSystem;
	std::cout << "Initializing particle system in GPU memory..." << std::endl;
	if (!flightSystem.initialize(flights.data(), numFlights)) {
		std::cerr << "Failed to initialize particle system" << std::endl;
		return 1;
	}

	// Allocate result arrays
	std::vector<int> gpuResults(numFlights, 0);

	// Run initial collision detection
	std::cout << "Running initial collision detection..." << std::endl;
	auto startGpu = std::chrono::high_resolution_clock::now();

	bool success = flightSystem.detectCollisions(box, gpuResults.data());
	if (!success) {
		std::cerr << "Collision detection failed" << std::endl;
		return 1;
	}

	auto endGpu = std::chrono::high_resolution_clock::now();
	std::chrono::duration<double, std::milli> gpuTime = endGpu - startGpu;

	// Count collisions from GPU results
	int collisionCount = std::count(gpuResults.begin(), gpuResults.end(), 1);

	// Output initial results
	std::cout << "Detected " << collisionCount << " points inside the bounding box out of "
		<< numFlights << " flights." << std::endl;
	std::cout << "GPU execution time: " << std::fixed << std::setprecision(2)
		<< gpuTime.count() << " ms" << std::endl;

	// Wait for user input before closing the console window
	std::cout << "\nPress Enter to continue..." << std::endl;
	std::cin.get();

	// Demonstrate updating specific flights
	int numFlightsToUpdate = std::min(1000, numFlights);
	std::vector<int> updateIndices(numFlightsToUpdate);
	std::vector<Vec3> newPositions(numFlightsToUpdate);

	// Select random flights to update
	std::random_device rd;
	std::mt19937 gen(rd());
	std::uniform_int_distribution<> idxDist(0, numFlights - 1);
	std::uniform_real_distribution<float> posDist(-15.0f, 15.0f);

	std::cout << "Updating positions of " << numFlightsToUpdate << " random flights..." << std::endl;

	for (int i = 0; i < numFlightsToUpdate; i++) {
		updateIndices[i] = idxDist(gen);
		newPositions[i].x = posDist(gen);
		newPositions[i].y = posDist(gen);
		newPositions[i].z = posDist(gen);
	}

	// Update flights in GPU memory
	auto startUpdate = std::chrono::high_resolution_clock::now();
	success = flightSystem.updateFlights(updateIndices.data(), newPositions.data(), numFlightsToUpdate);
	auto endUpdate = std::chrono::high_resolution_clock::now();

	if (!success) {
		std::cerr << "Failed to update flights" << std::endl;
		return 1;
	}

	std::chrono::duration<double, std::milli> updateTime = endUpdate - startUpdate;
	std::cout << "Flight update time: " << std::fixed << std::setprecision(2)
		<< updateTime.count() << " ms" << std::endl;

	// Re-run collision detection after update
	std::cout << "Re-running collision detection after update..." << std::endl;
	auto startRedetect = std::chrono::high_resolution_clock::now();

	success = flightSystem.detectCollisions(box, gpuResults.data());

	auto endRedetect = std::chrono::high_resolution_clock::now();
	std::chrono::duration<double, std::milli> redetectTime = endRedetect - startRedetect;

	if (!success) {
		std::cerr << "Collision re-detection failed" << std::endl;
		return 1;
	}

	// Count collisions after update
	int newCollisionCount = std::count(gpuResults.begin(), gpuResults.end(), 1);

	// Output updated results
	std::cout << "After update: Detected " << newCollisionCount << " points inside the bounding box." << std::endl;
	std::cout << "Collision re-detection time: " << std::fixed << std::setprecision(2)
		<< redetectTime.count() << " ms" << std::endl;

	// Test adding a single flight
	std::cout << "\nAdding just a single flight" << std::endl;
	auto startAddFlight = std::chrono::high_resolution_clock::now();

	Flight newFlight;
	newFlight.id = 1337;
	newFlight.position = { 0.0f, 0.0f, 0.0f };

	success = flightSystem.addFlights(&newFlight, 1);

	auto endAddFlight = std::chrono::high_resolution_clock::now();
	std::chrono::duration<double, std::milli> addFlightTime = endAddFlight - startAddFlight;

	if (!success) {
		std::cerr << "Adding flight failed" << std::endl;
		return 1;
	}

	// Resize to fit the new flight
	gpuResults.resize(flightSystem.getFlightCount());

	// Re-run collision detection after adding a flight. Should be one higher than before
	success = flightSystem.detectCollisions(box, gpuResults.data());

	if (!success) {
		std::cerr << "Collision re-detection (after add) failed" << std::endl;
		return 1;
	}

	int newCollisionCountAfterAdd = std::count(gpuResults.begin(), gpuResults.end(), 1);

	std::cout << "After adding one flight: Detected " << newCollisionCountAfterAdd << " points inside the bounding box." << std::endl;
	if (newCollisionCountAfterAdd == newCollisionCount + 1) {
		std::cout << "Adding a single flight worked as expected." << std::endl;
	}
	else
	{
		std::cerr << "Adding a single flight did not work as expected." << std::endl;
	}
	std::cout << "Adding a flight took: " << std::fixed << std::setprecision(2)
		<< addFlightTime.count() << " ms" << std::endl;

	// Test removing flights

	// Select random flights to remove
	numFlights = flightSystem.getFlightCount();
	int numFlightsToRemove = std::min(1000, numFlights);
	std::vector<int> removeIndices(numFlightsToRemove);

	for (int i = 0; i < numFlightsToRemove; i++) {
		removeIndices[i] = idxDist(gen);
	}

	std::cout << "\nRemoving " << numFlightsToRemove << " random flights..." << std::endl;

	// Remove flights in GPU memory
	auto startRemove = std::chrono::high_resolution_clock::now();
	success = flightSystem.removeFlights(removeIndices.data(), numFlightsToRemove);
	auto endRemove = std::chrono::high_resolution_clock::now();
	std::chrono::duration<double, std::milli> removeTime = endRemove - startRemove;

	if (!success) {
		std::cerr << "Removing flights failed" << std::endl;
		return 1;
	}

	int countAfterRemoval = flightSystem.getFlightCount();
	gpuResults.resize(countAfterRemoval);

	// Re-run collision detection after removing some flights.
	success = flightSystem.detectCollisions(box, gpuResults.data());

	if (!success) {
		std::cerr << "Collision re-detection (after remvoe) failed" << std::endl;
		return 1;
	}

	int newCollisionCountAfterRemove = std::count(gpuResults.begin(), gpuResults.end(), 1);

	std::cout << "Stored Flights before removal: " << numFlights << " and after removal: " << countAfterRemoval << std::endl;
	std::cout << "After removing " << numFlightsToRemove << " flights: Detected " << newCollisionCountAfterRemove << " points inside the bounding box." << std::endl;
	if (newCollisionCountAfterRemove < newCollisionCountAfterAdd) {
		std::cout << "Removing flights also removed some collisions as expected." << std::endl;
	}
	else
	{
		std::cerr << "Removing flights did not change the collision count." << std::endl;
	}
	std::cout << "Removing flights took: " << std::fixed << std::setprecision(2)
		<< removeTime.count() << " ms" << std::endl;

	// Clean up GPU resources
	flightSystem.cleanup();

	// Wait for user input before closing the console window
	std::cout << "\nPress Enter to exit..." << std::endl;
	std::cin.get();

	return 0;
}