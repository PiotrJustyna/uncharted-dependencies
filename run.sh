#!/bin/bash

# 2023-05-19 PJ:
# --------------
# This application is not intended to run directly on the hosting machine.
# Instead, use docker for building and running it.

docker build -t uncharted-dependencies -f ./dockerfile ./ &&
  docker run -v ./output:/tmp -it --rm uncharted-dependencies