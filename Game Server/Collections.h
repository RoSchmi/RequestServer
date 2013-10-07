#pragma once

#include <string>
#include <type_traits>

#include <Utilities/SQL/Database.h>

#include "CacheProvider.h"
#include "Objects.h"

namespace game_server {
	template<typename T, typename C> class db_collection {
		static_assert(std::is_base_of<util::sql::connection, C>::value && !std::is_same<util::sql::connection, C>::value, "typename C must derive from, but not be, util::sql::connection.");

		public:
			typedef T object_type;
			typedef C connection_type;
			typedef typename C::template binder_type<T, uint64> binder_type;

			db_collection(const db_collection& other) = delete;
			db_collection(db_collection&& other) = delete;
			db_collection& operator=(db_collection&& other) = delete;
			db_collection& operator=(const db_collection& other) = delete;

			exported virtual ~db_collection() = default;

			exported db_collection(connection_type& connection, std::string table_name) : db(connection), binder(connection, table_name) {
		
			}

			exported object_type get_by_id(obj_id id) {
				return this->binder.select_by_id(id);
			}

			exported virtual void update(object_type& object) {
				this->binder.update(object);
			}

			exported virtual void insert(object_type& object) {
				this->binder.insert(object);
			}

			exported virtual void remove(object_type& object) {
				this->binder.remove(object);
			}

		protected:
			connection_type& db;
			binder_type binder;
	};

	template<typename T, typename C> class cached_collection : public db_collection<T, C> {
		public:
			typedef T object_type;
			typedef C connection_type;
			typedef typename C::template binder_type<T, uint64> binder_type;

			cached_collection(const cached_collection& other) = delete;
			cached_collection(cached_collection&& other) = delete;
			cached_collection& operator=(cached_collection&& other) = delete;
			cached_collection& operator=(const cached_collection& other) = delete;

			exported virtual ~cached_collection() = default;

			exported cached_collection(connection_type& connection, cache_provider& cache, std::string table_name) : db_collection<T, C>(connection, table_name), cache(cache) {

			}

			exported void load(std::vector<object_type>& objects) {
				for (auto i : objects)
					this->cache.add(new object_type(i));
			}

			exported object_type* get_by_id(obj_id id) {
				return dynamic_cast<T*>(this->cache.get_by_id(id));
			}

			exported object_type* get_by_location(coord x, coord y) {
				return dynamic_cast<T*>(this->cache.get_by_location(x, y));
			}

			exported void update_location(object_type* object, coord new_x, coord new_y) {
				this->cache.update_location(object, new_x, new_y);
				this->update(*object);
			}

			exported void change_planet(object_type* object, obj_id new_planet_id) {
				object->planet_id = new_planet_id;
				this->update(*object);
				this->db.commit_transaction();
				this->cache.remove(object);
				delete object;
			}

			exported void insert(object_type* object) {
				this->insert(*object);
				this->cache.add(object);
			}

			exported void remove(object_type* object) {
				this->remove(*object);
				this->cache.remove(object);
				delete object;
			}

		protected:
			cache_provider& cache;
	};
}
