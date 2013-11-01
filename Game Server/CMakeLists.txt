cmake_minimum_required(VERSION 2.8)
project(game_server)

set(game_sources CacheProvider.cpp BrokerNode.cpp Objects.cpp ProcessorNode.cpp Updater.cpp)

file(GLOB game_headers *.h)

add_library(GameServerObjects OBJECT ${game_sources})
add_library(GameServer SHARED $<TARGET_OBJECTS:GameServerObjects>)
add_library(GameServerStatic STATIC $<TARGET_OBJECTS:GameServerObjects>)

set(CMAKE_CXX_FLAGS "-std=c++11 -fPIC -Wall -pedantic -Wno-overloaded-virtual ${CMAKE_CXX_FLAGS}")

find_package(Threads REQUIRED)
find_package(PostgreSQL REQUIRED)
find_package(PkgConfig REQUIRED)

pkg_check_modules(CONFIG REQUIRED libconfig++)

target_link_libraries(GameServer ${CMAKE_THREAD_LIBS_INIT} ${OPENSSL_LIBRARIES} ${PostgreSQL_LIBRARIES})

include_directories(${PostgreSQL_INCLUDE_DIRS})

if(NOT WIN32)
	target_link_libraries(GameServer rt)
	install(FILES ${game_headers} DESTINATION "include/Game Server")
	install(FILES ${CMAKE_BINARY_DIR}/libGameServer.so DESTINATION lib)
	install(FILES ${CMAKE_BINARY_DIR}/libGameServerStatic.a DESTINATION lib)
endif()
