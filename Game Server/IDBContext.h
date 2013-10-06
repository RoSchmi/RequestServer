#pragma once

#include "Common.h"

#include <Utilities/SQL/PostgreSQL.h>

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
			exported IDBContext(const util::sql::connection::parameters& connectionParameters);
			exported virtual ~IDBContext() = default;

			exported void beginTransaction();
			exported void rollbackTransaction();
			exported void commitTransaction();
			exported bool wasTransactionCommitted() const;
			exported ObjectId getNewId();
			
			util::sql::postgres::connection conn;
	};
}