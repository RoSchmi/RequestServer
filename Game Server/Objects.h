#pragma once

#include <type_traits>

#include <Utilities/SQL/Database.h>

#include "Common.h"

namespace game_server {
	namespace objects {
		template<typename T> struct dependent {
			T& parent;

			exported dependent(T& parent) : parent(parent) {}
			exported virtual ~dependent() = 0 {}
		};

		struct updatable {
			exported updatable();
			exported virtual ~updatable() = 0;

			exported virtual void update(word delta) = 0;

			date_time last_updated;
		};

		struct base_obj : public util::sql::object<obj_id> {
			exported base_obj();
			exported virtual ~base_obj() = 0;

			template<typename T> exported T* clone_as() const {
				static_assert(std::is_base_of<base_obj, T>::value, "typename T must be derived from base_obj.");

				return dynamic_cast<T*>(this->clone());
			}

			exported virtual base_obj* clone() const = 0;

			uint8 obj_type;
			date_time last_updated_by_cache;
		};

		struct map_obj : virtual public base_obj {
			exported virtual ~map_obj() = 0;

			obj_id planet_id;
			coord x;
			coord y;
			size width;
			size height;
		};

		struct owned_obj : virtual public base_obj {
			exported virtual ~owned_obj() = 0;

			owner_id owner;
		};

		struct map_owned_obj : public map_obj, public owned_obj {
			exported virtual ~map_owned_obj() = 0;
		};
	}
}