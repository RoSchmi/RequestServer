#include "BrokerNode.h"

#include <Utilities/Common.h>

#include "Common.h"

using namespace std;
using namespace util;
using namespace util::net;
using namespace game_server;

broker_node::broker_node(libconfig::Setting& settings) : processor_node(nullptr, nullptr, settings, 0) {

}

void broker_node::on_disconnect(tcp_connection& client) {
	this->authenticated_clients.erase(reinterpret_cast<obj_id>(client.state));
}

request_server::request_result broker_node::on_request(tcp_connection& client, word worker_number, uint8 category, uint8 method, data_stream& parameters, data_stream& response) {
	parameters.seek(parameters.size() - sizeof(obj_id));
	obj_id client_area_id = parameters.read<obj_id>();

	if (category == 0x00 && method == 0x00) {
		client.state = reinterpret_cast<void*>(client_area_id);
		this->authenticated_clients[client_area_id].push_back(&client);
	}
	else {
		this->send(client_area_id, move(parameters));
	}

	return request_server::request_result::NO_RESPONSE;
}