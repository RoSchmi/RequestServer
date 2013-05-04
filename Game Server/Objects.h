#pragma once

#include <string>
#include <Utilities/Time.h>
#include <Utilities/Array.h>
#include <Utilities/SQLDatabase.h>

namespace GameServer {
	namespace Objects {
		struct IObject : public Utilities::SQLDatabase::IDBObject  {
			exported IObject(uint8 objectType);
			exported virtual ~IObject();

			uint64 ownerId;
			uint8 objectType;
		};

		struct IMap : public IObject {
			exported IMap(uint8 objectType);
			exported virtual ~IMap();
			
			uint64 planetId;
			float64 x;
			float64 y;

			uint32 width;
			uint32 height;
		};
	}
}
