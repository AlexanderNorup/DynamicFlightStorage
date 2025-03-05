#include "flight_system.h"
#include <cuda_runtime.h>
#include <device_launch_parameters.h>
#include <thrust/sort.h>
#include <thrust/execution_policy.h>
#include <thrust/sequence.h>
#include <thrust/binary_search.h>
#include <thrust/extrema.h>
#include <iostream>

// CUDA kernel to update specific flights
__global__ void updateFlightsKernel(Flight* flights, int* indices, Vec3* newPositions, int* newDurations, int updateCount) {
	int idx = blockIdx.x * blockDim.x + threadIdx.x;
	if (idx < updateCount) {
		int flightIdx = indices[idx];
		flights[flightIdx].position = newPositions[idx];
		flights[flightIdx].flightDuration = newDurations[idx];
	}
}

// CUDA kernel to check collisions between flights and a bounding box
__global__ void checkCollisionsKernel(Flight* flights, int numFlights,
	int* indices, BoundingBox box, int offset, int* collisionResults) {
	int idx = blockIdx.x * blockDim.x + threadIdx.x;
	if (idx < numFlights) {
		int flightIdx = indices[idx + offset];
		Vec3 dep = flights[flightIdx].position;
		int duration = flights[flightIdx].flightDuration;

		// We must check if the box intersects with the flight.
		// The flight is a LINE in 3D space made up of points (dep, dest) where dest.x = dep.x + duration
		// Y and Z coordinates are the same. So if they're not in the box, the flight doesn't intersect the box.

		bool yzCollision = (dep.y >= box.min.y) && (dep.y <= box.max.y) &&
			(dep.z >= box.min.z) && (dep.z <= box.max.z);

		if (!yzCollision) {
			collisionResults[flightIdx] = 0;
			return;
		}

		float xDest = dep.x + duration;
		bool collision =
			((dep.x >= box.min.x) && (dep.x <= box.max.x)) // Checks if flight starts inside the box
			|| (dep.x < box.min.x && xDest > box.min.x); // Checks if flight intersects the box

		collisionResults[flightIdx] = collision ? 1 : 0;
	}
}

// Custom comparison functor for sorting by x position
struct CompareByX {
	Flight* flights;

	CompareByX(Flight* _flights) : flights(_flights) {}

	__host__ __device__ bool operator()(int a, int b) const {
		return flights[a].position.x < flights[b].position.x;
	}
};

// Custom compare function for lower/upper bound search to compare x-coordinate
struct CompareToLowerX {
	Flight* flights;

	CompareToLowerX(Flight* _flights) : flights(_flights) {}

	__host__ __device__ bool operator()(int idx, int val) const {
		return flights[idx].position.x < val;
	}
};

struct CompareByDuration {
	__host__ __device__ bool operator()(Flight a, Flight b)
	{
		return a.flightDuration < b.flightDuration;
	}
};


// Constructor - initialize member variables
FlightSystem::FlightSystem()
	: d_flights(nullptr), d_indices(nullptr), d_collisionResults(nullptr),
	numFlights(0), allocatedFlights(0), initialized(false), deviceId(0) {
	// Get the current CUDA device
	cudaGetDevice(&deviceId);
}

// Destructor - cleanup CUDA resources
FlightSystem::~FlightSystem() {
	cleanup();
}

