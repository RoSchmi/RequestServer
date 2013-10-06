#include "IDBContext.h"

#include <stdexcept>

using namespace std;
using namespace util;
using namespace util::sql;
using namespace GameServer;

IDBContext::IDBContext(const util::sql::connection::parameters& connectionParameters) : conn(connectionParameters) {
	this->nextId = 0;
	this->endOfIssuedIdBlock = 0;
	this->transactionCommitted = true;

	//preload a chunk of IDs
	this->getNewId();
	this->nextId--;
}

void IDBContext::beginTransaction() {
	if (!this->transactionCommitted)
		throw runtime_error("Transaction already begun.");

	this->conn.new_query("START TRANSACTION ISOLATION LEVEL REPEATABLE READ;").execute();
	this->transactionCommitted = false;
}

void IDBContext::commitTransaction() {
	if (this->transactionCommitted)
		throw runtime_error("Transaction not yet begun.");

	this->conn.new_query("COMMIT TRANSACTION;").execute();
	this->transactionCommitted = true;
}

void IDBContext::rollbackTransaction() {
	if (this->transactionCommitted)
		throw runtime_error("Transaction not yet begun.");

	this->conn.new_query("ROLLBACK TRANSACTION;").execute();
	this->transactionCommitted = true;
}

ObjectId IDBContext::getNewId() {
	if (this->nextId >= this->endOfIssuedIdBlock) {
		auto query = this->conn.new_query("UPDATE Config SET FieldNumber = FieldNumber + 5000 WHERE FieldName = 'NextId' RETURNING FieldNumber;");
		query.execute();
		query.advance_row();
		this->endOfIssuedIdBlock = query.get_uint64();
		this->nextId = this->endOfIssuedIdBlock - 5000;
	}

	return this->nextId++;
}

bool IDBContext::wasTransactionCommitted() const {
	return this->transactionCommitted;
}
