cmake_minimum_required(VERSION 3.16)
project(wlxpw C)

set(CMAKE_C_STANDARD 17)

add_library(wlxpw SHARED library.c)

set_target_properties(wlxpw
    PROPERTIES
    LIBRARY_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/../../"
)

target_link_libraries(wlxpw
        libpipewire-0.3.so)
        