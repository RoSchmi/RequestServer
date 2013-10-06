#include "IProcessorNode.h"

#include <algorithm>

using namespace std;
using namespace util;
using namespace util::net;
using namespace GameServer;

IProcessorNode::IProcessorNode(HandlerCreator handlerCreator, ContextCreator contextCreator, libconfig::Setting& settings, ObjectId areaId, void* state) {
	this->workers = settings["workerThreads"];
	this->handlerCreator = handlerCreator;
	this->contextCreator = contextCreator;
	this->areaId = areaId;
	this->state = state;

	auto& dbSettings = settings["Database"];
	this->dbParameters = { dbSettings["host"].c_str(), dbSettings["port"].c_str(), dbSettings["dbname"].c_str(), dbSettings["role"].c_str(), dbSettings["password"].c_str() };

	if (this->contextCreator)
		for (word i = 0; i < this->workers; i++)
			this->dbConnections[i] = this->contextCreator(i, this->dbParameters, this->state);

		this->requestServer = request_server(vector<string> { settings["tcpServerPort"].c_str(), settings["webSocketServerPort"].c_str() }, vector<bool> { false, true }, this->workers, IResultCode::RETRY_LATER, IProcessorNode::onRequestDispatch, IProcessorNode::onConnectDispatch, IProcessorNode::onDisconnectDispatch, this);

	if (this->areaId != 0) {
		tcp_connection brokerNode(settings["brokerAddress"].c_str(), settings["brokerPort"].c_str(), this);
		brokerNode.state = reinterpret_cast<void*>(this->areaId);
		this->brokerNode = &this->requestServer.adopt(std::move(brokerNode));
		this->authenticatedClients[this->areaId].push_back(this->brokerNode);
		this->sendMessageToBroker(this->areaId, this->createMessage(0x00, 0x00));
	}
}

IProcessorNode::~IProcessorNode() {

}

void IProcessorNode::sendMessage(ObjectId receipientUserId, data_stream&& notification) {
	this->clientsLock.lock();

	auto iter = this->authenticatedClients.find(receipientUserId);
	if (iter != this->authenticatedClients.end())
		for (auto i : iter->second)
			this->requestServer.enqueue_outgoing(request_server::message(*i, std::move(notification)));

	this->clientsLock.unlock();
}

void IProcessorNode::sendMessageToBroker(ObjectId targetAreaId, data_stream&& message) {
	message.write(targetAreaId);
	this->requestServer.enqueue_outgoing(request_server::message(*this->brokerNode, std::move(message)));
}

data_stream IProcessorNode::createMessage(uint8 category, uint8 type) {
	data_stream notification;
	request_server::message::write_header(notification, 0x00, category, type);
	return notification;
}

void IProcessorNode::onConnect(tcp_connection& client) {

}

void IProcessorNode::onDisconnect(tcp_connection& client) {
	ObjectId authenticatedId = reinterpret_cast<ObjectId>(client.state);

	this->clientsLock.lock();

	auto i = this->authenticatedClients.find(authenticatedId);
	if (i != this->authenticatedClients.end()) {
		auto& list = i->second;
		auto j = find(list.begin(), list.end(), &client);

		list.erase(j);

		if (list.size() == 0)
			this->authenticatedClients.erase(authenticatedId);
	}

	this->clientsLock.unlock();
}

request_server::request_result IProcessorNode::onRequest(tcp_connection& client, word workerNumber, uint8 requestCategory, uint8 requestMethod, data_stream& parameters, data_stream& response) {
	ResultCode resultCode = IResultCode::SUCCESS;
	ObjectId authenticatedId = reinterpret_cast<ObjectId>(client.state);
	ObjectId startId = authenticatedId;
	auto& context = this->contextCreator ? this->dbConnections[workerNumber] : this->emptyDB;

	auto handler = this->handlerCreator(workerNumber, requestCategory, requestMethod, authenticatedId, context, resultCode, this->state);
	if (!handler) {
		resultCode = IResultCode::INVALID_REQUEST_TYPE;
		goto end;
	}

	try {
		handler->deserialize(parameters);
	}
	catch (data_stream::read_past_end_exception&) {
		resultCode = IResultCode::INVALID_PARAMETERS;
		goto end;
	}

	if (this->contextCreator) {
		context->beginTransaction();
		resultCode = handler->process();
		try {
			context->commitTransaction();
		}
		catch (const sql::db_exception&) {
			context->rollbackTransaction();
			return request_server::request_result::RETRY_LATER;
		}
	}
	else {
		resultCode = handler->process();
	}

	if (resultCode == IResultCode::NO_RESPONSE)
		return request_server::request_result::NO_RESPONSE;

	if (authenticatedId != startId) {
		this->clientsLock.lock();
		this->authenticatedClients[authenticatedId].push_back(&client);
		this->clientsLock.unlock();
		client.state = reinterpret_cast<void*>(authenticatedId);
	}

end:
	response.write(resultCode);

	if (resultCode == IResultCode::SUCCESS)
		handler->serialize(response);

	return request_server::request_result::SUCCESS;
}

request_server::request_result IProcessorNode::onRequestDispatch(tcp_connection& client, void* state, word workerNumber, uint8 requestCategory, uint8 requestMethod, data_stream& parameters, data_stream& response) {
	return reinterpret_cast<IProcessorNode*>(state)->onRequest(client, workerNumber, requestCategory, requestMethod, parameters, response);
}

void IProcessorNode::onDisconnectDispatch(tcp_connection& client, void* state) {
	reinterpret_cast<IProcessorNode*>(state)->onDisconnect(client);
}

void IProcessorNode::onConnectDispatch(tcp_connection& client, void* state) {
	reinterpret_cast<IProcessorNode*>(state)->onConnect(client);
}