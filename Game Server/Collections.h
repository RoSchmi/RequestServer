#pragma once

#include <string>
#include <type_traits>

#include <Utilities/SQL/Database.h>
#include <Utilities/SQL/TableBinder.h>

#include "CacheProvider.h"
#include "Objects.h"

namespace game_server {
	template<typename T, typename C, typename B> class db_collection {
		static_assert(std::is_base_of<util::sql::connection, C>::value && !std::is_same<util::sql::connection, C>::value, "typename C must derive from util::sql::connection.");
		static_assert(std::is_base_of<util::sql::table_binder, B>::value && !std::is_same<util::sql::table_binder, B>::value, "typename Q must derive from util::sql::table_binder.");

		public:
			db_collection(const db_collection& other) = delete;
			db_collection(db_collection&& other) = delete;
			db_collection& operator=(db_collection&& other) = delete;
			db_collection& operator=(const db_collection& other) = delete;

			exported virtual ~db_collection() = default;

			exported db_collection(const C& connection, std::string table_name) : db(connection), binder(connection, table_name) { 
		
			}

			exported T get_by_id(obj_id id) {
				return this->binder.select_by_id(id);
			}

			exported virtual void update(T& object) {
				this->binder.update(object);
			}

			exported virtual void insert(T& object) {
				this->binder.insert(object);
			}

			exported virtual void remove(T& object) {
				this->binder.remove(object);
			}

		protected:
			const C& db;
			B<T> binder;
	};

	template<typename T, typename C, typename B> class cached_collection : public db_collection<T, C, B> {
		public:
			cached_collection(const cached_collection& other) = delete;
			cached_collection(cached_collection&& other) = delete;
			cached_collection& operator=(cached_collection&& other) = delete;
			cached_collection& operator=(const cached_collection& other) = delete;

			exported virtual ~cached_collection() = default;

			exported cached_collection(const C& connection, cache_provider& cache, std::string table_name) : db_collection<T, C, B>(connection, table_name), cache(cache) {

			}

			exported void load(std::vector<T>& objects) {
				for (auto i : objects)
					this->cache.add(new T(i));
			}

			exported T* get_by_id(obj_id id) {
				return dynamic_cast<T*>(this->cache.get_by_id(id));
			}

			exported T* get_by_location(coord x, coord y) {
				return dynamic_cast<T*>(this->cache.get_by_location(x, y));
			}

			exported void update_location(T* object, coord new_x, coord new_y) {
				this->cache.update_location(object, new_x, new_y);
				this->update(*object);
			}

			exported void change_planet(T* object, obj_id new_planet_id) {
				object->planet_id = new_planet_id;
				this->update(*object);
				this->db.commit_transaction();
				this->cache.remove(object);
				delete object;
			}

			exported void insert(T* object) {
				this->insert(*object);
				this->cache.add(object);
			}

			exported void remove(T* object) {
				this->remove(*object);
				this->cache.remove(object);
				delete object;
			}

		protected:
			cache_provider& cache;
	};
}
