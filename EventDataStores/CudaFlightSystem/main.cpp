#include <iostream>
#include <vector>
#include <random>
#include <chrono>
#include <algorithm>
#include <iomanip>
#include <string>
#include "console_colors.h"
#include "flight.h"
#include "flight_system.h"
#include "collisionSystemTest.h"

// Utility function to generate random point flights
void generateRandomFlights(std::vector<Flight>& flights, int count, int positionRange, int durationRange) {
	std::random_device rd;
	std::mt19937 gen(rd());
	std::uniform_int_distribution<int> posDist(-positionRange, positionRange);
	durationRange = std::max(50, durationRange);
	std::uniform_int_distribution<int> durDist(durationRange - 50, durationRange);

	flights.resize(count);

	for (int i = 0; i < count; i++) {
		flights[i].position.x = posDist(gen);
		flights[i].position.y = posDist(gen);
		flights[i].position.z = posDist(gen);
		flights[i].flightDuration = durDist(gen);
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
	box.min = { -10, -10, -10 };
	box.max = { 10, 10, 10 };

	std::cout << "Bounding Box: ["
		<< box.min.x << ", " << box.min.y << ", " << box.min.z << "] to ["
		<< box.max.x << ", " << box.max.y << ", " << box.max.z << "]" << std::endl;

	// Generate random flights
	std::vector<Flight> flights;
	std::cout << "Generating " << numFlights << " random point flights..." << std::endl;
	generateRandomFlights(flights, numFlights, 20.0f, 100);

	// Create and initialize flight system
	FlightSystem flightSystem;
	std::cout << "Initializing flight system in GPU memory..." << std::endl;
	if (!flightSystem.initialize(flights.data(), numFlights)) {
		std::cerr << COLOR_RED << "Failed to initialize flight system" << COLOR_RESET << std::endl;
		return 1;
	}

	if (flightSystem.getFlightCount() <= 0) {
		std::cerr << COLOR_RED << "Flight system initialized with 0 flights" << COLOR_RESET << std::endl;
		return 1;
	}
	else {
		std::cout << COLOR_GREEN << "Flight system initialized with " << flightSystem.getFlightCount() << " flights." << COLOR_RESET << std::endl;
	}

	if (flightSystem.getFlightCount() != numFlights) {
		std::cerr << COLOR_RED << "Flight count mismatch after initialization" << COLOR_RESET << std::endl;
		return 1;
	}

	// Allocate result arrays
	std::vector<int> gpuResults(numFlights, 0);

	// Run initial collision detection
	std::cout << "Running initial collision detection..." << std::endl;
	auto startGpu = std::chrono::high_resolution_clock::now();

	bool success = flightSystem.detectCollisions(box, gpuResults.data());
	if (!success) {
		std::cerr << COLOR_RED << "Collision detection failed" << COLOR_RESET << std::endl;
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

	if (collisionCount <= 0) {
		std::cerr << COLOR_RED << "No collisions detected in the initial run." << COLOR_RESET << std::endl;
	}

	// Wait for user input before closing the console window
	std::cout << "\nPausing so you can examine GPU memory\nPress Enter to continue..." << std::endl;
	std::cin.get();

	// Demonstrate updating specific flights
	int numFlightsToUpdate = std::min(1000, numFlights);
	std::vector<int> updateIndices(numFlightsToUpdate);
	std::vector<Vec3> newPositions(numFlightsToUpdate);
	std::vector<int> newDurations(numFlightsToUpdate);

	// Select random flights to update
	std::random_device rd;
	std::mt19937 gen(rd());
	std::uniform_int_distribution<> idxDist(0, numFlights - 1);
	std::uniform_int_distribution<> posDist(-15, 15);
	std::uniform_int_distribution<> posDuration(60, 100);

	std::cout << "Updating positions of " << numFlightsToUpdate << " random flights..." << std::endl;

	for (int i = 0; i < numFlightsToUpdate; i++) {
		updateIndices[i] = idxDist(gen);
		newPositions[i].x = posDist(gen);
		newPositions[i].y = posDist(gen);
		newPositions[i].z = posDist(gen);
		newDurations[i] = posDuration(gen);
	}

	// Update flights in GPU memory
	auto startUpdate = std::chrono::high_resolution_clock::now();
	success = flightSystem.updateFlights(updateIndices.data(), newPositions.data(), newDurations.data(), numFlightsToUpdate);
	auto endUpdate = std::chrono::high_resolution_clock::now();

	if (!success) {
		std::cerr << COLOR_RED << "Failed to update flights" << COLOR_RESET << std::endl;
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
		std::cerr << COLOR_RED << "Collision re-detection failed" << COLOR_RESET << std::endl;
		return 1;
	}

	// Count collisions after update
	int newCollisionCount = std::count(gpuResults.begin(), gpuResults.end(), 1);

	// Output updated results
	std::cout << "After update: Detected " << newCollisionCount << " points inside the bounding box." << std::endl;
	std::cout << "Collision re-detection time: " << std::fixed << std::setprecision(2)
		<< redetectTime.count() << " ms" << std::endl;

	if (newCollisionCount != collisionCount) {
		std::cout << COLOR_GREEN << "Updating flight positions changed the collision count as expected." << COLOR_RESET << std::endl;
	}
	else
	{
		std::cerr << COLOR_RED << "Updating flight positions did not change collision count." << COLOR_RESET << std::endl;
	}

	// Test adding a single flight
	std::cout << "\nAdding just a single flight" << std::endl;
	auto startAddFlight = std::chrono::high_resolution_clock::now();

	Flight newFlight;
	newFlight.id = 1337;
	newFlight.flightDuration = 420;
	newFlight.position = { 0, 0, 0 };

	success = flightSystem.addFlights(&newFlight, 1);

	auto endAddFlight = std::chrono::high_resolution_clock::now();
	std::chrono::duration<double, std::milli> addFlightTime = endAddFlight - startAddFlight;

	if (!success) {
		std::cerr << COLOR_RED << "Adding flight failed" << COLOR_RESET << std::endl;
		return 1;
	}

	// Resize to fit the new flight
	gpuResults.resize(flightSystem.getFlightCount());

	// Re-run collision detection after adding a flight. Should be one higher than before
	success = flightSystem.detectCollisions(box, gpuResults.data());

	if (!success) {
		std::cerr << COLOR_RED << "Collision re-detection (after add) failed" << COLOR_RESET << std::endl;
		return 1;
	}

	int newCollisionCountAfterAdd = std::count(gpuResults.begin(), gpuResults.end(), 1);

	std::cout << "After adding one flight: Detected " << newCollisionCountAfterAdd << " points inside the bounding box." << std::endl;
	if (newCollisionCountAfterAdd == newCollisionCount + 1) {
		std::cout << COLOR_GREEN << "Adding a single flight worked as expected." << COLOR_RESET << std::endl;
	}
	else
	{
		std::cerr << COLOR_RED << "Adding a single flight did not work as expected." << COLOR_RESET << std::endl;
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
		std::cerr << COLOR_RED << "Removing flights failed" << COLOR_RESET << std::endl;
		return 1;
	}

	int countAfterRemoval = flightSystem.getFlightCount();
	gpuResults.resize(countAfterRemoval);

	// Re-run collision detection after removing some flights.
	success = flightSystem.detectCollisions(box, gpuResults.data());

	if (!success) {
		std::cerr << COLOR_RED << "Collision re-detection (after remvoe) failed" << COLOR_RESET << std::endl;
		return 1;
	}

	int newCollisionCountAfterRemove = std::count(gpuResults.begin(), gpuResults.end(), 1);

	std::cout << "Stored Flights before removal: " << numFlights << " and after removal: " << countAfterRemoval << std::endl;
	if (numFlights - numFlightsToRemove != countAfterRemoval) {
		std::cerr << COLOR_RED << "Flight count mismatch after removal (diff=" << (numFlights - countAfterRemoval) << ")" << COLOR_RESET << std::endl;
	}
	else {
		std::cout << COLOR_GREEN << "Flight count matches expected value after removal" << COLOR_RESET << std::endl;
	}

	std::cout << "After removing " << numFlightsToRemove << " flights: Detected " << newCollisionCountAfterRemove << " points inside the bounding box." << std::endl;
	if (newCollisionCountAfterRemove < newCollisionCountAfterAdd) {
		std::cout << COLOR_GREEN << "Removing flights also removed some collisions as expected." << COLOR_RESET << std::endl;
	}
	else
	{
		std::cerr << COLOR_RED << "Removing flights did not change the collision count." << COLOR_RESET << std::endl;
	}
	std::cout << "Removing flights took: " << std::fixed << std::setprecision(2)
		<< removeTime.count() << " ms" << std::endl;

	// Clean up GPU resources
	flightSystem.cleanup();

	// Test creating an empty flightsystem
	std::cout << "\nCreating an empty flight system..." << std::endl;
	FlightSystem emptyFlightSystem;
	emptyFlightSystem.initialize(nullptr, 0);

	if (emptyFlightSystem.getFlightCount() != 0) {
		std::cerr << COLOR_RED << "Empty system is not empty" << COLOR_RESET << std::endl;
	}
	else {
		std::cout << COLOR_GREEN << "Empty system is initialized and contains " << emptyFlightSystem.getFlightCount() << " flights." << COLOR_RESET << std::endl;
	}

	// Test adding flights to an empty flight system
	int flightsToAdd = 1000;
	std::vector<Flight> newFlights;
	std::cout << "Generating " << flightsToAdd << " random point flights..." << std::endl;
	generateRandomFlights(newFlights, flightsToAdd, 20.0f, 100);
	success = emptyFlightSystem.addFlights(newFlights.data(), flightsToAdd);
	if (!success) {
		std::cerr << COLOR_RED << "Adding flights to empty system failed" << COLOR_RESET << std::endl;
		return 1;
	}

	std::vector<int> collisionResults(flightsToAdd, 0);
	success = emptyFlightSystem.detectCollisions(box, collisionResults.data());
	if (!success) {
		std::cerr << COLOR_RED << "Collision detection in (no longer) empty system failed" << COLOR_RESET << std::endl;
		return 1;
	}

	int emptyCollisionCount = std::count(collisionResults.begin(), collisionResults.end(), 1);

	std::cout << "Empty system now has " << emptyFlightSystem.getFlightCount() << " flights." << std::endl;
	std::cout << "Detected " << emptyCollisionCount << " points inside the bounding box out of "
		<< flightsToAdd << " flights in the (no longer) empty system." << std::endl;

	if (emptyFlightSystem.getFlightCount() <= 0 || emptyCollisionCount <= 0) {
		std::cout << COLOR_RED << "Adding flights to empty system did not work as expected." << COLOR_RESET << std::endl;
	}
	else
	{
		std::cout << COLOR_GREEN << "Adding flights to empty system worked as expected." << COLOR_RESET << std::endl;
	}

	emptyFlightSystem.cleanup();

	// Do collision tests
	testCollisionSystem();

	// Wait for user input before closing the console window
	std::cout << "\nPress Enter to exit..." << std::endl;
	std::cin.get();

	return 0;
}

