#pragma once

#include <map>

#include <Utilities/Common.h>
#include <Utilities/SQLDatabase.h>

#include "Objects.h"

namespace GameServer {
	class ICacheProvider {
		Coordinate startX;
		Coordinate startY;
		Coordinate endX;
		Coordinate endY;
		Size width;
		Size height;
		Size losRadius;

		std::map<OwnerId, std::map<ObjectId, GameServer::Objects::IObject*>> ownerIndex;
		std::map<ObjectId, GameServer::Objects::IObject*> idIndex;
		GameServer::Objects::IMap** locationIndex;
		
		ICacheProvider(const ICacheProvider& other);
		ICacheProvider(ICacheProvider&& other);
		ICacheProvider& operator=(const ICacheProvider& other);
		ICacheProvider& operator=(ICacheProvider&& other);
		
		public: 
			exported ICacheProvider(Coordinate startX, Coordinate startY, Size width, Size height, Size losRadius);
			exported virtual ~ICacheProvider();
			 
			exported void remove(GameServer::Objects::IObject* object);
			exported void remove(GameServer::Objects::IMap* object);
			exported void add(GameServer::Objects::IObject* object);
			exported void add(GameServer::Objects::IMap* object);
			exported void updateLocation(GameServer::Objects::IMap* object, Coordinate newX, Coordinate newY);
			exported void clampToDimensions(Coordinate& startX, Coordinate& startY, Coordinate& endX, Coordinate& endY);

			exported GameServer::Objects::IMap*& getByLocation(Coordinate x, Coordinate y);
			 
			exported GameServer::Objects::IObject* getById(ObjectId searchId);
			 
			exported std::map<ObjectId, GameServer::Objects::IMap*> getInArea(Coordinate x, Coordinate y, Size width = 1, Size height = 1);
			exported bool isAreaEmpty(Coordinate x, Coordinate y, Size width = 1, Size height = 1);
			exported bool isLocationInLOS(Coordinate x, Coordinate y, OwnerId ownerId);
			exported bool isLocationInBounds(Coordinate x, Coordinate y, Size width = 1, Size height = 1);
			 
			exported std::map<ObjectId, GameServer::Objects::IObject*> getByOwner(OwnerId ownerId);
			exported std::map<ObjectId, GameServer::Objects::IMap*> getInOwnerLOS(OwnerId ownerId);
			exported std::map<ObjectId, GameServer::Objects::IMap*> getInOwnerLOS(OwnerId ownerId, Coordinate x, Coordinate y, Size width, Size height);
			
			exported std::vector<ObjectId> getUsersWithLOSAt(Coordinate x, Coordinate y);
	};
}
