cmake_minimum_required(VERSION 3.25)

project(cat_risk_kernel
    VERSION 1.0.0
    DESCRIPTION "Catastrophe risk accumulation and event-loss simulation kernel"
    LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

if(MSVC)
    add_compile_options(/O2 /fp:fast /GR-)
else()
    add_compile_options(-O3 -ffast-math -fno-rtti)
endif()

add_library(cat_risk_kernel SHARED
    src/cat_risk_kernel.cpp
)

target_include_directories(cat_risk_kernel PUBLIC
    $<BUILD_INTERFACE:${CMAKE_CURRENT_SOURCE_DIR}/include>
    $<INSTALL_INTERFACE:include>)

if(NOT WIN32)
    target_compile_options(cat_risk_kernel PRIVATE -fvisibility=hidden)
endif()

set_target_properties(cat_risk_kernel PROPERTIES PREFIX "")

if(NOT DEFINED DOTNET_OUTPUT_DIR)
    set(DOTNET_OUTPUT_DIR
        "${CMAKE_CURRENT_SOURCE_DIR}/../../Portfolio.Services/bin/Debug/net10.0"
        CACHE PATH "Directory into which the built shared library is copied")
endif()

add_custom_command(TARGET cat_risk_kernel POST_BUILD
    COMMAND ${CMAKE_COMMAND} -E make_directory "${DOTNET_OUTPUT_DIR}"
    COMMAND ${CMAKE_COMMAND} -E copy_if_different
        $<TARGET_FILE:cat_risk_kernel>
        "${DOTNET_OUTPUT_DIR}"
    COMMENT "Copying cat_risk_kernel shared library to ${DOTNET_OUTPUT_DIR}")
