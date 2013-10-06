#pragma once

#include "Common.h"

#include <Utilities/SQL/PostgreSQL.h>

namespace game_server {
	class db_context {
		obj_id next_id;
		obj_id end_of_block;
		bool was_committed;
		
		db_context(const db_context& other) = delete;
		db_context(db_context&& other) = delete;
		db_context& operator=(const db_context& other) = delete;
		db_context& operator=(db_context&& other) = delete;

		public:
			exported db_context(const util::sql::connection::parameters& connectionParameters);
			exported virtual ~db_context() = default;

			exported void begin_transaction();
			exported void rollback_transaction();
			exported void commit_transaction();
			exported bool committed() const;
			exported obj_id get_new_id();
			
			util::sql::postgres::connection conn;
	};
}