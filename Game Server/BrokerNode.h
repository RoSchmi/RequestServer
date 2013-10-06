#pragma once

#include <Utilities/DataStream.h>
#include <Utilities/Net/TCPConnection.h>

#include "ProcessorNode.h"

namespace game_server {
	class broker_node : public processor_node {
		virtual void on_disconnect(util::net::tcp_connection& client) override;
		virtual util::net::request_server::request_result on_request(util::net::tcp_connection& client, word worker_number, uint8 category, uint8 method, util::data_stream& parameters, util::data_stream& response) override;

	public:
		exported broker_node(libconfig::Setting& settings);
		exported virtual ~broker_node();
	};
}
