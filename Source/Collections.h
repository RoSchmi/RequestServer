#pragma once

#include <Utilities/SQLDatabase.h>

#include <string>

#include "CacheProvider.h"
#include "Objects.h"

namespace GameServer {
	template<typename T> struct IDBCollection {
		exported IDBCollection(const Utilities::SQLDatabase::Connection& contextConnection, std::string tableName) : dbConnection(contextConnection), tableBinding(tableName, true) { }
		exported virtual ~IDBCollection() { };

		exported T getById(uint64 id) {
			return this->tableBinding.executeSelectById(this->dbConnection, id);
		}

		exported virtual void update(T& object) {
			this->tableBinding.executeUpdate(this->dbConnection, object);
		}

		exported virtual void insert(T& object) {
			this->tableBinding.executeInsert(this->dbConnection, object);
		}

		exported virtual void remove(T& object) {
			this->tableBinding.executeDelete(this->dbConnection, object);
		}

		protected:
			const Utilities::SQLDatabase::Connection& dbConnection;
			Utilities::SQLDatabase::TableBinding<T> tableBinding;
	};

	template<typename T> struct ICachedCollection : public IDBCollection<T> {
		exported ICachedCollection(const Utilities::SQLDatabase::Connection& contextConnection, ICacheProvider& contextCache, std::string tableName) : IDBCollection<T>(contextConnection, tableName), cache(contextCache) { }
		exported virtual ~ICachedCollection() { };

		template<typename U> exported void load(std::string fieldName, U fieldValue) {
			for (auto i : this->tableBinding.executeSelectManyByField(this->dbConnection, fieldName, fieldValue))
				this->cache.add(new T(i));
		}

		exported T* getById(uint64 id) {
			return dynamic_cast<T*>(this->cache.getById(id));
		}

		exported T* getByLocation(GameServer::Objects::Location location) {
			return dynamic_cast<T*>(this->cache.getByLocation(location));
		}

		exported void insert(T& object) {
			IDBCollection<T>::insert(object);
			this->cache.add(new T(object));
		}

		exported void remove(T* object) {
			IDBCollection<T>::remove(object);
			this->cache.remove(object);
			delete object;
		}

		protected:
			ICacheProvider& cache;
	};
}
