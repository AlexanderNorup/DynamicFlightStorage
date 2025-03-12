FROM nvidia/cuda:12.8.0-devel-ubuntu24.04

# Install CMake
RUN apt update && \
	apt install cmake -y && \
	rm -rf /var/lib/apt/lists/*

COPY docker_entrypoint.sh .

ENTRYPOINT ["/bin/bash", "docker_entrypoint.sh"]