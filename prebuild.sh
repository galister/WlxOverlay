#!/usr/bin/env sh

git describe --tags --dirty --always > Resources/version.txt

cd lib/wlxpw || exit 1
cmake .
cmake --build . -j$(nproc)
mv libwlxpw.so ../../
cd ../..

cd lib/wlxshm || exit 1
cmake .
cmake --build . -j$(nproc)
mv libwlxshm.so ../../
cd ../..

