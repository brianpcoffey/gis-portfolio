cmake_minimum_required(VERSION 3.25)

project(geostream_processor
    VERSION 1.0.0
    DESCRIPTION "Geospatial telemetry stream processing kernel"
    LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

# This kernel does NOT enable fast math. The cell index is floor((coordinate + offset) / grid), and
# fast math lets the compiler turn that division into a multiplication by 1.0 / grid (MSVC does
# exactly that under /fp:fast). The two differ in the last bit, so a coordinate on a cell boundary
# (-128.4 at grid 0.1: 515 in managed code, 516 from the fast-math build) landed one cell away from the
# managed fallback. Precise semantics keep every cell index, and the cell centre that follows from
# it, identical to ProcessManaged, which the parity tests assert.
if(MSVC)
    add_compile_options(/O2 /GR-)
else()
    add_compile_options(-O3 -fno-rtti)
endif()

add_library(geostream_processor SHARED
    src/geostream_processor.cpp
)

target_include_directories(geostream_processor PUBLIC
    $<BUILD_INTERFACE:${CMAKE_CURRENT_SOURCE_DIR}/include>
    $<INSTALL_INTERFACE:include>)

if(NOT WIN32)
    target_compile_options(geostream_processor PRIVATE -fvisibility=hidden)
endif()

set_target_properties(geostream_processor PROPERTIES PREFIX "")

if(NOT DEFINED DOTNET_OUTPUT_DIR)
    set(DOTNET_OUTPUT_DIR
        "${CMAKE_CURRENT_SOURCE_DIR}/../../Portfolio.Services/bin/Debug/net10.0"
        CACHE PATH "Directory into which the built shared library is copied")
endif()

# The ASP.NET host loads native libraries from its own output directory, not from
# Portfolio.Services'. Without this second copy the kernels build successfully and the web
# app still reports "Native: No", which is a confusing way to spend an afternoon.
if(NOT DEFINED WEB_OUTPUT_DIR)
    set(WEB_OUTPUT_DIR
        "${CMAKE_CURRENT_SOURCE_DIR}/../../Portfolio.Web/bin/Debug/net10.0"
        CACHE PATH "Additional directory into which the built shared library is copied")
endif()

add_custom_command(TARGET geostream_processor POST_BUILD
    COMMAND ${CMAKE_COMMAND} -E make_directory "${DOTNET_OUTPUT_DIR}"
    COMMAND ${CMAKE_COMMAND} -E copy_if_different
        $<TARGET_FILE:geostream_processor>
        "${DOTNET_OUTPUT_DIR}"
    COMMAND ${CMAKE_COMMAND} -E make_directory "${WEB_OUTPUT_DIR}"
    COMMAND ${CMAKE_COMMAND} -E copy_if_different
        $<TARGET_FILE:geostream_processor>
        "${WEB_OUTPUT_DIR}"
    COMMENT "Copying geostream_processor shared library to ${DOTNET_OUTPUT_DIR}")
