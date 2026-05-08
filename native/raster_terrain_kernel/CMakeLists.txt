cmake_minimum_required(VERSION 3.25)

project(raster_terrain_kernel
    VERSION 1.0.0
    DESCRIPTION "Raster terrain analysis kernel"
    LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

if(MSVC)
    add_compile_options(/O2 /fp:fast /GR-)
else()
    add_compile_options(-O3 -ffast-math -fno-rtti)
endif()

add_library(raster_terrain_kernel SHARED
    src/raster_terrain_kernel.cpp
)

target_include_directories(raster_terrain_kernel PUBLIC
    $<BUILD_INTERFACE:${CMAKE_CURRENT_SOURCE_DIR}/include>
    $<INSTALL_INTERFACE:include>)

if(NOT WIN32)
    target_compile_options(raster_terrain_kernel PRIVATE -fvisibility=hidden)
endif()

set_target_properties(raster_terrain_kernel PROPERTIES PREFIX "")

if(NOT DEFINED DOTNET_OUTPUT_DIR)
    set(DOTNET_OUTPUT_DIR
        "${CMAKE_CURRENT_SOURCE_DIR}/../../Portfolio.Services/bin/Debug/net10.0"
        CACHE PATH "Directory into which the built shared library is copied")
endif()

add_custom_command(TARGET raster_terrain_kernel POST_BUILD
    COMMAND ${CMAKE_COMMAND} -E make_directory "${DOTNET_OUTPUT_DIR}"
    COMMAND ${CMAKE_COMMAND} -E copy_if_different
        $<TARGET_FILE:raster_terrain_kernel>
        "${DOTNET_OUTPUT_DIR}"
    COMMENT "Copying raster_terrain_kernel shared library to ${DOTNET_OUTPUT_DIR}")
