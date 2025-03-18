#ifndef FLIGHT_H
#define FLIGHT_H

// Vector structure for 3D coordinates
struct Vec3 {
	int x, // Time coordinate (unix time seconds)
		y,   // Weather coordinate
		z;   // Airport coordinate
};

struct Airport {
	int y,	// Weather level
		z;  // Airport ICAO
};

struct FlightPosition {
	int x;  // Time coordinate (unix time seconds)
	Airport* airport; // Airport coorindates
	int airportLength;
	int airportOffset; // Only used internally
};

// Flight structure (point in space)
struct Flight {
	FlightPosition position;
	int id;
	int flightDuration; // In seconds
	bool isRecalculating;
};

struct BoundingBox {
	Vec3 min;
	Vec3 max;
};

#endif // FLIGHT_H