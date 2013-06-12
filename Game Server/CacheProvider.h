#pragma once

#include <Utilities/Common.h>
#include <Utilities/SQLDatabase.h>

#include <map>

#include "Objects.h"

namespace GameServer {
	class ICacheProvider {
		float64 startX;
		float64 startY;
		float64 endX;
		float64 endY;
		uint32 width;
		uint32 height;

		std::map<uint64, std::map<uint64, GameServer::Objects::IObject*>> ownerIndex;
		std::map<uint64, GameServer::Objects::IObject*> idIndex;
		GameServer::Objects::IMap** locationIndex;
		
		ICacheProvider(const ICacheProvider& other);
		ICacheProvider(ICacheProvider&& other);
		ICacheProvider& operator=(const ICacheProvider& other);
		ICacheProvider& operator=(ICacheProvider&& other);
		
		public: 
			exported ICacheProvider(float64 startX, float64 startY, uint32 width, uint32 height);
			exported virtual ~ICacheProvider();
			 
			exported void remove(GameServer::Objects::IObject* object);
			exported void remove(GameServer::Objects::IMap* object);
			exported void add(GameServer::Objects::IObject* object);
			exported void add(GameServer::Objects::IMap* object);
			exported void updateLocation(GameServer::Objects::IMap* object, float64 newX, float64 newY);

			exported void moveTo(GameServer::Objects::IMap* object, float64 x, float64 y);
			exported GameServer::Objects::IMap*& getByLocation(float64 x, float64 y);
			 
			exported GameServer::Objects::IObject* getById(uint64 searchId);
			 
			exported std::map<uint64, GameServer::Objects::IMap*> getInArea(float64 x, float64 y, uint32 width = 1, uint32 height = 1);
			exported bool isAreaEmpty(float64 x, float64 y, uint32 width = 1, uint32 height = 1);
			exported bool isLocationInLOS(float64 x, float64 y, uint64 ownerId, uint32 radius);
			exported bool isLocationInBounds(float64 x, float64 y, uint32 width = 1, uint32 height = 1);
			 
			exported std::map<uint64, GameServer::Objects::IObject*> getByOwner(uint64 ownerId);
			exported std::map<uint64, GameServer::Objects::IMap*> getInOwnerLOS(uint64 ownerId, uint32 radius);
	};
}
