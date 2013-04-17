#pragma once

#include <Utilities/Common.h>
#include <Utilities/SQLDatabase.h>

#include <map>

#include "Objects.h"

namespace GameServer {
	class ICacheProvider {
		GameServer::Objects::Size size;

		std::map<uint64, std::map<uint64, GameServer::Objects::IObject*>> ownerIndex;
		std::map<uint64, GameServer::Objects::IObject*> idIndex;
		GameServer::Objects::IMap** locationIndex;
		
		ICacheProvider(const ICacheProvider& other);
		ICacheProvider(ICacheProvider&& other);
		ICacheProvider& operator=(const ICacheProvider& other);
		ICacheProvider& operator=(ICacheProvider&& other);

		uint64 locationToOffset(GameServer::Objects::Location location);
		uint64 locationToOffset(float64 x, float64 y);
		uint64 locationToOffsetInt(uint64 x, uint64 y);
		GameServer::Objects::IMap* getAtLocation(uint64 offset);

		public: 
			exported ICacheProvider(GameServer::Objects::Size size, const Utilities::SQLDatabase::Connection& connection);
			exported virtual ~ICacheProvider();
			 
			exported void remove(GameServer::Objects::IObject* object);
			exported void remove(GameServer::Objects::IMap* object);
			exported void add(GameServer::Objects::IObject* object);
			exported void add(GameServer::Objects::IMap* object);
			 
			exported GameServer::Objects::IObject* getById(uint64 searchId);
			 
			exported std::map<uint64, GameServer::Objects::IMap*> getInArea(GameServer::Objects::Location location, GameServer::Objects::Size size);
			exported bool isAreaEmpty(GameServer::Objects::Location location, GameServer::Objects::Size size);
			exported GameServer::Objects::IMap* getByLocation(GameServer::Objects::Location searchLocation);
			exported GameServer::Objects::IMap* getByLocationInt(uint64 x, uint64 y);
			exported GameServer::Objects::IMap* getByLocation(float64 x, float64 y);
			exported bool isLocationInLOS(GameServer::Objects::Location location, uint64 ownerId, uint32 radius);
			 
			exported std::map<uint64, GameServer::Objects::IObject*> getByOwner(uint64 ownerId);
			exported std::map<uint64, GameServer::Objects::IMap*> getInOwnerLOS(uint64 ownerId, uint32 radius);
	};
}
