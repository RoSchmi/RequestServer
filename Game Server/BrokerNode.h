#pragma once

#include <Utilities/DataStream.h>
#include <Utilities/Net/TCPConnection.h>

#include "ProcessorNode.h"

namespace game_server {
	class broker_node : public processor_node {
		virtual void on_disconnect(util::net::tcp_connection& client) override;
		virtual util::net::request_server::request_result on_request(util::net::tcp_connection& client, word worker_number, uint8 category, uint8 method, util::data_stream& parameters, util::data_stream& response) override;
	
		public:
			broker_node(const broker_node& other) = delete;
			broker_node(broker_node&& other) = delete;
			broker_node& operator=(broker_node&& other) = delete;
			broker_node& operator=(const broker_node& other) = delete;
	
			exported broker_node(word workers, util::net::endpoint ep);
			exported virtual ~broker_node() = default;
	};
}
