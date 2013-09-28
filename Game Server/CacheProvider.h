#pragma once

#include <map>

#include <Utilities/Common.h>
#include <Utilities/SQLDatabase.h>

#include "Objects.h"

namespace GameServer {
	class ICacheProvider {
		coord startX;
		coord startY;
		coord endX;
		coord endY;
		size width;
		size height;
		size losRadius;

		std::map<OwnerId, std::map<ObjectId, GameServer::Objects::IObject*>> ownerIndex;
		std::map<ObjectId, GameServer::Objects::IObject*> idIndex;
		GameServer::Objects::IMapObject** locationIndex;
		
		ICacheProvider(const ICacheProvider& other);
		ICacheProvider(ICacheProvider&& other);
		ICacheProvider& operator=(const ICacheProvider& other);
		ICacheProvider& operator=(ICacheProvider&& other);
		
		public: 
			exported ICacheProvider(coord startX, coord startY, size width, size height, size losRadius);
			exported virtual ~ICacheProvider();
			 
			exported void remove(GameServer::Objects::IObject* object);
			exported void remove(GameServer::Objects::IMapObject* object);
			exported void add(GameServer::Objects::IObject* object);
			exported void add(GameServer::Objects::IMapObject* object);
			exported void updateLocation(GameServer::Objects::IMapObject* object, coord newX, coord newY);
			exported void clampToDimensions(coord& startX, coord& startY, coord& endX, coord& endY);

			exported GameServer::Objects::IMapObject*& getByLocation(coord x, coord y);
			 
			exported GameServer::Objects::IObject* getById(ObjectId searchId);
			 
			exported std::map<ObjectId, GameServer::Objects::IMapObject*> getInArea(coord x, coord y, size width = 1, size height = 1);
			exported bool isAreaEmpty(coord x, coord y, size width = 1, size height = 1);
			exported bool isLocationInLOS(coord x, coord y, OwnerId ownerId);
			exported bool isLocationInBounds(coord x, coord y, size width = 1, size height = 1);
			exported bool isUserPresent(ObjectId userId);
			 
			exported std::map<ObjectId, GameServer::Objects::IObject*> getByOwner(OwnerId ownerId);
			exported std::map<ObjectId, GameServer::Objects::IMapObject*> getInOwnerLOS(OwnerId ownerId);
			exported std::map<ObjectId, GameServer::Objects::IMapObject*> getInOwnerLOS(OwnerId ownerId, coord x, coord y, size width, size height);
			
			exported std::vector<ObjectId> getUsersWithLOSAt(coord x, coord y);
	};
}
