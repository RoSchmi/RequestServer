#pragma once

#include <Utilities/SQLDatabase.h>

#include "Common.h"

namespace GameServer {
	class IDBContext {
		ObjectId nextId;
		ObjectId endOfIssuedIdBlock;
		bool transactionCommitted;
		
		IDBContext(const IDBContext& other) = delete;
		IDBContext(IDBContext&& other) = delete;
		IDBContext& operator=(const IDBContext& other) = delete;
		IDBContext& operator=(IDBContext&& other) = delete;

		public:
			exported IDBContext(const Utilities::SQLDatabase::Connection::Parameters& connectionParameters);
			exported virtual ~IDBContext();

			exported void beginTransaction();
			exported void rollbackTransaction();
			exported void commitTransaction();
			exported bool wasTransactionCommitted() const;
			exported ObjectId getNewId();
			
			Utilities::SQLDatabase::Connection connection;
	};
}
