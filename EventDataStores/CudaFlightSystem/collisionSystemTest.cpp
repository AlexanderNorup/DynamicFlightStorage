#include <iostream>
#include "console_colors.h"
#include "flight_system.h"
#include "collisionSystemTest.h"

void testCase(const char* desc, Flight* flight, Vec3* position, bool shouldCollide, FlightSystem* flightSystem) {
	std::cout << "\n== Collision Test case: " << desc << " ==" << std::endl;

	if (position != nullptr) {
		flight->position.x = position->x;
		flight->position.airport = new Airport[2]{ position->y, position->z };
		flight->position.airportLength = 1;
	}

	std::vector<int> ids(1, 0);
	flightSystem->updateFlights(ids.data(), &flight->position, &flight->flightDuration, 1);
	flightSystem->sortFlightsByX();
	delete[] flight->position.airport;

	BoundingBox box;
	box.min = { -10, -10, -10 };
	box.max = { 10, 10, 10 };


	int* collisionResults = flightSystem->detectCollisions(box, false);
	bool collided = collisionResults[0] > 0;
	flightSystem->releaseCollisionResults(collisionResults);

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
	flight.id = 0;
	flight.flightDuration = 100;
	flight.position.airport = new Airport[3]{ { 0, 0 }, { 0, 0 }, { 0, 0 } };
	flight.position.airportLength = 3;
	Vec3 position;
	bool success = flightsystem.initialize(&flight, 1);

	if (!success) {
		std::cerr << COLOR_RED << "Flight system initialization failed for collision test" << COLOR_RESET << std::endl;
		return;
	}
	delete[] flight.position.airport;

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

	// Test cases with multiple airport-z-values
	flight.position.x = 0;

	flight.position.airport = new Airport[3]{ { 0, 1 }, { 0, 2 }, { 0, 3 } };
	flight.position.airportLength = 3;
	testCase("Multiple airport-z-values, all inside", &flight, nullptr, true, &flightsystem);

	flight.position.airport = new Airport[3]{ { 0, -11 }, { 0, -12 }, { 0, 0 } };
	flight.position.airportLength = 3;
	testCase("Multiple airport-z-values, 1 inside", &flight, nullptr, true, &flightsystem);

	flight.position.airport = new Airport[3]{ { 0, -11 }, { 0, -12 }, { 0, -13 } };
	flight.position.airportLength = 3;
	testCase("Multiple airport-z-values, all outside", &flight, nullptr, false, &flightsystem);

	flight.position.airport = new Airport[3]{ { -11, -11 }, { -12, -12 }, { -13, -13 } };
	flight.position.airportLength = 3;
	testCase("Multiple airport-yz-values, all outside", &flight, nullptr, false, &flightsystem);

	std::cout << "\nCollision system test complete." << std::endl;
	flightsystem.cleanup();
}