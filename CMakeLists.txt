cmake_minimum_required(VERSION 3.12)
project(UnityNativePlugin VERSION 1.0.0 LANGUAGES C)

set(CMAKE_C_STANDARD 11)
set(CMAKE_C_STANDARD_REQUIRED ON)

set(PLUGIN_NAME "FBXNormalPlugin")

set(SRC_FILES
    "Src/ufbx/ufbx.c"
    "Src/ufbx/ufbx.h"
    "Src/fbxplugin.c"
    "Src/fbxplugin.h"
)

if(APPLE)
    add_library(${PLUGIN_NAME} MODULE ${SRC_FILES})
    set_target_properties(${PLUGIN_NAME} PROPERTIES BUNDLE TRUE)
else()
    add_library(${PLUGIN_NAME} SHARED ${SRC_FILES})
endif()

# 出力先の設定
set(UNITY_PLUGINS_DIR "${CMAKE_CURRENT_SOURCE_DIR}/Unity/Plugins")

if(WIN32)
    # 🛠️ ここを修正：Debug や Release などのサブフォルダを強制的に無視して、
    # 指定したフォルダの直下に直接出力するようにプロパティを一括指定します。
    set_target_properties(${PLUGIN_NAME} PROPERTIES 
        RUNTIME_OUTPUT_DIRECTORY "${UNITY_PLUGINS_DIR}"
        RUNTIME_OUTPUT_DIRECTORY_DEBUG "${UNITY_PLUGINS_DIR}"
        RUNTIME_OUTPUT_DIRECTORY_RELEASE "${UNITY_PLUGINS_DIR}"
    )
elseif(APPLE)
    set_target_properties(${PLUGIN_NAME} PROPERTIES LIBRARY_OUTPUT_DIRECTORY "${UNITY_PLUGINS_DIR}")
else()
    set_target_properties(${PLUGIN_NAME} PROPERTIES LIBRARY_OUTPUT_DIRECTORY "${UNITY_PLUGINS_DIR}")
endif()