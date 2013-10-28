#pragma once

#include <Utilities/SQL/Database.h>

#include "Common.h"

namespace game_server {
	namespace objects {
		template<typename T> struct dependent_object {
			T& parent;
			
			exported dependent_object(T& parent) : parent(parent) { }
			exported virtual ~dependent_object() = 0 { }
		};
		
		struct cached_object {
			exported cached_object();
			exported virtual ~cached_object() = 0;
			
			exported virtual cached_object* clone() = 0;
			
			date_time last_updated_by_cache;		
		};
	
		template<uint8 type> struct object : public util::sql::db_object<uint64>  {
			exported object() : owner(0), obj_type(type) { };
			exported virtual ~object() = 0 { };

			owner_id owner;
			uint8 obj_type;
		};

		struct map_object : public object {
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
