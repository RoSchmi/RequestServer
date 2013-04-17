cmake_minimum_required(VERSION 2.8)
project(GameServer)

set(game_sources CacheProvider.cpp DBContext.cpp Objects.cpp)

file(GLOB game_headers *.h)

add_library(GameServer SHARED ${game_sources})

set(CMAKE_CXX_FLAGS "-std=c++11 -Wall -pedantic -Wno-overloaded-virtual ${CMAKE_CXX_FLAGS}")

find_package(Threads REQUIRED)
find_package(PostgreSQL REQUIRED)
find_package(PkgConfig REQUIRED)

pkg_check_modules(CONFIG REQUIRED libconfig++)

target_link_libraries(GameServer ${CMAKE_THREAD_LIBS_INIT}
	${OPENSSL_LIBRARIES} ${PostgreSQL_LIBRARIES})

include_directories(${PostgreSQL_INCLUDE_DIRS})

if(NOT WIN32)
	target_link_libraries(GameServer rt)
	install(FILES ${game_headers} DESTINATION include/GameServer)
	install(FILES ${CMAKE_BINARY_DIR}/libGameServer.so DESTINATION lib)
endif()
