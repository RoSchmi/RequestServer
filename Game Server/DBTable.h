#pragma once

#include <string>
#include <type_traits>

#include <Utilities/SQL/Database.h>

#include "Objects.h"

namespace game_server {
	template<typename T, typename C> class db_table {
		static_assert(std::is_base_of<util::sql::connection, C>::value && !std::is_same<util::sql::connection, C>::value, "typename C must derive from, but not be, util::sql::connection.");

		public:
			typedef T object_type;
			typedef C connection_type;
			typedef typename C::template binder_type<T, uint64> binder_type;

			db_table(const db_table& other) = delete;
			db_table(db_table&& other) = delete;
			db_table& operator=(db_table&& other) = delete;
			db_table& operator=(const db_table& other) = delete;

			exported virtual ~db_table() = default;

			exported db_table(connection_type& connection, std::string table_name) : db(connection), binder(connection, table_name) {
		
			}

			exported object_type get_by_id(obj_id id) {
				return this->binder.select_by_id(id);
			}

			exported virtual void update(object_type& object) {
				this->binder.update(object);
			}

			exported virtual void insert(object_type& object) {
				this->binder.insert(object);
			}

			exported virtual void remove(object_type& object) {
				this->binder.remove(object);
			}

		protected:
			connection_type& db;
			binder_type binder;
	};
}
