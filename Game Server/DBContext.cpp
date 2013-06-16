#include "DBContext.h"

using namespace Utilities::SQLDatabase;
using namespace GameServer;

IDBContext::IDBContext(const Connection::Parameters& connectionParameters) : connection(connectionParameters) {
	this->nextId = 0;
	this->endOfIssuedIdBlock = 0;
	this->transactionCommitted = false;

	//preload a chunk of IDs
	this->getNewId();
	this->nextId--;
}

IDBContext::~IDBContext() {

}

void IDBContext::beginTransaction() {
	auto query = this->connection.newQuery("START TRANSACTION ISOLATION LEVEL REPEATABLE READ;");
	query.execute();
	this->transactionCommitted = false;
}

void IDBContext::commitTransaction() {
	if (this->transactionCommitted)
		return;

	auto query = this->connection.newQuery("COMMIT TRANSACTION;");
	query.execute();

	this->transactionCommitted = true;
}

void IDBContext::rollbackTransaction() {
	if (this->transactionCommitted)
		return;

	auto query = this->connection.newQuery("ROLLBACK TRANSACTION;");
	query.execute();

	this->transactionCommitted = true;
}

ObjectId IDBContext::getNewId() {
	if (this->nextId < this->endOfIssuedIdBlock) {
		return this->nextId++;
	}
	else {
		auto query = this->connection.newQuery("UPDATE Config SET FieldNumber = FieldNumber + 5000 WHERE FieldName = 'NextId' RETURNING FieldNumber;");
		query.execute();
		query.advanceToNextRow();
		this->endOfIssuedIdBlock = query.getUInt64();
		this->nextId = this->endOfIssuedIdBlock - 5000;
		return this->nextId++;
	}
}

bool IDBContext::wasTransactionCommitted() const {
	return this->transactionCommitted;
}