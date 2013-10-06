#pragma once

#include <string>

#include <Utilities/SQL/Database.h>

#include "Common.h"

namespace GameServer {
	namespace Objects {
		struct IObject : public util::sql::db_object<uint64>  {
			exported IObject(uint8 objectType);
			exported virtual ~IObject();

			OwnerId ownerId;
			uint8 objectType;
		};

		struct IMapObject : public IObject {
			exported IMapObject(uint8 objectType);
			exported virtual ~IMapObject();
			
			ObjectId planetId;
			coord x;
			coord y;

			size width;
			size height;
		};
	}
}
