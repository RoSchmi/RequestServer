#include "ProcessorNode.h"

#include <algorithm>

using namespace std;
using namespace Utilities;
using namespace Utilities::Net;
using namespace GameServer;

ProcessorNode::ProcessorNode(HandlerCreator handlerCreator, ContextCreator contextCreator, string configFileName, ObjectId areaId) {
	this->config.readFile(configFileName.c_str());
	auto& settings = this->config.getRoot();

	this->workers = settings["workerThreads"];
	this->handlerCreator = handlerCreator;
	this->contextCreator = contextCreator;
	this->areaId = areaId;

	auto& dbSettings = settings["Database"];
	this->dbParameters = { dbSettings["host"], dbSettings["port"], dbSettings["dbname"], dbSettings["role"], dbSettings["password"] };

	if (this->contextCreator)
		for (word i = 0; i < this->workers; i++)
			this->dbConnections[i] = this->contextCreator(this->dbParameters);

	this->requestServer = RequestServer(vector<string> { settings["tcpServerPort"].c_str(), settings["webSocketServerPort"].c_str() }, vector<bool> { false, true }, this->workers, IResultCode::RETRY_LATER, ProcessorNode::onRequest, nullptr, ProcessorNode::onDisconnect, this);

	if (this->areaId != 0) {
		TCPConnection brokerNode(settings["brokerAddress"].c_str(), settings["brokerPort"].c_str(), this);
		brokerNode.state = reinterpret_cast<void*>(this->areaId);
		brokerNode.send(reinterpret_cast<uint8*>(&this->areaId), sizeof(this->areaId));
		this->brokerNode = &this->requestServer.adoptConnection(std::move(brokerNode));
		this->authenticatedClients[this->areaId].push_back(this->brokerNode);
	}
}

ProcessorNode::~ProcessorNode() {

}

void ProcessorNode::sendNotification(ObjectId receipientUserId, DataStream&& notification) {
	this->clientsLock.lock();

	auto iter = this->authenticatedClients.find(receipientUserId);
	if (iter != this->authenticatedClients.end())
		for (auto i : iter->second)
			this->requestServer.addToOutgoingQueue(RequestServer::Message(*i, std::move(notification)));

	this->clientsLock.unlock();
}

void ProcessorNode::sendMessageToBroker(DataStream&& message) {
	this->requestServer.addToOutgoingQueue(RequestServer::Message(*this->brokerNode, std::move(message)));
}

DataStream ProcessorNode::createNotification(uint8 category, uint8 type) {
	DataStream notification;
	RequestServer::Message::writeHeader(notification, 0x00, category, type);
	return notification;
}

DataStream ProcessorNode::createBrokerMessage(ObjectId targetAreaId, uint8 category, uint8 type) {
	DataStream notification;
	notification.write(targetAreaId);
	RequestServer::Message::writeHeader(notification, 0x00, category, type);
	return notification;
}

void ProcessorNode::onDisconnect(TCPConnection& client, void* state) {
	ProcessorNode& node = *static_cast<ProcessorNode*>(state);
	ObjectId authenticatedId = reinterpret_cast<ObjectId>(client.state);

	node.clientsLock.lock();

	auto i = node.authenticatedClients.find(authenticatedId);
	if (i != node.authenticatedClients.end()) {
		auto& list = i->second;
		auto j = find(list.begin(), list.end(), &client);

		list.erase(j);

		if (list.size() == 0)
			node.authenticatedClients.erase(authenticatedId);
	}

	node.clientsLock.unlock();
}

RequestServer::RequestResult ProcessorNode::onRequest(TCPConnection& client, void* state, word workerNumber, uint8 requestCategory, uint8 requestMethod, DataStream& parameters, DataStream& response) {
	ProcessorNode& node = *static_cast<ProcessorNode*>(state);
	ResultCode resultCode = IResultCode::SUCCESS;
	ObjectId authenticatedId = reinterpret_cast<ObjectId>(client.state);
	ObjectId startId = authenticatedId;
	auto& context = node.contextCreator ? node.dbConnections[workerNumber] : node.emptyDB;

	auto handler = node.handlerCreator(requestCategory, requestMethod, authenticatedId, context, resultCode);
	if (!handler) {
		resultCode = IResultCode::INVALID_REQUEST_TYPE;
		goto end;
	}

	try {
		handler->deserialize(parameters);
	}
	catch (DataStream::ReadPastEndException&) {
		resultCode = IResultCode::INVALID_PARAMETERS;
		goto end;
	}

	if (node.contextCreator) {
		context->beginTransaction();
		resultCode = handler->process();
		try {
			context->commitTransaction();
		}
		catch (const SQLDatabase::Exception&) {
			context->rollbackTransaction();
			return RequestServer::RequestResult::RETRY_LATER;
		}
	}
	else {
		resultCode = handler->process();
	}


	if (resultCode == IResultCode::NO_RESPONSE)
		return RequestServer::RequestResult::NO_RESPONSE;

	if (authenticatedId != startId) {
		node.clientsLock.lock();
		node.authenticatedClients[authenticatedId].push_back(&client);
		node.clientsLock.unlock();
		client.state = reinterpret_cast<void*>(authenticatedId);
	}

end:
	response.write(resultCode);

	if (resultCode == IResultCode::SUCCESS)
		handler->serialize(response);

	return RequestServer::RequestResult::SUCCESS;
}
