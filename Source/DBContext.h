#pragma once

#include <Utilities/SQLDatabase.h>

#include "CacheProvider.h"
#include "Collections.h"
#include "Objects.h"

namespace GameServer {
	class IDBContext {
		uint64 nextId;
		uint64 endOfIssuedIdBlock;
		
		IDBContext(const IDBContext& other);
		IDBContext(IDBContext&& other);
		IDBContext& operator=(const IDBContext& other);
		IDBContext& operator=(IDBContext&& other);

		public:
			IDBContext(const Utilities::SQLDatabase::Connection::Parameters& connectionParameters);
			virtual ~IDBContext();

			void beginTransaction();
			void rollbackTransaction();
			void commitTransaction();
			uint64 getNewId();
			
			Utilities::SQLDatabase::Connection connection;
	};
}
