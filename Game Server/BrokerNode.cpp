#include "BrokerNode.h"

#include <Utilities/Common.h>
#include <Utilities/TCPServer.h>

#include "Common.h"

using namespace std;
using namespace Utilities;
using namespace Utilities::Net;
using namespace GameServer;

BrokerNode::BrokerNode(cstr port) : server(port, BrokerNode::onClientConnect, this) {
	this->running = true;
	this->ioWorker = thread(&BrokerNode::ioWorkerRun, this);
}

BrokerNode::~BrokerNode() {
	this->running = false;
	this->ioWorker.join();
}

void BrokerNode::ioWorkerRun() {
	while (this->running) {
		this->listLock.lock();

		for (auto& i : this->clients) {
			if (i.isDataAvailable()) {
				for (auto& k : i.read()) {
					if (!k.wasClosed) {
						this->onMessage(i, k);
					}
					else {
						if (i.state)
							this->authenticated.erase(reinterpret_cast<ObjectId>(i.state));

					}
				}
			}
		}

		this->listLock.unlock();

		this_thread::sleep_for(chrono::microseconds(500));
	}
}

void BrokerNode::onClientConnect(Utilities::Net::TCPConnection&& client, void* state) {
	BrokerNode& self = *reinterpret_cast<BrokerNode*>(state);
	client.state = nullptr;
	self.listLock.lock();
	self.clients.push_back(std::move(client));
	self.listLock.unlock();
}

void BrokerNode::onMessage(TCPConnection& connection, TCPConnection::Message& message) {
	if (message.length < sizeof(ObjectId))
		return;
		
	ObjectId targetId = reinterpret_cast<ObjectId*>(message.data)[0];
	if (message.length == sizeof(ObjectId))
		this->authenticated[targetId] = &connection;
	else
		this->authenticated[targetId]->send(message.data + sizeof(ObjectId), message.length - sizeof(ObjectId));
}