// Allocate or reallocate device memory
bool FlightSystem::allocateDeviceMemory(int requiredSize) {
	// If we already have enough space, no need to reallocate
	if (allocatedFlights >= requiredSize && d_flights != nullptr &&
		d_indices != nullptr && d_collisionResults != nullptr) {
		return true;
	}

	// Calculate new allocation size (with some extra space for future additions)
	int newSize = requiredSize * 1.5; // Allocate 50% extra space
	if (newSize < 100) newSize = 100; // Minimum allocation

	// Allocate new memory
	Flight* new_d_flights = nullptr;
	int* new_d_indices = nullptr;
	int* new_d_collisionResults = nullptr;

	cudaError_t error = cudaMalloc(&new_d_flights, newSize * sizeof(Flight));
	if (error != cudaSuccess) {
		std::cerr << "Failed to allocate device memory for flights: "
			<< cudaGetErrorString(error) << std::endl;
		return false;
	}

	error = cudaMalloc(&new_d_indices, newSize * sizeof(int));
	if (error != cudaSuccess) {
		std::cerr << "Failed to allocate device memory for indices: "
			<< cudaGetErrorString(error) << std::endl;
		cudaFree(new_d_flights);
		return false;
	}

	error = cudaMalloc(&new_d_collisionResults, newSize * sizeof(int));
	if (error != cudaSuccess) {
		std::cerr << "Failed to allocate device memory for collision results: "
			<< cudaGetErrorString(error) << std::endl;
		cudaFree(new_d_flights);
		cudaFree(new_d_indices);
		return false;
	}

	// If we're reallocating, copy existing data to new memory
	if (d_flights != nullptr && numFlights > 0) {
		cudaMemcpy(new_d_flights, d_flights, numFlights * sizeof(Flight), cudaMemcpyDeviceToDevice);
		cudaMemcpy(new_d_indices, d_indices, numFlights * sizeof(int), cudaMemcpyDeviceToDevice);

		// Free old memory
		cudaFree(d_flights);
		cudaFree(d_indices);
		cudaFree(d_collisionResults);
	}

	// Set the new pointers
	d_flights = new_d_flights;
	d_indices = new_d_indices;
	d_collisionResults = new_d_collisionResults;
	allocatedFlights = newSize;

	return true;
}

// Initialize with flights from host
bool FlightSystem::initialize(Flight* hostFlights, int count) {
	// Clean up previous allocation if any
	cleanup();

	if (count < 0) {
		std::cerr << "Invalid flight count provided for initialization" << std::endl;
		return false;
	}

	numFlights = count;

	// Allocate device memory
	if (!allocateDeviceMemory(count)) {
		return false;
	}

	if (hostFlights != nullptr) {
		// Copy flights to device
		cudaError_t error = cudaMemcpy(d_flights, hostFlights, numFlights * sizeof(Flight), cudaMemcpyHostToDevice);
		if (error != cudaSuccess) {
			std::cerr << "Failed to copy flights to device: "
				<< cudaGetErrorString(error) << std::endl;
			cleanup();
			return false;
		}

		// Initialize indices and sort flights
		sortFlightsByX();
	}

	initialized = true;
	return true;
}

// Add new flights to the system
bool FlightSystem::addFlights(Flight* newFlights, int count) {
	if (!initialized) {
		std::cerr << "Flight system not initialized" << std::endl;
		return false;
	}

	if (count < 0 || newFlights == nullptr) {
		std::cerr << "Invalid flight data provided for addition" << std::endl;
		return false;
	}

	// Check if we need to reallocate memory
	int newTotal = numFlights + count;
	if (newTotal > allocatedFlights) {
		if (!allocateDeviceMemory(newTotal)) {
			return false;
		}
	}

	// Copy new flights to the end of existing flights
	cudaError_t error = cudaMemcpy(d_flights + numFlights, newFlights, count * sizeof(Flight), cudaMemcpyHostToDevice);
	if (error != cudaSuccess) {
		std::cerr << "Failed to copy new flights to device: "
			<< cudaGetErrorString(error) << std::endl;
		return false;
	}

	// Update flight count
	numFlights = newTotal;

	// Re-sort flights by X coordinate
	sortFlightsByX();

	return true;
}

