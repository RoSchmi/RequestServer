#include "DBContext.h"

#include <stdexcept>

using namespace std;
using namespace Utilities;
using namespace Utilities::SQLDatabase;
using namespace GameServer;

IDBContext::IDBContext(const Connection::Parameters& connectionParameters) : connection(connectionParameters) {
	this->nextId = 0;
	this->endOfIssuedIdBlock = 0;
	this->transactionCommitted = true;

	//preload a chunk of IDs
	this->getNewId();
	this->nextId--;
}

IDBContext::~IDBContext() {

}

void IDBContext::beginTransaction() {
	if (!this->transactionCommitted)
		throw runtime_error("Transaction already begun.");

	this->connection.newQuery("START TRANSACTION ISOLATION LEVEL REPEATABLE READ;").execute();
	this->transactionCommitted = false;
}

void IDBContext::commitTransaction() {
	if (this->transactionCommitted)
		throw runtime_error("Transaction not yet begun.");

	this->connection.newQuery("COMMIT TRANSACTION;").execute();
	this->transactionCommitted = true;
}

void IDBContext::rollbackTransaction() {
	if (this->transactionCommitted)
		throw runtime_error("Transaction not yet begun.");

	this->connection.newQuery("ROLLBACK TRANSACTION;").execute();
	this->transactionCommitted = true;
}

ObjectId IDBContext::getNewId() {
	if (this->nextId >= this->endOfIssuedIdBlock) {
		auto query = this->connection.newQuery("UPDATE Config SET FieldNumber = FieldNumber + 5000 WHERE FieldName = 'NextId' RETURNING FieldNumber;");
		query.execute();
		query.advanceToNextRow();
		this->endOfIssuedIdBlock = query.getUInt64();
		this->nextId = this->endOfIssuedIdBlock - 5000;
	}

	return this->nextId++;
}

bool IDBContext::wasTransactionCommitted() const {
	return this->transactionCommitted;
}