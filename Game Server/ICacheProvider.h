#pragma once

#include <unordered_map>
#include <unordered_set>

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

		std::unordered_map<OwnerId, std::unordered_map<ObjectId, Objects::IObject*>> ownerIndex;
		std::unordered_map<ObjectId, Objects::IObject*> idIndex;
		Objects::IMapObject** locationIndex;
		
		public: 
			ICacheProvider(const ICacheProvider& other) = delete;
			ICacheProvider(ICacheProvider&& other) = delete;
			ICacheProvider& operator=(ICacheProvider&& other) = delete;
			ICacheProvider& operator=(const ICacheProvider& other) = delete;
		
			exported ICacheProvider(coord startX, coord startY, size width, size height, size losRadius);
			exported virtual ~ICacheProvider();
			 
			exported void remove(Objects::IObject* object);
			exported void remove(Objects::IMapObject* object);
			exported void add(Objects::IObject* object);
			exported void add(Objects::IMapObject* object);
			exported void updateLocation(Objects::IMapObject* object, coord newX, coord newY);
			exported void clampToDimensions(coord& startX, coord& startY, coord& endX, coord& endY);

			exported Objects::IMapObject*& getByLocation(coord x, coord y);
			exported Objects::IObject* getById(ObjectId searchId);
			exported std::unordered_set<Objects::IMapObject*> getInArea(coord x, coord y, size width = 1, size height = 1);
			exported std::unordered_set<ObjectId> getUsersWithLOSAt(coord x, coord y);

			exported const std::unordered_map<ObjectId, Objects::IObject*> getByOwner(OwnerId ownerId);
			exported std::unordered_set<Objects::IMapObject*> getInOwnerLOS(OwnerId ownerId);
			exported std::unordered_set<Objects::IMapObject*> getInOwnerLOS(OwnerId ownerId, coord x, coord y, size width, size height);

			exported bool isAreaEmpty(coord x, coord y, size width = 1, size height = 1);
			exported bool isLocationInLOS(coord x, coord y, OwnerId ownerId);
			exported bool isLocationInBounds(coord x, coord y, size width = 1, size height = 1);
			exported bool isUserPresent(ObjectId userId);
	};
}
