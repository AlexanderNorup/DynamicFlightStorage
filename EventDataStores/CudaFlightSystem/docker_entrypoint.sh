#!/bin/bash

## This script runs inside the compile container

if ! [ -d /source ]; then
  echo "Source directory does not exist. Please mount the source-code to /source"
  exit 1
fi


if ! [ -d /output ]; then
  echo "Output directory does not exist. Please mount the output directory to /output"
  exit 1
fi


# Compile the CUDA code
cp -r /source /build
cd /build
cmake .

if [ $? -ne 0 ]; then
  echo "Failed to run cmake"
  exit 1
fi

make

if [ $? -ne 0 ]; then
  echo "Compilation failed"
  exit 1
fi


# Copy the compiled binary to the output directory
cp /build/*.so /output

if [ $? -ne 0 ]; then
  echo "No .so files produced"
  exit 1
fi


echo "Successfully copied the compiled binary to /output"