#include "DBContext.h"

using namespace GameServer;
using namespace Utilities::SQLDatabase;

IDBContext::IDBContext(const Connection::Parameters& connectionParameters) : connection(connectionParameters) {
	this->nextId = 0;
	this->endOfIssuedIdBlock = 0;

	//preload a chunk of IDs
	this->getNewId();
	this->nextId--;
}

IDBContext::~IDBContext() {

}

void IDBContext::beginTransaction() {
	auto query = this->connection.newQuery("START TRANSACTION ISOLATION LEVEL REPEATABLE READ;");
	query.execute();
}

void IDBContext::commitTransaction() {
	auto query = this->connection.newQuery("COMMIT TRANSACTION;");
	query.execute();
}

void IDBContext::rollbackTransaction() {
	auto query = this->connection.newQuery("ROLLBACK TRANSACTION;");
	query.execute();
}

uint64 IDBContext::getNewId() {
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
