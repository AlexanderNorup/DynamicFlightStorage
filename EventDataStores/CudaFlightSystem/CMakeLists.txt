cmake_minimum_required(VERSION 3.8 FATAL_ERROR)
project(CUDA_SortAndSweep LANGUAGES CXX CUDA)

# Set C++ standard
set(CMAKE_CXX_STANDARD 14)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Find CUDA
find_package(CUDA REQUIRED)

# Set CUDA flags
set(CUDA_NVCC_FLAGS ${CUDA_NVCC_FLAGS};-std=c++14;-O3;-arch=sm_60)

# Set files to compile
SET(SRCS collisionSystemTest.cpp
    collisionSystemTest.h
    console_colors.h
    flight.h
    flight_system.cu
    flight_system.h
    FlightSystemWrapper.cpp
    FlightSystemWrapper.h
    main.cpp)

# Add executable
cuda_add_library(cuda_flight_system SHARED
    ${SRCS}
)

cuda_add_executable(cuda_flight_system_ex ${SRCS})

# Include directories
include_directories(${CUDA_INCLUDE_DIRS})

# Link libraries
target_link_libraries(cuda_flight_system ${CUDA_LIBRARIES})
target_link_libraries(cuda_flight_system_ex ${CUDA_LIBRARIES})