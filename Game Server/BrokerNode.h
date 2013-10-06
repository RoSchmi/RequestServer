#pragma once

#include <Utilities/DataStream.h>
#include <Utilities/Net/TCPConnection.h>

#include "IProcessorNode.h"

namespace GameServer {
	class BrokerNode : public IProcessorNode {
		virtual void onDisconnect(util::net::tcp_connection& client) override;
		virtual util::net::request_server::request_result onRequest(util::net::tcp_connection& client, word workerNumber, uint8 requestCategory, uint8 requestMethod, util::data_stream& parameters, util::data_stream& response) override;

	public:
		exported BrokerNode(libconfig::Setting& settings);
		exported virtual ~BrokerNode();
	};
}
