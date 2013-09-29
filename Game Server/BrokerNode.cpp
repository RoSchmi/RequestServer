#include "BrokerNode.h"

#include <Utilities/Common.h>
#include <Utilities/TCPServer.h>

#include "Common.h"

using namespace std;
using namespace Utilities;
using namespace Utilities::Net;
using namespace GameServer;

BrokerNode::BrokerNode(libconfig::Setting& settings) : IProcessorNode(nullptr, nullptr, settings, 0) {

}

BrokerNode::~BrokerNode() {

}

void BrokerNode::onDisconnect(TCPConnection& client) {
	this->authenticatedClients.erase(reinterpret_cast<ObjectId>(client.state));
}

RequestServer::RequestResult BrokerNode::onRequest(TCPConnection& client, word workerNumber, uint8 requestCategory, uint8 requestMethod, DataStream& parameters, DataStream& response) {
	parameters.seek(parameters.getLength() - sizeof(ObjectId));
	ObjectId clientAreaId = parameters.read<ObjectId>();

	if (requestCategory == 0x00 && requestMethod == 0x00) {
		client.state = reinterpret_cast<void*>(clientAreaId);
		this->authenticatedClients[clientAreaId].push_back(&client);
	}
	else {
		this->sendMessage(clientAreaId, std::move(parameters));
	}

	return RequestServer::RequestResult::NO_RESPONSE;
}