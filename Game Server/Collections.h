#pragma once

#include <string>

#include <Utilities/SQL/Database.h>
#include <Utilities/SQL/TableBinder.h>

#include "cache_provider.h"
#include "objects.h"

namespace game_server {
	template<typename T> struct IDBCollection {
		exported IDBCollection(const util::sql::connection& contextConnection, std::string tableName) : dbConnection(contextConnection), tableBinder(tableName, true) { 
		
		}

		exported virtual ~IDBCollection() { 
		
		}

		exported T get_by_id(obj_id id) {
			return this->tableBinder.executeSelectById(this->dbConnection, id);
		}

		exported virtual void update(T& object) {
			this->tableBinder.executeUpdate(this->dbConnection, object);
		}

		exported virtual void insert(T& object) {
			this->tableBinder.executeInsert(this->dbConnection, object);
		}

		exported virtual void remove(T& object) {
			this->tableBinder.executeDelete(this->dbConnection, object);
		}

		protected:
			const util::sql::connection& dbConnection;
			util::sql::table_binder<T> tableBinder;
	};

	template<typename T> struct ICachedCollection : public IDBCollection<T> {
		exported ICachedCollection(const util::sql::connection& contextConnection, cache_provider& contextCache, std::string tableName) : IDBCollection<T>(contextConnection, tableName), cache(contextCache) { 

		}

		exported virtual ~ICachedCollection() { 
		
		}

		template<typename U> exported void load(std::string fieldName, U fieldValue) {
			for (auto i : this->tableBinder.executeSelectManyByField(this->dbConnection, fieldName, fieldValue))
				this->cache.add(new T(i));
		}

		exported T* get_by_id(obj_id id) {
			return dynamic_cast<T*>(this->cache.get_by_id(id));
		}

		exported T* get_by_location(coord x, coord y) {
			return dynamic_cast<T*>(this->cache.get_by_location(x, y));
		}

		exported void update_location(T* object, coord newX, coord newY) {
			IDBCollection<T>::update(*object);
			this->cache.update_location(object, newX, newY);
		}

		exported void changePlanet(T* object, obj_id newPlanetId) {
			object->planet_id = newPlanetId;
			IDBCollection<T>::update(*object);
			this->dbConnection.commit_transaction();
			this->cache.remove(object);
			delete object;
		}

		exported void insert(T* object) {
			IDBCollection<T>::insert(*object);
			this->cache.add(object);
		}

		exported void remove(T* object) {
			IDBCollection<T>::remove(object);
			this->cache.remove(object);
			delete object;
		}

		protected:
			cache_provider& cache;
	};
}
