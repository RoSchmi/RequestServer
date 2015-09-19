#pragma once

#include <type_traits>

#include <Utilities/SQL/Database.h>

#include "Common.h"

namespace game_server {
	namespace objects {
		struct updatable {
			updatable();
			virtual ~updatable() = 0;

			virtual void update(uint64 delta) = 0;

			date_time last_updated;
		};

		struct base_obj : public util::sql::object<obj_id> {
			base_obj(obj_type object_type);
			virtual ~base_obj() = 0;

			template<typename T> T* clone_as() const {
				static_assert(std::is_base_of<base_obj, T>::value, "typename T must be derived from base_obj.");

				return dynamic_cast<T*>(this->clone());
			}

			virtual base_obj* clone() const = 0;

			obj_type object_type;
			date_time last_updated_by_cache;
			owner_id owner;
		};

		struct map_obj : public base_obj {
			map_obj(obj_type type);
			virtual ~map_obj() = 0;

			virtual map_obj* clone() const = 0;

			obj_id planet_id;
			coord x;
			coord y;
			size width;
			size height;
		};
	}
}