#include "BrokerNode.h"

#include <ArkeIndustries.CPPUtilities/Common.h>

#include "Common.h"

using namespace std;
using namespace util;
using namespace util::net;
using namespace game_server;

broker_node::broker_node(word workers, endpoint ep) : processor_node(workers, ep) {

}

void broker_node::on_disconnect(shared_ptr<tcp_connection> client) {
	auto id = reinterpret_cast<obj_id>(client->state);
	if (this->authenticated_clients.count(id) != 0)
		this->authenticated_clients.erase(id);
	processor_node::on_disconnect(client);
}

request_server::request_result broker_node::on_request(shared_ptr<tcp_connection> client, word worker_number, uint8 category, uint8 method, data_stream& parameters, data_stream& response) {
	parameters.seek(parameters.size() - sizeof(obj_id));
	obj_id client_area_id = parameters.read<obj_id>();

	if (category == 0x00 && method == 0x00) {
		client->state = reinterpret_cast<void*>(client_area_id);
		this->authenticated_clients[client_area_id].push_back(client);
	}
	else {
		parameters.shrink_written(parameters.size() - sizeof(obj_id));
		this->send(client_area_id, move(parameters));
	}

	return request_server::request_result::no_response;
}