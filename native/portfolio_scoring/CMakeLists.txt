cmake_minimum_required(VERSION 3.25)

project(portfolio_scoring
    VERSION 1.0.0
    DESCRIPTION "HomeFinder property scoring kernel – native C++ library"
    LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

# ---------------------------------------------------------------------------
# Compiler-specific optimisation flags
# ---------------------------------------------------------------------------
if(MSVC)
    # /O2  – full optimisation
    # /arch:AVX2 – enable 256-bit AVX2 for auto-vectorisation of the batch loop
    # /fp:fast   – allow re-association of FP ops (matches -ffast-math intent)
    # /GR-       – no RTTI needed; trim binary size
    add_compile_options(/O2 /arch:AVX2 /fp:fast /GR-)
else()
    # GCC / Clang on Linux/macOS
    add_compile_options(-O3 -march=haswell -ffast-math -fno-rtti)
endif()

# ---------------------------------------------------------------------------
# Shared library target
# ---------------------------------------------------------------------------
add_library(portfolio_scoring SHARED
    src/scoring_kernel.cpp
)

target_include_directories(portfolio_scoring
    PUBLIC
        $<BUILD_INTERFACE:${CMAKE_CURRENT_SOURCE_DIR}/include>
        $<INSTALL_INTERFACE:include>
)

# Ensure symbols are hidden by default on non-Windows platforms so that only
# explicitly PS_API-annotated functions appear in the shared library's export table.
if(NOT WIN32)
    target_compile_options(portfolio_scoring PRIVATE -fvisibility=hidden)
endif()

# Remove the "lib" prefix on Linux so NativeLibrary.Load("portfolio_scoring")
# resolves the same logical name on both platforms.
set_target_properties(portfolio_scoring PROPERTIES PREFIX "")

# ---------------------------------------------------------------------------
# Install rules
# ---------------------------------------------------------------------------
install(TARGETS portfolio_scoring
    RUNTIME DESTINATION bin     # .dll  on Windows
    LIBRARY DESTINATION lib     # .so   on Linux / macOS
    ARCHIVE DESTINATION lib)    # .lib  import library on Windows (optional)

install(DIRECTORY include/ DESTINATION include)

# ---------------------------------------------------------------------------
# Post-build: copy the shared library next to the consuming .NET assembly.
# Override DOTNET_OUTPUT_DIR on the cmake command line when the publish path
# differs from the default below.
#
# Example (from repo root):
#   cmake -B build/native -S native/portfolio_scoring \
#         -DDOTNET_OUTPUT_DIR=Portfolio.Web/bin/Debug/net10.0
#   cmake --build build/native --config Release
# ---------------------------------------------------------------------------
if(NOT DEFINED DOTNET_OUTPUT_DIR)
    set(DOTNET_OUTPUT_DIR
        "${CMAKE_CURRENT_SOURCE_DIR}/../../Portfolio.Services/bin/Debug/net10.0"
        CACHE PATH "Directory into which the built .dll/.so is copied")
endif()

add_custom_command(TARGET portfolio_scoring POST_BUILD
    COMMAND ${CMAKE_COMMAND} -E make_directory "${DOTNET_OUTPUT_DIR}"
    COMMAND ${CMAKE_COMMAND} -E copy_if_different
        $<TARGET_FILE:portfolio_scoring>
        "${DOTNET_OUTPUT_DIR}"
    COMMENT "Copying portfolio_scoring shared library to ${DOTNET_OUTPUT_DIR}")
