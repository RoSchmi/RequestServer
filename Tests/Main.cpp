#include <gtest/gtest.h>

#include <Utilities/SQL/PostgreSQL.h>
#include <libconfig.h++>
#include <memory>
#include "../Game Server/Objects.h"
#include "../Game Server/CacheProvider.h"
#include "../Game Server/ProcessorNode.h"

using namespace game_server;
using namespace util;
using namespace std;

class obj : public objects::map_object {
	public:
		obj() : objects::map_object(0) {};
		obj* clone() override { return new obj(*this); };
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
	obj o;
	unique_ptr<obj> p;

	cache.update(o);
	cache.update_single(o);
	cache.update(p);
	cache.update_single(p);

	processor_node node(cfg.getRoot());
	processor_node_db<foo> bar(cfg.getRoot(), nullptr);

	::testing::InitGoogleTest(&argc, argv);
	return RUN_ALL_TESTS();
}
