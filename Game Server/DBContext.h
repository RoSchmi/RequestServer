#pragma once

#include <memory>

#include <Utilities/SQL/Database.h>

namespace game_server {
	class db_context {
		public:
			exported db_context();
			exported virtual ~db_context() = default;

			std::unique_ptr<util::sql::connection> conn;
	};
}