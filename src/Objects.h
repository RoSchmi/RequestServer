#pragma once

#include <type_traits>

#include <Utilities/SQL/Database.h>

#include "Common.h"

namespace game_server {
	namespace objects {
		struct updatable {
			exported updatable();
			exported virtual ~updatable() = 0;

			exported virtual void update(uint64 delta) = 0;

			date_time last_updated;
		};

		struct base_obj : public util::sql::object<obj_id> {
			exported base_obj(obj_type object_type);
			exported virtual ~base_obj() = 0;

			template<typename T> exported T* clone_as() const {
				static_assert(std::is_base_of<base_obj, T>::value, "typename T must be derived from base_obj.");

				return dynamic_cast<T*>(this->clone());
			}

			exported virtual base_obj* clone() const = 0;

			obj_type object_type;
			date_time last_updated_by_cache;
			owner_id owner;
		};

		struct map_obj : public base_obj {
			exported map_obj(obj_type type);
			exported virtual ~map_obj() = 0;

			exported virtual map_obj* clone() const = 0;

			obj_id planet_id;
			coord x;
			coord y;
			size width;
			size height;
		};
	}
}