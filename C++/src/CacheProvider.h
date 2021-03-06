#pragma once

#include <unordered_map>
#include <unordered_set>
#include <vector>
#include <mutex>
#include <utility>
#include <mutex>
#include <memory>
#include <thread>
#include <type_traits>

#include <ArkeIndustries.CPPUtilities/Common.h>

#include "Common.h"
#include "Objects.h"

namespace game_server {
	class cache_provider {
		coord start_x;
		coord start_y;
		coord end_x;
		coord end_y;
		dimension width;
		dimension height;
		dimension los_radius;

		std::thread::id lock_holder;
		std::recursive_mutex mtx;

		std::vector<objects::updatable*> updatable_idx;
		std::unordered_map<obj_id, objects::base_obj*> id_idx;
		std::unordered_map<owner_id, std::vector<objects::base_obj*>> owner_idx;
		std::unordered_map<coord, std::unordered_map<coord, objects::map_obj*>> loc_idx;

		objects::map_obj*& get_loc(coord x, coord y);
		bool is_root_object(objects::map_obj* obj, coord x, coord y);

		void add_internal(objects::base_obj* object);
		bool add_internal(objects::map_obj* object);
		void remove_internal(objects::base_obj* object);
		void remove_internal(objects::map_obj* object);

		objects::updatable* get_next_updatable(word position);

		friend class cache_updater;

		public:
			cache_provider(const cache_provider& other) = delete;
			cache_provider(cache_provider&& other) = delete;
			cache_provider& operator=(cache_provider&& other) = delete;
			cache_provider& operator=(const cache_provider& other) = delete;

			cache_provider(coord start_x, coord start_y, dimension width, dimension height, dimension los_radius);
			cache_provider() = default;
			virtual ~cache_provider();

			void set_bounds(coord start_x, coord start_y, dimension width, dimension height, dimension los_radius);

			void lock();
			void unlock();
			void begin_update(coord x = 0, coord y = 0, dimension width = 0, dimension height = 0);
			void end_update();

			void clamp(coord& start_x, coord& start_y, coord& end_x, coord& end_y);

			std::unique_ptr<objects::base_obj> get_by_id(obj_id search_id);
			std::unique_ptr<objects::map_obj> get_at_location(coord x, coord y);
			std::unordered_map<obj_id, std::unique_ptr<objects::map_obj>> get_in_area(coord x, coord y, dimension width = 1, dimension height = 1);
			std::unordered_set<obj_id> get_users_with_los_at(coord x, coord y);

			std::unordered_map<obj_id, std::unique_ptr<objects::base_obj>> get_by_owner(owner_id owner);
			std::unordered_map<obj_id, std::unique_ptr<objects::map_obj>> get_in_owner_los(owner_id owner);
			std::unordered_map<obj_id, std::unique_ptr<objects::map_obj>> get_in_owner_los(owner_id owner, coord x, coord y, dimension width, dimension height);

			bool is_area_empty(coord x, coord y, dimension width = 1, dimension height = 1);
			bool is_location_in_los(coord x, coord y, owner_id owner);
			bool is_location_in_bounds(coord x, coord y, dimension width = 1, dimension height = 1);
			bool is_user_present(obj_id user_id);

			template<typename T> T get_by_id(obj_id search_id) {
				static_assert(std::is_base_of<objects::base_obj, T>::value, "typename T must derive from objects::base_obj.");

				std::unique_lock<std::recursive_mutex> lck(this->mtx);

				if (this->id_idx.count(search_id) == 0)
					return T();

				T* result = dynamic_cast<T*>(this->id_idx[search_id]);
				if (!result)
					return T();

				return T(*result);
			}

			template<typename T> void add(T& type) {
				static_assert(std::is_base_of<objects::base_obj, T>::value, "typename T must derive from objects::base_obj.");

				if (this->lock_holder != std::this_thread::get_id())
					throw util::sql::synchronization_exception();

				T* new_obj = type.clone();
				objects::base_obj* as_base = new_obj;
				objects::map_obj* as_map = dynamic_cast<objects::map_obj*>(new_obj);

				if (as_map) {
					if (!this->add_internal(as_map)) {
						delete new_obj;
						throw util::sql::synchronization_exception();
					}
				}

				this->add_internal(as_base);
			}

