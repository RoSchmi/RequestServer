#pragma once

#include <unordered_map>
#include <unordered_set>

#include <Utilities/Common.h>
#include <Utilities/SQL/Database.h>

#include "Objects.h"

namespace game_server {
	class cache_provider {
		coord start_x;
		coord start_y;
		coord end_x;
		coord end_y;
		size width;
		size height;
		size los_radius;

		std::unordered_map<owner_id, std::unordered_map<obj_id, objects::data_object*>> owner_idx;
		std::unordered_map<obj_id, objects::data_object*> id_idx;
		objects::map_object** loc_idx;
		
		public: 
			cache_provider(const cache_provider& other) = delete;
			cache_provider(cache_provider&& other) = delete;
			cache_provider& operator=(cache_provider&& other) = delete;
			cache_provider& operator=(const cache_provider& other) = delete;
		
			exported cache_provider(coord start_x, coord start_y, size width, size height, size los_radius);
			exported virtual ~cache_provider();
			 
			exported void remove(objects::data_object* object);
			exported void remove(objects::map_object* object);
			exported void add(objects::data_object* object);
			exported void add(objects::map_object* object);
			exported void update_location(objects::map_object* object, coord new_x, coord new_y);
			exported void clamp(coord& start_x, coord& start_y, coord& end_x, coord& end_y);

			exported objects::map_object*& get_by_location(coord x, coord y);
			exported objects::data_object* get_by_id(obj_id search_id);
			exported std::unordered_set<objects::map_object*> get_in_area(coord x, coord y, size width = 1, size height = 1);
			exported std::unordered_set<obj_id> get_users_with_los_at(coord x, coord y);

			exported const std::unordered_map<obj_id, objects::data_object*> get_by_owner(owner_id owner);
			exported std::unordered_set<objects::map_object*> get_in_owner_los(owner_id owner);
			exported std::unordered_set<objects::map_object*> get_in_owner_los(owner_id owner, coord x, coord y, size width, size height);

			exported bool is_area_empty(coord x, coord y, size width = 1, size height = 1);
			exported bool is_location_in_los(coord x, coord y, owner_id owner);
			exported bool is_location_in_bounds(coord x, coord y, size width = 1, size height = 1);
			exported bool is_user_present(obj_id user_id);
	};
}
