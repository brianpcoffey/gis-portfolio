cmake_minimum_required(VERSION 3.25)

project(spatial_overlay_kernel
    VERSION 1.0.0
    DESCRIPTION "Point-in-polygon spatial join (overlay) kernel"
    LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

if(MSVC)
    add_compile_options(/O2 /fp:fast /GR-)
else()
    add_compile_options(-O3 -ffast-math -fno-rtti)
endif()

add_library(spatial_overlay_kernel SHARED
    src/spatial_overlay_kernel.cpp
)

target_include_directories(spatial_overlay_kernel PUBLIC
    $<BUILD_INTERFACE:${CMAKE_CURRENT_SOURCE_DIR}/include>
    $<INSTALL_INTERFACE:include>)

if(NOT WIN32)
    target_compile_options(spatial_overlay_kernel PRIVATE -fvisibility=hidden)
endif()

set_target_properties(spatial_overlay_kernel PROPERTIES PREFIX "")

if(NOT DEFINED DOTNET_OUTPUT_DIR)
    set(DOTNET_OUTPUT_DIR
        "${CMAKE_CURRENT_SOURCE_DIR}/../../Portfolio.Services/bin/Debug/net10.0"
        CACHE PATH "Directory into which the built shared library is copied")
endif()

add_custom_command(TARGET spatial_overlay_kernel POST_BUILD
    COMMAND ${CMAKE_COMMAND} -E make_directory "${DOTNET_OUTPUT_DIR}"
    COMMAND ${CMAKE_COMMAND} -E copy_if_different
        $<TARGET_FILE:spatial_overlay_kernel>
        "${DOTNET_OUTPUT_DIR}"
    COMMENT "Copying spatial_overlay_kernel shared library to ${DOTNET_OUTPUT_DIR}")
