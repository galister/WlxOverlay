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

cd lib/StereoKit || exit 1
cmake . -DSK_LINUX_EGL=ON -DSK_PHYSICS=OFF -DSK_BUILD_TESTS=OFF
cmake --build . -j$(nproc)
mv libStereoKitC.so ../../runtimes/linux-x64/native/
cd ../..
