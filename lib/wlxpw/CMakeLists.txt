cmake_minimum_required(VERSION 3.16)
project(wlxpw C)

set(CMAKE_C_STANDARD 17)

find_package(PkgConfig REQUIRED)

pkg_check_modules(WLXPWLIBS REQUIRED IMPORTED_TARGET libpipewire-0.3 libspa-0.2)

add_library(wlxpw SHARED helpers.c library.c)

target_link_libraries(wlxpw
        PkgConfig::WLXPWLIBS)
        