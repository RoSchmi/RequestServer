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
			exported IDBContext(const Utilities::SQLDatabase::Connection::Parameters& connectionParameters);
			exported virtual ~IDBContext();

			exported void beginTransaction();
			exported void rollbackTransaction();
			exported void commitTransaction();
			exported uint64 getNewId();
			
			Utilities::SQLDatabase::Connection connection;
	};
}
