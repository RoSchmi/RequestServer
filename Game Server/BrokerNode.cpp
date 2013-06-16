#include "BrokerNode.h"

#include <map>
#include <iostream>

#include <Utilities/Common.h>
#include <Utilities/TCPServer.h>

#include "Common.h"

using namespace std;
using namespace Utilities;
using namespace Utilities::Net;
using namespace GameServer;

BrokerNode::BrokerNode(cstr port) : server(port, false, this, BrokerNode::onClientConnect, BrokerNode::onRequestReceived) {

}

BrokerNode::~BrokerNode() {

}

void BrokerNode::run() {
	int8 input;
	do {
		cin >> input;
	} while (input != 'c');
}

void* BrokerNode::onClientConnect(TCPConnection& connection, void* serverState, const uint8 clientAddress[Net::Socket::ADDRESS_LENGTH]) {
	return serverState;
}

void BrokerNode::onRequestReceived(TCPConnection& connection, void* state, TCPConnection::Message& message) {
	BrokerNode& node = *reinterpret_cast<BrokerNode*>(state);

	if (message.length < sizeof(ObjectId))
		return;
		
	ObjectId targetId = reinterpret_cast<ObjectId*>(message.data)[0];

	if (message.length == sizeof(ObjectId))
		node.servers[targetId] = &connection;
	else
		node.servers[targetId]->send(message.data + sizeof(ObjectId), message.length - sizeof(ObjectId));
}
