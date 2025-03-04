#ifndef FLIGHT_H
#define FLIGHT_H

// Vector structure for 3D coordinates
struct Vec3 {
	float x, y, z;
};

// Flight structure (point in space)
struct Flight {
	Vec3 position;
	int id;
};

struct BoundingBox {
	Vec3 min;
	Vec3 max;
};

#endif // FLIGHT_H