// Remove flights by indices
bool FlightSystem::removeFlights(int* indices, int count) {
	if (!initialized) {
		std::cerr << "Flight system not initialized" << std::endl;
		return false;
	}

	if (count <= 0 || indices == nullptr) {
		std::cerr << "Invalid indices provided for removal" << std::endl;
		return false;
	}

	// Create a temporary host array of all flights
	Flight* hostFlights = new Flight[numFlights];
	if (!hostFlights) {
		std::cerr << "Failed to allocate host memory for flight removal" << std::endl;
		return false;
	}

	// Copy flights from device to host
	cudaError_t error = cudaMemcpy(hostFlights, d_flights, numFlights * sizeof(Flight), cudaMemcpyDeviceToHost);
	if (error != cudaSuccess) {
		std::cerr << "Failed to copy flights to host for removal: " << cudaGetErrorString(error) << std::endl;
		delete[] hostFlights;
		return false;
	}

	// Create a flagged array to mark flights for removal
	bool* toRemove = new bool[numFlights]();
	for (int i = 0; i < count; i++) {
		if (indices[i] >= 0 && indices[i] < numFlights) {
			toRemove[indices[i]] = true;
		}
		else {
			std::cerr << "Invalid flight index for removal: " << indices[i] << std::endl;
		}
	}

	// Create a new array without removed flights
	int newCount = 0;
	Flight* newFlights = new Flight[numFlights];

	for (int i = 0; i < numFlights; i++) {
		if (!toRemove[i]) {
			newFlights[newCount++] = hostFlights[i];
		}
	}

	// Clean up temporary arrays
	delete[] hostFlights;
	delete[] toRemove;

	// Reinitialize with the new array
	bool result = initialize(newFlights, newCount);

	// Clean up the new array
	delete[] newFlights;

	return result;
}

// Update specific flights with new positions
bool FlightSystem::updateFlights(int* indices, Vec3* newPositions, int* newDurations, int updateCount) {
	if (!initialized) {
		std::cerr << "Flight system not initialized" << std::endl;
		return false;
	}

	if (updateCount <= 0 || indices == nullptr || newPositions == nullptr || newDurations == nullptr) {
		std::cerr << "Invalid data provided for flight update" << std::endl;
		return false;
	}

	// Allocate device memory for indices, new positions and durations
	int* d_updateIndices;
	Vec3* d_newPositions;
	int* d_newDurations;

	cudaError_t error = cudaMalloc(&d_updateIndices, updateCount * sizeof(int));
	if (error != cudaSuccess) {
		std::cerr << "Failed to allocate device memory for update indices: "
			<< cudaGetErrorString(error) << std::endl;
		return false;
	}

	error = cudaMalloc(&d_newPositions, updateCount * sizeof(Vec3));
	if (error != cudaSuccess) {
		std::cerr << "Failed to allocate device memory for new positions: "
			<< cudaGetErrorString(error) << std::endl;
		cudaFree(d_updateIndices);
		return false;
	}

	error = cudaMalloc(&d_newDurations, updateCount * sizeof(int));
	if (error != cudaSuccess) {
		std::cerr << "Failed to allocate device memory for new durations: "
			<< cudaGetErrorString(error) << std::endl;
		cudaFree(d_updateIndices);
		cudaFree(d_newPositions);
		return false;
	}

	// Copy indices and new positions to device
	cudaMemcpy(d_updateIndices, indices, updateCount * sizeof(int), cudaMemcpyHostToDevice);
	cudaMemcpy(d_newPositions, newPositions, updateCount * sizeof(Vec3), cudaMemcpyHostToDevice);
	cudaMemcpy(d_newDurations, newDurations, updateCount * sizeof(int), cudaMemcpyHostToDevice);

	// Launch kernel to update flights
	int blockSize = 256;
	int numBlocks = (updateCount + blockSize - 1) / blockSize;

	updateFlightsKernel << <numBlocks, blockSize >> > (
		d_flights, d_updateIndices, d_newPositions, d_newDurations, updateCount);

	// Wait for kernel to finish
	cudaDeviceSynchronize();

	// Check for errors
	error = cudaGetLastError();
	if (error != cudaSuccess) {
		std::cerr << "Error updating flights: " << cudaGetErrorString(error) << std::endl;
		cudaFree(d_updateIndices);
		cudaFree(d_newPositions);
		cudaFree(d_newDurations);
		return false;
	}

	// Free temporary device memory
	cudaFree(d_updateIndices);
	cudaFree(d_newPositions);
	cudaFree(d_newDurations);

	// Re-sort flights by X coordinate after update
	sortFlightsByX();

	return true;
}

