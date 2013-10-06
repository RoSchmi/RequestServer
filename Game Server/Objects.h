#pragma once

#include <string>

#include <Utilities/SQL/Database.h>

#include "Common.h"

namespace game_server {
	namespace objects {
		struct data_object : public util::sql::db_object<uint64>  {
			exported data_object(uint8 obj_type);
			exported virtual ~data_object() = 0;

			owner_id owner;
			uint8 obj_type;
		};

		struct map_object : public data_object {
			exported map_object(uint8 obj_type);
			exported virtual ~map_object() = 0;
			
			obj_id planet_id;
			coord x;
			coord y;

			size width;
			size height;
		};
	}
}
