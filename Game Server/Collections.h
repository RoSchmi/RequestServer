#pragma once

#include <string>

#include <Utilities/SQL/PostgreSQL.h>
#include <Utilities/SQL/PostgreSQLBinder.h>

#include "CacheProvider.h"
#include "Objects.h"

namespace game_server {
	template<typename T> struct db_collection {
		exported db_collection(const util::sql::postgres::connection& connection, std::string table_name) : db(connection), binder(table_name, true) { 
		
		}

		exported virtual ~db_collection() { 
		
		}

		exported T get_by_id(obj_id id) {
			return this->binder.select_by_id(this->db, id);
		}

		exported virtual void update(T& object) {
			this->binder.update(this->db, object);
		}

		exported virtual void insert(T& object) {
			this->binder.insert(this->db, object);
		}

		exported virtual void remove(T& object) {
			this->binder.remove(this->db, object);
		}

		protected:
			const util::sql::postgres::connection& db;
			util::sql::postgres::table_binder<T> binder;
	};

	template<typename T> struct cached_collection : public db_collection<T> {
		exported cached_collection(const util::sql::postgres::connection& connection, cache_provider& cache, std::string table_name) : db_collection<T>(connection, table_name), cache(cache) {

		}

		exported virtual ~cached_collection() { 
		
		}

		template<typename U> exported void load(std::string fieldName, U fieldValue) {
			for (auto i : this->binder.executeSelectManyByField(this->db, fieldName, fieldValue))
				this->cache.add(new T(i));
		}

		exported T* get_by_id(obj_id id) {
			return dynamic_cast<T*>(this->cache.get_by_id(id));
		}

		exported T* get_by_location(coord x, coord y) {
			return dynamic_cast<T*>(this->cache.get_by_location(x, y));
		}

		exported void update_location(T* object, coord new_x, coord new_y) {
			db_collection<T>::update(*object);
			this->cache.update_location(object, new_x, new_y);
		}

		exported void changePlanet(T* object, obj_id new_planet_id) {
			object->planet_id = new_planet_id;
			db_collection<T>::update(*object);
			this->db.commit_transaction();
			this->cache.remove(object);
			delete object;
		}

		exported void insert(T* object) {
			db_collection<T>::insert(*object);
			this->cache.add(object);
		}

		exported void remove(T* object) {
			db_collection<T>::remove(object);
			this->cache.remove(object);
			delete object;
		}

		protected:
			cache_provider& cache;
	};
}
