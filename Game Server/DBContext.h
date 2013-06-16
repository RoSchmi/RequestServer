#pragma once

#include <Utilities/SQLDatabase.h>

#include "CacheProvider.h"
#include "Collections.h"
#include "Objects.h"
#include "Common.h"

namespace GameServer {
	class IDBContext {
		ObjectId nextId;
		ObjectId endOfIssuedIdBlock;
		bool transactionCommitted;
		
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
			exported ObjectId getNewId();
			exported bool wasTransactionCommitted() const;
			
			Utilities::SQLDatabase::Connection connection;
	};
}
