#include "IProcessorNode.h"

#include <algorithm>

using namespace std;
using namespace Utilities;
using namespace Utilities::Net;
using namespace GameServer;

IProcessorNode::IProcessorNode(HandlerCreator handlerCreator, ContextCreator contextCreator, string configFileName, ObjectId areaId, void* state) {
	this->config.readFile(configFileName.c_str());
	auto& settings = this->config.getRoot();

	this->workers = settings["workerThreads"];
	this->handlerCreator = handlerCreator;
	this->contextCreator = contextCreator;
	this->areaId = areaId;
	this->state = state;

	auto& dbSettings = settings["Database"];
	this->dbParameters = { dbSettings["host"], dbSettings["port"], dbSettings["dbname"], dbSettings["role"], dbSettings["password"] };

	if (this->contextCreator)
		for (word i = 0; i < this->workers; i++)
			this->dbConnections[i] = this->contextCreator(this->dbParameters, this->state);

		this->requestServer = RequestServer(vector<string> { settings["tcpServerPort"].c_str(), settings["webSocketServerPort"].c_str() }, vector<bool> { false, true }, this->workers, IResultCode::RETRY_LATER, IProcessorNode::onRequestDispatch, IProcessorNode::onConnectDispatch, IProcessorNode::onDisconnectDispatch, this);

	if (this->areaId != 0) {
		TCPConnection brokerNode(settings["brokerAddress"].c_str(), settings["brokerPort"].c_str(), this);
		brokerNode.state = reinterpret_cast<void*>(this->areaId);
		this->brokerNode = &this->requestServer.adoptConnection(std::move(brokerNode));
		this->authenticatedClients[this->areaId].push_back(this->brokerNode);
		this->sendMessageToBroker(this->areaId, this->createMessage(0x00, 0x00));
	}
}

IProcessorNode::~IProcessorNode() {

}

void IProcessorNode::sendMessage(ObjectId receipientUserId, DataStream&& notification) {
	this->clientsLock.lock();

	auto iter = this->authenticatedClients.find(receipientUserId);
	if (iter != this->authenticatedClients.end())
		for (auto i : iter->second)
			this->requestServer.addToOutgoingQueue(RequestServer::Message(*i, std::move(notification)));

	this->clientsLock.unlock();
}

void IProcessorNode::sendMessageToBroker(ObjectId targetAreaId, DataStream&& message) {
	message.write(targetAreaId);
	this->requestServer.addToOutgoingQueue(RequestServer::Message(*this->brokerNode, std::move(message)));
}

DataStream IProcessorNode::createMessage(uint8 category, uint8 type) {
	DataStream notification;
	RequestServer::Message::writeHeader(notification, 0x00, category, type);
	return notification;
}

void IProcessorNode::onConnect(TCPConnection& client) {

}

void IProcessorNode::onDisconnect(TCPConnection& client) {
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

RequestServer::RequestResult IProcessorNode::onRequest(TCPConnection& client, word workerNumber, uint8 requestCategory, uint8 requestMethod, DataStream& parameters, DataStream& response) {
	ResultCode resultCode = IResultCode::SUCCESS;
	ObjectId authenticatedId = reinterpret_cast<ObjectId>(client.state);
	ObjectId startId = authenticatedId;
	auto& context = this->contextCreator ? this->dbConnections[workerNumber] : this->emptyDB;

	auto handler = this->handlerCreator(requestCategory, requestMethod, authenticatedId, context, resultCode, this->state);
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

	if (this->contextCreator) {
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
		this->clientsLock.lock();
		this->authenticatedClients[authenticatedId].push_back(&client);
		this->clientsLock.unlock();
		client.state = reinterpret_cast<void*>(authenticatedId);
	}

end:
	response.write(resultCode);

	if (resultCode == IResultCode::SUCCESS)
		handler->serialize(response);

	return RequestServer::RequestResult::SUCCESS;
}

RequestServer::RequestResult IProcessorNode::onRequestDispatch(TCPConnection& client, void* state, word workerNumber, uint8 requestCategory, uint8 requestMethod, DataStream& parameters, DataStream& response) {
	return reinterpret_cast<IProcessorNode*>(state)->onRequest(client, workerNumber, requestCategory, requestMethod, parameters, response);
}

void IProcessorNode::onDisconnectDispatch(TCPConnection& client, void* state) {
	reinterpret_cast<IProcessorNode*>(state)->onDisconnect(client);
}

void IProcessorNode::onConnectDispatch(TCPConnection& client, void* state) {
	reinterpret_cast<IProcessorNode*>(state)->onConnect(client);
}