// Sort flights by X coordinate for efficient sweep
void FlightSystem::sortFlightsByX() {
	// Initialize indices
	thrust::sequence(thrust::device, d_indices, d_indices + numFlights, 0);

	// Sort flights by their x-coordinate
	thrust::sort(thrust::device, d_indices, d_indices + numFlights,
		CompareByX(d_flights));

	findLongestFlightDuration();
}

void FlightSystem::findLongestFlightDuration() {
	std::vector<Flight> flight(1);
	Flight* d_maxFlight = thrust::max_element(thrust::device, d_flights, d_flights + numFlights, CompareByDuration());
	cudaMemcpy(flight.data(), d_maxFlight, sizeof(Flight), cudaMemcpyDeviceToHost);

	longestFlightDuration = flight[0].flightDuration;
}

int* FlightSystem::getMinMaxIndex(int min, int max) {
	// When we sort by time (X) we also need to consider the flight duration.
	// The nicest way to do that is simply to add the longest flight duration to the min value.
	int adjustedMin = min - longestFlightDuration;

	// This requires d_indicies to be sorted by x-coordinate.
	int* lower = thrust::lower_bound(thrust::device, d_indices, d_indices + numFlights, adjustedMin,
		CompareToLowerX(d_flights));
	// Using Lower_bouund with max + 1 here, because upper_bound would not work. Probably a skill issue, but this works. 
	int* higher = thrust::lower_bound(thrust::device, d_indices, d_indices + numFlights, max + 1,
		CompareToLowerX(d_flights));

	// Calculate the indices by subtracting the adresses we get back from lower_bound.
	int lowerIdx = lower - d_indices;
	int upperIdx = higher - d_indices - 1; // -1 because upper_bound gives position after the last element

	int* result = new int[2];
	result[0] = lowerIdx;
	result[1] = upperIdx;

	return result;
}

// Detect collisions with a bounding box
bool FlightSystem::detectCollisions(const BoundingBox& box, int* collisionResults) {
	if (!initialized) {
		std::cerr << "Flight system not initialized" << std::endl;
		return false;
	}

	// Binary search to find the first flight that might intersect the box
	int* minMaxIndex = getMinMaxIndex(box.min.x, box.max.x);

	int numFlightsInsideBox = minMaxIndex[1] - minMaxIndex[0] + 1;
	int offset = minMaxIndex[0];

	delete[] minMaxIndex; // Free the memory

	// Uncomment this to scan all flights
	//offset = 0;
	//numFlightsInsideBox = numFlights;

#if DEBUG
	std::cout << "[DEBUG] Saving: " << numFlights - numFlightsInsideBox << " flight lookups through Sort and Sweep" << std::endl;
#endif 

	if (numFlightsInsideBox <= 0) {
		return true; // No flights to check, we know they're all outside.
	}

	// Launch collision detection kernel
	int blockSize = 256;
	int numBlocks = (numFlightsInsideBox + blockSize - 1) / blockSize;

	checkCollisionsKernel << <numBlocks, blockSize >> > (
		d_flights, numFlights, d_indices, box, offset, d_collisionResults);

	// Wait for kernel to finish
	cudaDeviceSynchronize();


	// Check for errors
	auto error = cudaGetLastError();
	if (error != cudaSuccess) {
		std::cerr << "Error detecting collisions: " << cudaGetErrorString(error) << std::endl;
		return false;
	}

	// Copy results back to host
	error = cudaMemcpy(collisionResults, d_collisionResults, numFlights * sizeof(int), cudaMemcpyDeviceToHost);
	if (error != cudaSuccess) {
		std::cerr << "Failed to copy collision results to host: "
			<< cudaGetErrorString(error) << std::endl;
		return false;
	}

	return true;
}

// Free all allocated device memory
void FlightSystem::cleanup() {
	if (d_flights) {
		cudaFree(d_flights);
		d_flights = nullptr;
	}

	if (d_indices) {
		cudaFree(d_indices);
		d_indices = nullptr;
	}

	if (d_collisionResults) {
		cudaFree(d_collisionResults);
		d_collisionResults = nullptr;
	}

	initialized = false;
	numFlights = 0;
	allocatedFlights = 0;
}