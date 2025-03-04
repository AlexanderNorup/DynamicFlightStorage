#include "flight_system.h"
#include <cuda_runtime.h>
#include <device_launch_parameters.h>
#include <thrust/sort.h>
#include <thrust/device_ptr.h>
#include <thrust/execution_policy.h>
#include <thrust/sequence.h>
#include <thrust/copy.h>
#include <thrust/remove.h>
#include <iostream>

// CUDA kernel to update specific flights
__global__ void updateFlightsKernel(Flight* flights, int* indices, Vec3* newPositions, int updateCount) {
	int idx = blockIdx.x * blockDim.x + threadIdx.x;
	if (idx < updateCount) {
		int flightIdx = indices[idx];
		flights[flightIdx].position = newPositions[idx];
	}
}

// CUDA kernel to check collisions between flights and a bounding box
__global__ void checkCollisionsKernel(Flight* flights, int numFlights,
	int* indices, BoundingBox box, int* collisionResults) {
	int idx = blockIdx.x * blockDim.x + threadIdx.x;
	if (idx < numFlights) {
		int flightIdx = indices[idx];
		Vec3 pos = flights[flightIdx].position;

		// Check if point is inside the bounding box
		bool collision =
			(pos.x >= box.min.x) && (pos.x <= box.max.x) &&
			(pos.y >= box.min.y) && (pos.y <= box.max.y) &&
			(pos.z >= box.min.z) && (pos.z <= box.max.z);

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

	if (count <= 0 || hostFlights == nullptr) {
		std::cerr << "Invalid flight data provided for initialization" << std::endl;
		return false;
	}

	numFlights = count;

	// Allocate device memory
	if (!allocateDeviceMemory(count)) {
		return false;
	}

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

	initialized = true;
	return true;
}

// Add new flights to the system
bool FlightSystem::addFlights(Flight* newFlights, int count) {
	if (!initialized) {
		std::cerr << "Flight system not initialized" << std::endl;
		return false;
	}

	if (count <= 0 || newFlights == nullptr) {
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
bool FlightSystem::updateFlights(int* indices, Vec3* newPositions, int updateCount) {
	if (!initialized) {
		std::cerr << "Flight system not initialized" << std::endl;
		return false;
	}

	if (updateCount <= 0 || indices == nullptr || newPositions == nullptr) {
		std::cerr << "Invalid data provided for flight update" << std::endl;
		return false;
	}

	// Allocate device memory for indices and new positions
	int* d_updateIndices;
	Vec3* d_newPositions;

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

	// Copy indices and new positions to device
	cudaMemcpy(d_updateIndices, indices, updateCount * sizeof(int), cudaMemcpyHostToDevice);
	cudaMemcpy(d_newPositions, newPositions, updateCount * sizeof(Vec3), cudaMemcpyHostToDevice);

	// Launch kernel to update flights
	int blockSize = 256;
	int numBlocks = (updateCount + blockSize - 1) / blockSize;

	updateFlightsKernel << <numBlocks, blockSize >> > (
		d_flights, d_updateIndices, d_newPositions, updateCount);

	// Wait for kernel to finish
	cudaDeviceSynchronize();

	// Check for errors
	error = cudaGetLastError();
	if (error != cudaSuccess) {
		std::cerr << "Error updating flights: " << cudaGetErrorString(error) << std::endl;
		cudaFree(d_updateIndices);
		cudaFree(d_newPositions);
		return false;
	}

	// Free temporary device memory
	cudaFree(d_updateIndices);
	cudaFree(d_newPositions);

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
}

// Detect collisions with a bounding box
bool FlightSystem::detectCollisions(const BoundingBox& box, int* collisionResults) {
	if (!initialized) {
		std::cerr << "Flight system not initialized" << std::endl;
		return false;
	}

	// Launch collision detection kernel
	int blockSize = 256;
	int numBlocks = (numFlights + blockSize - 1) / blockSize;

	checkCollisionsKernel << <numBlocks, blockSize >> > (
		d_flights, numFlights, d_indices, box, d_collisionResults);

	// Wait for kernel to finish
	cudaDeviceSynchronize();

	// Check for errors
	cudaError_t error = cudaGetLastError();
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