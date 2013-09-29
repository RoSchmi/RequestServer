#pragma once

#include <Utilities/DataStream.h>
#include <Utilities/TCPConnection.h>

#include "IProcessorNode.h"

namespace GameServer {
	class BrokerNode : public IProcessorNode {
		virtual void onDisconnect(Utilities::Net::TCPConnection& client) override;
		virtual Utilities::Net::RequestServer::RequestResult onRequest(Utilities::Net::TCPConnection& client, word workerNumber, uint8 requestCategory, uint8 requestMethod, Utilities::DataStream& parameters, Utilities::DataStream& response) override;

	public:
		exported BrokerNode(std::string configFileName);
		exported virtual ~BrokerNode();
	};
}
