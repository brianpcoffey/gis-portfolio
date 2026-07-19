cmake_minimum_required(VERSION 3.25)

project(facility_location_kernel
    VERSION 1.0.0
    DESCRIPTION "Emergency response facility location kernel: p-median, Teitz-Bart substitution, and weighted coverage statistics"
    LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

# Unlike the other kernels this one does NOT enable fast math. Unreachable demand points
# carry positive infinity through the whole search, and fast math permits the compiler to
# assume infinities never occur. Precise semantics also keep the objective bit-identical
# to the managed fallback, which is what the parity check asserts.
if(MSVC)
    add_compile_options(/O2 /GR-)
else()
    add_compile_options(-O3 -fno-rtti)
endif()

add_library(facility_location_kernel SHARED
    src/facility_location_kernel.cpp
)

target_include_directories(facility_location_kernel PUBLIC
    $<BUILD_INTERFACE:${CMAKE_CURRENT_SOURCE_DIR}/include>
    $<INSTALL_INTERFACE:include>)

if(NOT WIN32)
    target_compile_options(facility_location_kernel PRIVATE -fvisibility=hidden)
endif()

set_target_properties(facility_location_kernel PROPERTIES PREFIX "")

if(NOT DEFINED DOTNET_OUTPUT_DIR)
    set(DOTNET_OUTPUT_DIR
        "${CMAKE_CURRENT_SOURCE_DIR}/../../Portfolio.Services/bin/Debug/net10.0"
        CACHE PATH "Directory into which the built shared library is copied")
endif()

add_custom_command(TARGET facility_location_kernel POST_BUILD
    COMMAND ${CMAKE_COMMAND} -E make_directory "${DOTNET_OUTPUT_DIR}"
    COMMAND ${CMAKE_COMMAND} -E copy_if_different
        $<TARGET_FILE:facility_location_kernel>
        "${DOTNET_OUTPUT_DIR}"
    COMMENT "Copying facility_location_kernel shared library to ${DOTNET_OUTPUT_DIR}")
