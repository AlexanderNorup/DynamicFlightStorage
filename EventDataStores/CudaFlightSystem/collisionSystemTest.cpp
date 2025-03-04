#include <iostream>
#include "console_colors.h"
#include "flight_system.h"
#include "collisionSystemTest.h"

void testCase(char* desc, Flight* flight, bool shouldCollide, FlightSystem* flightSystem) {
	std::cout << "\n== Collision Test case: " << desc << " ==" << std::endl;

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
	flight.position = { 0, 0, 0 };
	testCase("Basic collision", &flight, true, &flightsystem);

	flight.position = { 20, 20, 20 };
	testCase("Basic non-collision", &flight, false, &flightsystem);

	flight.flightDuration = 0;
	flight.position = { 11, 0, 0 };
	testCase("Outside x-coord (pos)", &flight, false, &flightsystem);

	flight.position = { -11, 0, 0 };
	testCase("Outside x-coord (neg)", &flight, false, &flightsystem);

	flight.position = { 0, 11, 0 };
	testCase("Outside y-coord (pos)", &flight, false, &flightsystem);

	flight.position = { 0, -11, 0 };
	testCase("Outside y-coord (neg)", &flight, false, &flightsystem);

	flight.position = { 0, 0, 11 };
	testCase("Outside z-coord (pos)", &flight, false, &flightsystem);

	flight.position = { 0, 0, -11 };
	testCase("Outside z-coord (neg)", &flight, false, &flightsystem);

	flight.position = { 0, 11, 11 };
	testCase("Outside y-z-coord (pos)", &flight, false, &flightsystem);

	flight.position = { 0, -11, -11 };
	testCase("Outside y-z-coord (neg)", &flight, false, &flightsystem);

	// Now we test with duration
	flight.flightDuration = 100;
	flight.position = { 0, 0, 0 };
	testCase("Inside long duration", &flight, true, &flightsystem);

	flight.position = { -11, 0, 0 };
	testCase("Outside overlapping duration", &flight, true, &flightsystem);

	flight.position = { -11, 11, 0 };
	testCase("Outside overlapping duration, but wrong y", &flight, false, &flightsystem);

	flight.flightDuration = 5;
	flight.position = { -1, 0, 0 };
	testCase("Purely inside", &flight, true, &flightsystem);

	flight.position = { -11, 0, 0 };
	testCase("From outside, stops inside", &flight, true, &flightsystem);

	std::cout << "\nCollision system test complete." << std::endl;
}