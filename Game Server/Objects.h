#pragma once

#include <string>

#include <Utilities/SQLDatabase.h>

#include "Common.h"

namespace GameServer {
	namespace Objects {
		struct IObject : public Utilities::SQLDatabase::IDBObject  {
			exported IObject(uint8 objectType);
			exported virtual ~IObject();

			OwnerId ownerId;
			uint8 objectType;
		};

		struct IMap : public IObject {
			exported IMap(uint8 objectType);
			exported virtual ~IMap();
			
			ObjectId planetId;
			Coordinate x;
			Coordinate y;

			Size width;
			Size height;
		};
	}
}
