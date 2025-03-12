#!/bin/bash

imageName="cuda_compiler:latest"

if [ -z "$(docker images -q $imageName 2> /dev/null)" ]; then
  echo "Docker image not found. Building the image..."
  docker build -t $imageName .
fi

mkdir -p out

docker run --rm -v /${PWD}:/source:ro -v /${PWD}/out/:/output $imageName

if [ $? -ne 0 ]; then
  echo "Failed to compile the CUDA code"
  exit 1
fi

echo "Successfully compiled the CUDA code. The compiled binary is in the out/ directory"