#include "BrokerNode.h"

#include <Utilities/Common.h>

#include "Common.h"

using namespace std;
using namespace util;
using namespace util::net;
using namespace GameServer;

BrokerNode::BrokerNode(libconfig::Setting& settings) : IProcessorNode(nullptr, nullptr, settings, 0) {

}

BrokerNode::~BrokerNode() {

}

void BrokerNode::onDisconnect(tcp_connection& client) {
	this->authenticatedClients.erase(reinterpret_cast<ObjectId>(client.state));
}

request_server::request_result BrokerNode::onRequest(tcp_connection& client, word workerNumber, uint8 requestCategory, uint8 requestMethod, data_stream& parameters, data_stream& response) {
	parameters.seek(parameters.size() - sizeof(ObjectId));
	ObjectId clientAreaId = parameters.read<ObjectId>();

	if (requestCategory == 0x00 && requestMethod == 0x00) {
		client.state = reinterpret_cast<void*>(clientAreaId);
		this->authenticatedClients[clientAreaId].push_back(&client);
	}
	else {
		this->sendMessage(clientAreaId, std::move(parameters));
	}

	return request_server::request_result::NO_RESPONSE;
}