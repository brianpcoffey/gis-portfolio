cmake_minimum_required(VERSION 3.25)

project(vrp_solver_kernel
    VERSION 1.0.0
    DESCRIPTION "Capacitated vehicle routing with time windows: Clarke-Wright savings plus 2-opt/Or-opt local search"
    LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

# /fp:precise (the default) rather than /fp:fast: the managed fallback has to reproduce
# the solver's accept/reject decisions exactly, and reassociation can flip a comparison.
if(MSVC)
    add_compile_options(/O2 /GR-)
else()
    add_compile_options(-O3 -fno-rtti)
endif()

add_library(vrp_solver_kernel SHARED
    src/vrp_solver_kernel.cpp
)

target_include_directories(vrp_solver_kernel PUBLIC
    $<BUILD_INTERFACE:${CMAKE_CURRENT_SOURCE_DIR}/include>
    $<INSTALL_INTERFACE:include>)

if(NOT WIN32)
    target_compile_options(vrp_solver_kernel PRIVATE -fvisibility=hidden)
endif()

set_target_properties(vrp_solver_kernel PROPERTIES PREFIX "")

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

add_custom_command(TARGET vrp_solver_kernel POST_BUILD
    COMMAND ${CMAKE_COMMAND} -E make_directory "${DOTNET_OUTPUT_DIR}"
    COMMAND ${CMAKE_COMMAND} -E copy_if_different
        $<TARGET_FILE:vrp_solver_kernel>
        "${DOTNET_OUTPUT_DIR}"
    COMMAND ${CMAKE_COMMAND} -E make_directory "${WEB_OUTPUT_DIR}"
    COMMAND ${CMAKE_COMMAND} -E copy_if_different
        $<TARGET_FILE:vrp_solver_kernel>
        "${WEB_OUTPUT_DIR}"
    COMMENT "Copying vrp_solver_kernel shared library to ${DOTNET_OUTPUT_DIR}")
