cmake_minimum_required(VERSION 3.16)
project(wlxshm C)

set(CMAKE_C_STANDARD 17)

add_library(wlxshm SHARED xhelpers.h xhelpers.c library.c)

set_target_properties(wlxshm
        PROPERTIES
        LIBRARY_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/../../"
        )

target_link_libraries(wlxshm
        libxcb.so
        libxcb-randr.so
        libxcb-shm.so
        libxcb-xinerama.so
        )