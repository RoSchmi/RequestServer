#include "DBContext.h"

using namespace std;
using namespace util;
using namespace util::sql;
using namespace game_server;

db_context::db_context(const util::sql::connection::parameters& connectionParameters) : conn(connectionParameters) {
	this->next_id = 0;
	this->end_of_block = 0;
	this->was_committed = true;

	//preload a chunk of IDs
	this->get_new_id();
	this->next_id--;
}

void db_context::begin_transaction() {
	if (!this->was_committed)
		throw db_exception("Transaction already begun.");

	this->conn.new_query("START TRANSACTION ISOLATION LEVEL REPEATABLE READ;").execute();
	this->was_committed = false;
}

void db_context::commit_transaction() {
	if (this->was_committed)
		throw db_exception("Transaction not yet begun.");

	this->conn.new_query("COMMIT TRANSACTION;").execute();
	this->was_committed = true;
}

void db_context::rollback_transaction() {
	if (this->was_committed)
		throw db_exception("Transaction not yet begun.");

	this->conn.new_query("ROLLBACK TRANSACTION;").execute();
	this->was_committed = true;
}

obj_id db_context::get_new_id() {
	if (this->next_id >= this->end_of_block) {
		auto query = this->conn.new_query("UPDATE Config SET FieldNumber = FieldNumber + 5000 WHERE FieldName = 'Next_id' RETURNING FieldNumber;");
		query.execute();
		query.advance_row();
		this->end_of_block = query.get_uint64();
		this->next_id = this->end_of_block - 5000;
	}

	return this->next_id++;
}

bool db_context::committed() const {
	return this->was_committed;
}
