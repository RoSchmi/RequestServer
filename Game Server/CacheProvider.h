#pragma once

#include <unordered_map>
#include <vector>
#include <mutex>
#include <utility>
#include <mutex>
#include <memory>
#include <thread>
#include <cstring>

#include <Utilities/Common.h>

#include "Common.h"
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

		std::thread::id lock_holder;
		std::recursive_mutex mtx;
		
		std::unordered_map<owner_id, std::vector<objects::map_object*>> owner_idx;
		std::unordered_map<obj_id, objects::map_object*> id_idx;
		std::unordered_map<coord, std::unordered_map<coord, objects::map_object*>> loc_idx;

		objects::map_object*& get_loc(coord x, coord y);
		
		public: 
			cache_provider(const cache_provider& other) = delete;
			cache_provider(cache_provider&& other) = delete;
			cache_provider& operator=(cache_provider&& other) = delete;
			cache_provider& operator=(const cache_provider& other) = delete;
		
			exported cache_provider(coord start_x, coord start_y, size width, size height, size los_radius);
			exported virtual ~cache_provider();

			exported void lock();
			exported void unlock();
			exported void begin_update(coord x = 0, coord y = 0, size width = 0, size height = 0);
			exported void end_update();
			exported void add(objects::map_object& object);
			exported void remove(objects::map_object& object);
			exported void clamp(coord& start_x, coord& start_y, coord& end_x, coord& end_y);

			exported std::unique_ptr<objects::map_object> get_by_id(obj_id search_id);
			exported std::unique_ptr<objects::map_object> get_at_location(coord x, coord y);
			exported std::vector<std::unique_ptr<objects::map_object>> get_in_area(coord x, coord y, size width = 1, size height = 1);
			exported std::vector<obj_id> get_users_with_los_at(coord x, coord y);

			exported std::vector<std::unique_ptr<objects::map_object>> get_by_owner(owner_id owner);
			exported std::vector<std::unique_ptr<objects::map_object>> get_in_owner_los(owner_id owner);
			exported std::vector<std::unique_ptr<objects::map_object>> get_in_owner_los(owner_id owner, coord x, coord y, size width, size height);

			exported bool is_area_empty(coord x, coord y, size width = 1, size height = 1);
			exported bool is_location_in_los(coord x, coord y, owner_id owner);
			exported bool is_location_in_bounds(coord x, coord y, size width = 1, size height = 1);
			exported bool is_user_present(obj_id user_id);

			template<typename T> exported T get_by_id(obj_id search_id) {
				std::unique_lock<std::recursive_mutex> lck(this->mtx);

				if (this->id_idx.count(search_id) == 0)
					return T();

				T* result = dynamic_cast<T*>(this->id_idx[search_id]);
				if (!result)
					return T();

				return T(*result);
			}

			template<typename T> exported void update_single(std::unique_ptr<T>& object) {
				this->update_single(*object);
			}

			template<typename T> exported void update_single(T& object) {
				this->begin_update();
				this->update(object);
				this->end_update();
			}

			template<typename T> exported void update(std::unique_ptr<T>& object) {
				this->update(*object);
			}

			template<typename T> exported void update(T& object) {
				if (this->lock_holder != std::this_thread::get_id())
					throw util::sql::synchronization_exception();

				objects::map_object* orig = this->id_idx[object.id];

				if (orig->last_updated_by_cache != object.last_updated_by_cache)
					throw util::sql::synchronization_exception();

				for (coord x = object.x; x < object.x + object.width; x++)
					for (coord y = object.y; y < object.y + object.height; y++)
						if (this->get_loc(x, y) != nullptr)
							throw util::sql::synchronization_exception();
								
				if (object.x != orig->x || object.y != orig->y) {
					for (coord x = object.x; x < object.x + object.width; x++)
						for (coord y = object.y; y < object.y + object.height; y++)
							this->get_loc(x, y) = nullptr;

					for (coord x = object.x; x < object.x + object.width; x++)
						for (coord y = object.y; y < object.y + object.height; y++)
							this->get_loc(x, y) = orig;
				}

				object.last_updated_by_cache = date_time::clock::now();

				memcpy(orig, &object, sizeof(T));
			}
	};
}
