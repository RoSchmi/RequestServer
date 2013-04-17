#pragma once

#include <Utilities/SQLDatabase.h>

#include <string>

#include "CacheProvider.h"
#include "Objects.h"

namespace GameServer {
	template<typename T> struct IDBCollection {
		IDBCollection(const Utilities::SQLDatabase::Connection& contextConnection, std::string tableName) : dbConnection(contextConnection), tableBinding(tableName, true) { }
		virtual ~IDBCollection() { };

		T getById(uint64 id) {
			return this->tableBinding.executeSelectById(this->dbConnection, id);
		}

		virtual void update(T& object) {
			this->tableBinding.executeUpdate(this->dbConnection, object);
		}

		virtual void insert(T& object) {
			this->tableBinding.executeInsert(this->dbConnection, object);
		}

		virtual void remove(T& object) {
			this->tableBinding.executeDelete(this->dbConnection, object);
		}

		protected:
			const Utilities::SQLDatabase::Connection& dbConnection;
			Utilities::SQLDatabase::TableBinding<T> tableBinding;
	};

	template<typename T> struct ICachedCollection : public IDBCollection<T> {
		ICachedCollection(const Utilities::SQLDatabase::Connection& contextConnection, ICacheProvider& contextCache, std::string tableName) : IDBCollection<T>(contextConnection, tableName), cache(contextCache) { }
		virtual ~ICachedCollection() { };

		void load(uint64 planetId) {
			for (auto i : this->tableBinding.executeSelectManyByField(this->dbConnection, "PlanetId", planetId))
				this->cache.add(new T(i));
		}

		T* getById(uint64 id) {
			return dynamic_cast<T*>(this->cache.getById(id));
		}

		T* getByLocation(GameServer::Objects::Location location) {
			return dynamic_cast<T*>(this->cache.getByLocation(location));
		}

		void insert(T& object) {
			IDBCollection<T>::insert(object);
			this->cache.add(new T(object));
		}

		void remove(T* object) {
			IDBCollection<T>::remove(object);
			this->cache.remove(object);
			delete object;
		}

		protected:
			ICacheProvider& cache;
	};
}
