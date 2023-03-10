cmake_minimum_required(VERSION 3.16)
project(wlxpw C)

set(CMAKE_C_STANDARD 17)

add_library(wlxpw SHARED library.c)

target_link_libraries(wlxpw
        libpipewire-0.3.so)
        