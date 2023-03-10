#!/usr/bin/env sh

git describe --tags --dirty --always > Resources/version.txt

cd lib/wlxpw
cmake .
make

cd ../..

cd lib/wlxshm
cmake .
make

cd ../..