			template<typename T> void remove(T& type) {
				static_assert(std::is_base_of<objects::base_obj, T>::value, "typename T must derive from objects::base_obj.");

				if (this->lock_holder != std::this_thread::get_id())
					throw util::sql::synchronization_exception();

				objects::base_obj* as_base = this->id_idx[type.id];

				if (type.last_updated_by_cache != as_base->last_updated_by_cache)
					throw util::sql::synchronization_exception();

				objects::map_obj* as_map = dynamic_cast<objects::map_obj*>(as_base);

				if (as_map)
					this->remove_internal(as_map);

				this->remove_internal(as_base);
			}

			template<typename T> void update(T& object) {
				static_assert(std::is_base_of<objects::base_obj, T>::value, "typename T must derive from objects::base_obj.");

				if (this->lock_holder != std::this_thread::get_id())
					throw util::sql::synchronization_exception();

				objects::base_obj* orig = this->id_idx[object.id];
				objects::map_obj* orig_as_map = dynamic_cast<objects::map_obj*>(orig);
				objects::map_obj* obj_as_map = dynamic_cast<objects::map_obj*>(&object);
				bool loc_changed = obj_as_map && (obj_as_map->x != orig_as_map->x || obj_as_map->y != orig_as_map->y);
				bool own_changed = orig->owner != object.owner;

				if (orig->last_updated_by_cache != object.last_updated_by_cache)
					throw util::sql::synchronization_exception();

				if (loc_changed)
					for (coord x = obj_as_map->x; x < obj_as_map->x + obj_as_map->width; x++)
						for (coord y = obj_as_map->y; y < obj_as_map->y + obj_as_map->height; y++)
							if (this->get_loc(x, y) != nullptr && this->get_loc(x, y) != orig_as_map)
								throw util::sql::synchronization_exception();

				object.last_updated_by_cache = date_time::clock::now();

				if (loc_changed)
					this->remove_internal(orig_as_map);

				if (own_changed)
					this->remove_internal(orig);

				*orig = object;

				if (loc_changed)
					this->add_internal(orig_as_map);

				if (own_changed)
					this->add_internal(orig);
			}

			template<typename T> void add(std::unique_ptr<T>& object) {
				static_assert(std::is_base_of<objects::base_obj, T>::value, "typename T must derive from objects::base_obj.");

				this->add(*object);
			}

			template<typename T> void remove(std::unique_ptr<T>& object) {
				static_assert(std::is_base_of<objects::base_obj, T>::value, "typename T must derive from objects::base_obj.");

				this->remove(*object);
			}

			template<typename T> void update(std::unique_ptr<T>& object) {
				static_assert(std::is_base_of<objects::base_obj, T>::value, "typename T must derive from objects::base_obj.");

				this->update(*object);
			}

			template<typename T> void add_single(std::unique_ptr<T>& object) {
				static_assert(std::is_base_of<objects::base_obj, T>::value, "typename T must derive from objects::base_obj.");

				this->begin_update();
				this->add(object);
				this->end_update();
			}

			template<typename T> void add_single(T& object) {
				static_assert(std::is_base_of<objects::base_obj, T>::value, "typename T must derive from objects::base_obj.");

				this->begin_update();
				this->add(object);
				this->end_update();
			}

			template<typename T> void remove_single(std::unique_ptr<T>& object) {
				static_assert(std::is_base_of<objects::base_obj, T>::value, "typename T must derive from objects::base_obj.");

				this->begin_update();
				this->add(object);
				this->end_update();
			}

			template<typename T> void remove_single(T& object) {
				static_assert(std::is_base_of<objects::base_obj, T>::value, "typename T must derive from objects::base_obj.");

				this->begin_update();
				this->add(object);
				this->end_update();
			}

			template<typename T> void update_single(std::unique_ptr<T>& object) {
				static_assert(std::is_base_of<objects::base_obj, T>::value, "typename T must derive from objects::base_obj.");

				this->begin_update();
				this->update(object);
				this->end_update();
			}

			template<typename T> void update_single(T& object) {
				static_assert(std::is_base_of<objects::base_obj, T>::value, "typename T must derive from objects::base_obj.");

				this->begin_update();
				this->update(object);
				this->end_update();
			}
	};
}
