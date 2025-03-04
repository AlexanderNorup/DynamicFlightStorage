#include <iostream>
#include "console_colors.h"
#include "flight_system.h"
#include "collisionSystemTest.h"

void testCase(char* desc, Flight* flight, bool shouldCollide, FlightSystem* flightSystem) {
	std::cout << "\n== Test case: " << desc << " ==" << std::endl;

	std::vector<int> indicies(1, 0);
	flightSystem->updateFlights(indicies.data(), &flight->position, &flight->flightDuration, 1);

	BoundingBox box;
	box.min = { -10, -10, -10 };
	box.max = { 10, 10, 10 };

	std::vector<int> collisionResults(1, 0);

	bool success2 = flightSystem->detectCollisions(box, collisionResults.data());
	bool collided = collisionResults[0] > 0;
	if (!success2) {
		std::cerr << COLOR_RED << "Collision detection failed" << COLOR_RESET << std::endl;
		return;
	}

	if (shouldCollide == collided) {
		std::cout << COLOR_GREEN << "[PASS] " << COLOR_RESET;
	}
	else
	{
		std::cout << COLOR_RED << "[FAIL] " << COLOR_RESET;
	}

	std::cout << "Detected " << (collided ? "collision" : "no collision") << " in bounding box. Expected collision=" << (shouldCollide ? "true" : "false") << std::endl;
}

void testCollisionSystem() {
	std::cout << "\nTesting the collision system..." << std::endl;
	FlightSystem flightsystem;

	Flight flight;
	flight.id = 1;
	flight.flightDuration = 100;
	flight.position = { 0, 0, 0 };
	bool success = flightsystem.initialize(&flight, 1);

	if (!success) {
		std::cerr << COLOR_RED << "flight system initialization failed for collision test" << COLOR_RESET << std::endl;
		return;
	}

	// Run tests
	testCase("Basic collision", &flight, true, &flightsystem);

	flight.position = { 20, 20, 20 };
	testCase("Basic non-collision", &flight, true, &flightsystem);

	std::cout << "\nCollision system test complete." << std::endl;
}