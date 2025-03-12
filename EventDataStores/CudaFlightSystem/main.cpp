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
	std::uniform_int_distribution<int> durDist(durationRange - durationRange / 2, durationRange);

	flights.resize(count);

	for (int i = 0; i < count; i++) {
		flights[i].position.x = posDist(gen);
		flights[i].position.y = posDist(gen);
		flights[i].position.z = new int[2] { posDist(gen), posDist(gen) };
		flights[i].position.zLength = 2;
		flights[i].flightDuration = durDist(gen);
		flights[i].id = i;
	}
}

void freeFlights(std::vector<Flight>& flights) {
	for (int i = 0; i < flights.size(); i++) {
		delete[] flights[i].position.z;
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
	generateRandomFlights(flights, numFlights, 100.0f, 50);

	// Create and initialize flight system
	FlightSystem flightSystem;
	std::cout << "Initializing flight system in GPU memory..." << std::endl;
	if (!flightSystem.initialize(flights.data(), numFlights)) {
		std::cerr << COLOR_RED << "Failed to initialize flight system" << COLOR_RESET << std::endl;
		return 1;
	}
	freeFlights(flights);

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

	// Run initial collision detection
	std::cout << "Running initial collision detection..." << std::endl;
	auto startGpu = std::chrono::high_resolution_clock::now();

	int* gpuResults = flightSystem.detectCollisions(box, false);

	auto endGpu = std::chrono::high_resolution_clock::now();
	std::chrono::duration<double, std::milli> gpuTime = endGpu - startGpu;

	// Count collisions from GPU results
	int collisionCount = gpuResults[0];;

	// Verify the collisionCount is accurate
	bool countInvalid = false;
	for (int i = 0; i < collisionCount; i++) {
		if (gpuResults[i + 1] == INT_MIN) {
			std::cerr << COLOR_RED << "Invalid flight id detected in collision results. INT_MIN found within the list already at index " << i + 1 << COLOR_RESET << std::endl;
			countInvalid = true;
			break;
		}
	}

	if (!countInvalid)
	{
		std::cout << COLOR_GREEN << "Collision count seems right when compared to the data in the returned pointer." << COLOR_RESET << std::endl;
	}

	flightSystem.releaseCollisionResults(gpuResults);

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
	std::vector<int> updateIds(numFlightsToUpdate);
	std::vector<FlightPosition> newPositions(numFlightsToUpdate);
	std::vector<int> newDurations(numFlightsToUpdate);

	// Select random flights to update
	std::random_device rd;
	std::mt19937 gen(rd());
	std::uniform_int_distribution<> idxDist(0, numFlights - 1);
	std::uniform_int_distribution<> posDist(-15, 15);
	std::uniform_int_distribution<> posDuration(60, 100);

	std::cout << "Updating positions of " << numFlightsToUpdate << " random flights..." << std::endl;

	for (int i = 0; i < numFlightsToUpdate; i++) {
		updateIds[i] = idxDist(gen);
		newPositions[i].x = posDist(gen);
		newPositions[i].y = posDist(gen);
		newPositions[i].z = new int[2] { posDist(gen), posDist(gen) };
		newPositions[i].zLength = 2;
		newDurations[i] = posDuration(gen);
	}

	// Update flights in GPU memory
	auto startUpdate = std::chrono::high_resolution_clock::now();
	bool success = flightSystem.updateFlights(updateIds.data(), newPositions.data(), newDurations.data(), numFlightsToUpdate);
	auto endUpdate = std::chrono::high_resolution_clock::now();

	if (!success) {
		std::cerr << COLOR_RED << "Failed to update flights" << COLOR_RESET << std::endl;
		return 1;
	}

	for (int i = 0; i < numFlightsToUpdate; i++) {
		delete[] newPositions[i].z; // Clear memory
	}

	std::chrono::duration<double, std::milli> updateTime = endUpdate - startUpdate;
	std::cout << "Flight update time: " << std::fixed << std::setprecision(2)
		<< updateTime.count() << " ms" << std::endl;

	// Re-run collision detection after update
	std::cout << "Re-running collision detection after update..." << std::endl;
	auto startRedetect = std::chrono::high_resolution_clock::now();

	gpuResults = flightSystem.detectCollisions(box, false);

	auto endRedetect = std::chrono::high_resolution_clock::now();
	std::chrono::duration<double, std::milli> redetectTime = endRedetect - startRedetect;

	// Count collisions after update
	int newCollisionCount = gpuResults[0];
	flightSystem.releaseCollisionResults(gpuResults);

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

	Flight newFlight;
	newFlight.isRecalculating = false;
	newFlight.id = -1337;
	newFlight.flightDuration = 420;
	newFlight.position = { 0, 0, new int[2] {0, 0}, 2 };

	auto startAddFlight = std::chrono::high_resolution_clock::now();

	success = flightSystem.addFlights(&newFlight, 1);

	auto endAddFlight = std::chrono::high_resolution_clock::now();
	std::chrono::duration<double, std::milli> addFlightTime = endAddFlight - startAddFlight;

	delete[] newFlight.position.z;

	if (!success) {
		std::cerr << COLOR_RED << "Adding flight failed" << COLOR_RESET << std::endl;
		return 1;
	}

	if (flightSystem.getFlightCount() == numFlights + 1) {
		std::cout << COLOR_GREEN << "Adding a flight increased flight-count by 1 as expected. Count before: " << numFlights << " count now: " << flightSystem.getFlightCount() << COLOR_RESET << std::endl;
	}
	else {
		std::cerr << COLOR_RED << "Adding a flight did not increase flight-count by 1. Count before: " << numFlights << " count now: " << flightSystem.getFlightCount() << COLOR_RESET << std::endl;
	}

	// Re-run collision detection after adding a flight. Should be one higher than before
	gpuResults = flightSystem.detectCollisions(box, false);

	int newCollisionCountAfterAdd = gpuResults[0];
	flightSystem.releaseCollisionResults(gpuResults);

	std::cout << "After adding one flight: Detected " << newCollisionCountAfterAdd << " points inside the bounding box." << std::endl;
	if (newCollisionCountAfterAdd == newCollisionCount + 1) {
		std::cout << COLOR_GREEN << "Adding a single flight in the center added a new collision as expected." << COLOR_RESET << std::endl;
	}
	else
	{
		std::cerr << COLOR_RED << "Adding a single flight did not add a new collision." << COLOR_RESET << std::endl;
	}
	std::cout << "Adding a flight took: " << std::fixed << std::setprecision(2)
		<< addFlightTime.count() << " ms" << std::endl;


	std::cout << "\nTest finding index by id" << std::endl;
	int idToFind = -1337;

	auto startFindId = std::chrono::high_resolution_clock::now();
	int foundIndex = flightSystem.getIndexFromId(idToFind);
	auto endFindId = std::chrono::high_resolution_clock::now();
	std::chrono::duration<double, std::milli> findIdTime = endFindId - startFindId;

	std::cout << "Lookup flight id: " << idToFind << " yielded index: " << foundIndex << ". Time: " << std::fixed << std::setprecision(6)
		<< findIdTime.count() << " ms" << std::endl;
	if (foundIndex == numFlights) {
		std::cout << COLOR_GREEN << "Found the correct index of the newly added flight by ID. Index is " << foundIndex << COLOR_RESET << std::endl;
	}
	else {
		std::cerr << COLOR_RED << "Failed to find the index of the newly added flight by ID. Function returned " << foundIndex << ". It should be: " << numFlights << COLOR_RESET << std::endl;
	}

	// Test removing flights

	// Select random flights to remove
	numFlights = flightSystem.getFlightCount();
	int numFlightsToRemove = std::min(1000, numFlights);
	std::vector<int> removeIds(numFlightsToRemove);

	for (int i = 0; i < numFlightsToRemove; i++) {
		removeIds[i] = idxDist(gen);
	}

	std::cout << "\nRemoving " << numFlightsToRemove << " random flights..." << std::endl;

	// Remove flights in GPU memory
	auto startRemove = std::chrono::high_resolution_clock::now();
	success = flightSystem.removeFlights(removeIds.data(), numFlightsToRemove);
	auto endRemove = std::chrono::high_resolution_clock::now();
	std::chrono::duration<double, std::milli> removeTime = endRemove - startRemove;

	if (!success) {
		std::cerr << COLOR_RED << "Removing flights failed" << COLOR_RESET << std::endl;
		return 1;
	}

	int countAfterRemoval = flightSystem.getFlightCount();

	// Re-run collision detection after removing some flights.
	gpuResults = flightSystem.detectCollisions(box, false);

	int newCollisionCountAfterRemove = gpuResults[0];
	flightSystem.releaseCollisionResults(gpuResults);

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


	std::cout << "\nTesting isRecalculating flag..." << std::endl;

	auto firstRecalcStart = std::chrono::high_resolution_clock::now();
	gpuResults = flightSystem.detectCollisions(box, true);
	auto firstRecalcEnd = std::chrono::high_resolution_clock::now();
	std::chrono::duration<double, std::milli> firstRecalcTime = firstRecalcEnd - firstRecalcStart;

	int collisionsWhenRecalculating = gpuResults[0];

	std::cout << "After first recalc, collisions: " << collisionsWhenRecalculating << ". Time: " << std::fixed << std::setprecision(2)
		<< firstRecalcTime.count() << " ms" << std::endl;
	if (collisionsWhenRecalculating != newCollisionCountAfterRemove) {
		std::cerr << COLOR_RED << "Collision count mismatch when recalculating. Current count: " << collisionsWhenRecalculating << COLOR_RESET << std::endl;
	}
	else {
		std::cout << COLOR_GREEN << "Collision count matches expected value when recalculating" << COLOR_RESET << std::endl;
	}

	// Before detecting collisions again, we keep the indicies of the flights that should be updated
	std::vector<int> updateIds2;
	std::vector<FlightPosition> newPositions2;
	std::vector<int> newDurations2;
	for (int i = 0; i < gpuResults[0]; i++) {
		int idToUpdate = gpuResults[i + 1];
		updateIds2.push_back(idToUpdate);
		newPositions2.push_back({ 1, 2, new int[2] { 3, 4 }, 2 });
		newDurations2.push_back(posDuration(gen));
	}

	flightSystem.releaseCollisionResults(gpuResults);

	auto secondRecalcStart = std::chrono::high_resolution_clock::now();
	gpuResults = flightSystem.detectCollisions(box, true);
	auto secondRecalcEnd = std::chrono::high_resolution_clock::now();
	std::chrono::duration<double, std::milli> secondRecalcTime = secondRecalcEnd - secondRecalcStart;

	int collisionsWhenRecalculatingAgain = gpuResults[0];
	flightSystem.releaseCollisionResults(gpuResults);
	std::cout << "After second recalc, collisions: " << collisionsWhenRecalculatingAgain << ". Time: " << std::fixed << std::setprecision(2)
		<< secondRecalcTime.count() << " ms" << std::endl;
	if (collisionsWhenRecalculatingAgain != 0) {
		std::cerr << COLOR_RED << "When recalculating 2nd time, the count was not 0. Current count: " << collisionsWhenRecalculatingAgain << COLOR_RESET << std::endl;
	}
	else {
		std::cout << COLOR_GREEN << "Collision count when recalculating again is 0 as expected" << COLOR_RESET << std::endl;
	}

	std::cout << "\nTesting update with isRecalculating flag..." << std::endl;

	success = flightSystem.updateFlights(updateIds2.data(), newPositions2.data(), newDurations2.data(), updateIds2.size());
	if (!success) {
		std::cerr << COLOR_RED << "Updating flights to check recalculation failed" << COLOR_RESET << std::endl;
		return 1;
	}

	for (int i = 0; i < newPositions2.size(); i++) {
		delete[] newPositions2[i].z;
	}

	auto thirdRecalcStart = std::chrono::high_resolution_clock::now();
	gpuResults = flightSystem.detectCollisions(box, true);
	auto thirdRecalcEnd = std::chrono::high_resolution_clock::now();
	std::chrono::duration<double, std::milli> thirdRecalcTime = thirdRecalcEnd - thirdRecalcStart;
	if (!success) {
		std::cerr << COLOR_RED << "Collision re-detection (with recalculating flag after update) failed" << COLOR_RESET << std::endl;
		return 1;
	}

	int collisionsWhenRecalculatingAfterUpdate = gpuResults[0];
	flightSystem.releaseCollisionResults(gpuResults);
	std::cout << "After third recalc, collisions: " << collisionsWhenRecalculatingAfterUpdate << ". Time: " << std::fixed << std::setprecision(2)
		<< thirdRecalcTime.count() << " ms" << std::endl;
	if (collisionsWhenRecalculatingAfterUpdate != collisionsWhenRecalculating) {
		std::cerr << COLOR_RED << "Collision count mismatch after updating with recalculating flag did not match expected. "
			<< "Current count: " << collisionsWhenRecalculatingAfterUpdate << ". Expected: " << collisionsWhenRecalculating << COLOR_RESET << std::endl;
	}
	else {
		std::cout << COLOR_GREEN << "Collision count after updating matches collision count before first recalculation as expected" << COLOR_RESET << std::endl;
	}

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
	freeFlights(newFlights);

	gpuResults = emptyFlightSystem.detectCollisions(box, false);

	int emptyCollisionCount = gpuResults[0];
	emptyFlightSystem.releaseCollisionResults(gpuResults);

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

