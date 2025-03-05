#ifndef FLIGHT_H
#define FLIGHT_H

// Vector structure for 3D coordinates
struct Vec3 {
	int x, // Time coordinate (unix time seconds)
		y,   // Weather coordinate
		z;   // Airport coordinate
};

struct FlightPosition {
	int x;  // Time coordinate (unix time seconds)
	int y;  // Weather coordinates
	int* z; // Airport coorindates
	int zLength;
};

// Flight structure (point in space)
struct Flight {
	FlightPosition position;
	int id;
	int flightDuration; // In seconds
};

struct BoundingBox {
	Vec3 min;
	Vec3 max;
};

#endif // FLIGHT_H