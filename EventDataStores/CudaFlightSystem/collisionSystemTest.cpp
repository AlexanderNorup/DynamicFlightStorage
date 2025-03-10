#include <iostream>
#include "console_colors.h"
#include "flight_system.h"
#include "collisionSystemTest.h"

void testCase(char* desc, Flight* flight, Vec3* position, bool shouldCollide, FlightSystem* flightSystem) {
	std::cout << "\n== Collision Test case: " << desc << " ==" << std::endl;

	if (position != nullptr) {
		flight->position.x = position->x;
		flight->position.y = position->y;
		flight->position.z = new int[3] {position->z, 999, 999}; // The two 999's are just placeholders
		flight->position.zLength = 3;
	}

	std::vector<int> indicies(1, 0);
	flightSystem->updateFlights(indicies.data(), &flight->position, &flight->flightDuration, 1);

	delete[] flight->position.z;

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
	flight.position.z = new int[3] { 0, 0, 0 };
	flight.position.zLength = 3;
	Vec3 position;
	bool success = flightsystem.initialize(&flight, 1);

	if (!success) {
		std::cerr << COLOR_RED << "flight system initialization failed for collision test" << COLOR_RESET << std::endl;
		return;
	}
	delete[] flight.position.z;

	// Run tests
	position = { 0, 0, 0 };
	testCase("Basic collision", &flight, &position, true, &flightsystem);

	position = { 20, 20, 20 };
	testCase("Basic non-collision", &flight, &position, false, &flightsystem);

	flight.flightDuration = 0;
	position = { 11, 0, 0 };
	testCase("Outside x-coord (pos)", &flight, &position, false, &flightsystem);

	position = { -11, 0, 0 };
	testCase("Outside x-coord (neg)", &flight, &position, false, &flightsystem);

	position = { 0, 11, 0 };
	testCase("Outside y-coord (pos)", &flight, &position, false, &flightsystem);

	position = { 0, -11, 0 };
	testCase("Outside y-coord (neg)", &flight, &position, false, &flightsystem);

	position = { 0, 0, 11 };
	testCase("Outside z-coord (pos)", &flight, &position, false, &flightsystem);

	position = { 0, 0, -11 };
	testCase("Outside z-coord (neg)", &flight, &position, false, &flightsystem);

	position = { 0, 11, 11 };
	testCase("Outside y-z-coord (pos)", &flight, &position, false, &flightsystem);

	position = { 0, -11, -11 };
	testCase("Outside y-z-coord (neg)", &flight, &position, false, &flightsystem);

	// Now we test with duration
	flight.flightDuration = 100;
	position = { 0, 0, 0 };
	testCase("Inside long duration", &flight, &position, true, &flightsystem);

	position = { -11, 0, 0 };
	testCase("Outside overlapping duration", &flight, &position, true, &flightsystem);

	position = { -11, 11, 0 };
	testCase("Outside overlapping duration, but wrong y", &flight, &position, false, &flightsystem);

	flight.flightDuration = 5;
	position = { -1, 0, 0 };
	testCase("Purely inside", &flight, &position, true, &flightsystem);

	position = { -11, 0, 0 };
	testCase("From outside, stops inside", &flight, &position, true, &flightsystem);

	// Test cases with multiple z-values
	flight.position.x = 0;
	flight.position.y = 0;

	flight.position.z = new int[3] { 1, 2, 3 };
	flight.position.zLength = 3;
	testCase("Multiple z-values, all inside", &flight, nullptr, true, &flightsystem);

	flight.position.z = new int[3] { -11, -12, 0 };
	flight.position.zLength = 3;
	testCase("Multiple z-values, 1 inside", &flight, nullptr, true, &flightsystem);

	flight.position.z = new int[3] { -11, -12, -13 };
	flight.position.zLength = 3;
	testCase("Multiple z-values, all outside", &flight, nullptr, false, &flightsystem);

	std::cout << "\nCollision system test complete." << std::endl;
}