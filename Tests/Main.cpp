#include <gtest/gtest.h>

#include <Utilities/SQL/PostgreSQL.h>
#include <libconfig.h++>
#include "../Game Server/Objects.h"
#include "../Game Server/DBTable.h"
#include "../Game Server/CacheProvider.h"
#include "../Game Server/ProcessorNode.h"

using namespace game_server;
using namespace util;

class obj : public objects::map_object {
	public:
		obj() : objects::map_object(0) {};
};

struct foo {
	void begin_transaction() {};
	void commit_transaction() {};
	void rollback_transaction() {};
	bool committed() { return true; };
};


int main(int argc, char **argv) {
	libconfig::Config cfg;
	sql::postgres::connection conn(sql::connection::parameters{ });
	cache_provider cache(0, 0, 0, 0, 0);

	db_table<obj, sql::postgres::connection> dbc(conn, "");

	processor_node node(cfg.getRoot());
	processor_node_db<foo> bar(cfg.getRoot(), nullptr);

	::testing::InitGoogleTest(&argc, argv);
	return RUN_ALL_TESTS();
}
