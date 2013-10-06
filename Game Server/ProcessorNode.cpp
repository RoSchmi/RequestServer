#include "ProcessorNode.h"

#include <algorithm>

using namespace std;
using namespace util;
using namespace util::net;
using namespace game_server;

processor_node::processor_node(handler_creator hndlr_creator, context_creator ctx_creator, libconfig::Setting& settings, obj_id area_id, void* state) {
	this->workers = settings["workerThreads"];
	this->hndlr_creator = hndlr_creator;
	this->ctx_creator = ctx_creator;
	this->area_id = area_id;
	this->state = state;

	auto& dbSettings = settings["Database"];
	this->db_parameters = { dbSettings["host"].c_str(), dbSettings["port"].c_str(), dbSettings["dbname"].c_str(), dbSettings["role"].c_str(), dbSettings["password"].c_str() };

	if (this->ctx_creator)
		for (word i = 0; i < this->workers; i++)
			this->dbs[i] = this->ctx_creator(i, this->db_parameters, this->state);

		this->server = request_server(vector<string> { settings["tcpServerPort"].c_str(), settings["webSocketServerPort"].c_str() }, vector<bool> { false, true }, this->workers, result_codes::RETRY_LATER, processor_node::on_request_d, processor_node::on_connect_d, processor_node::on_disconnect_d, this);

	if (this->area_id != 0) {
		tcp_connection broker(settings["brokerAddress"].c_str(), settings["brokerPort"].c_str(), this);
		broker.state = reinterpret_cast<void*>(this->area_id);
		this->broker = &this->server.adopt(move(broker));
		this->authenticated_clients[this->area_id].push_back(this->broker);
		this->send_to_broker(this->area_id, this->create_message(0x00, 0x00));
	}
}

processor_node::~processor_node() {

}

void processor_node::send(obj_id receipient_id, data_stream&& notification) {
	this->clients_lock.lock();

	auto iter = this->authenticated_clients.find(receipient_id);
	if (iter != this->authenticated_clients.end())
		for (auto i : iter->second)
			this->server.enqueue_outgoing(request_server::message(*i, move(notification)));

	this->clients_lock.unlock();
}

void processor_node::send_to_broker(obj_id target_id, data_stream&& message) {
	message.write(target_id);
	this->server.enqueue_outgoing(request_server::message(*this->broker, move(message)));
}

data_stream processor_node::create_message(uint8 category, uint8 type) {
	data_stream notification;
	request_server::message::write_header(notification, 0x00, category, type);
	return notification;
}

void processor_node::on_connect(tcp_connection& client) {

}

void processor_node::on_disconnect(tcp_connection& client) {
	obj_id authenticated_id = reinterpret_cast<obj_id>(client.state);

	this->clients_lock.lock();

	auto i = this->authenticated_clients.find(authenticated_id);
	if (i != this->authenticated_clients.end()) {
		auto& list = i->second;
		auto j = find(list.begin(), list.end(), &client);

		list.erase(j);

		if (list.size() == 0)
			this->authenticated_clients.erase(authenticated_id);
	}

	this->clients_lock.unlock();
}

request_server::request_result processor_node::on_request(tcp_connection& client, word worker_num, uint8 category, uint8 method, data_stream& parameters, data_stream& response) {
	result_code resultCode = result_codes::SUCCESS;
	obj_id authenticated_id = reinterpret_cast<obj_id>(client.state);
	obj_id start_id = authenticated_id;
	auto& context = this->ctx_creator ? this->dbs[worker_num] : this->empty_db;

	auto handler = this->hndlr_creator(worker_num, category, method, authenticated_id, context, resultCode, this->state);
	if (!handler) {
		resultCode = result_codes::INVALID_REQUEST_TYPE;
		goto end;
	}

	try {
		handler->deserialize(parameters);
	}
	catch (data_stream::read_past_end_exception&) {
		resultCode = result_codes::INVALID_PARAMETERS;
		goto end;
	}

	if (this->ctx_creator) {
		context->begin_transaction();
		resultCode = handler->process();
		try {
			context->commit_transaction();
		}
		catch (const sql::db_exception&) {
			context->rollback_transaction();
			return request_server::request_result::RETRY_LATER;
		}
	}
	else {
		resultCode = handler->process();
	}

	if (resultCode == result_codes::NO_RESPONSE)
		return request_server::request_result::NO_RESPONSE;

	if (authenticated_id != start_id) {
		this->clients_lock.lock();
		this->authenticated_clients[authenticated_id].push_back(&client);
		this->clients_lock.unlock();
		client.state = reinterpret_cast<void*>(authenticated_id);
	}

end:
	response.write(resultCode);

	if (resultCode == result_codes::SUCCESS)
		handler->serialize(response);

	return request_server::request_result::SUCCESS;
}

request_server::request_result processor_node::on_request_d(tcp_connection& client, void* state, word worker_num, uint8 category, uint8 method, data_stream& parameters, data_stream& response) {
	return reinterpret_cast<processor_node*>(state)->on_request(client, worker_num, category, method, parameters, response);
}

void processor_node::on_disconnect_d(tcp_connection& client, void* state) {
	reinterpret_cast<processor_node*>(state)->on_disconnect(client);
}

void processor_node::on_connect_d(tcp_connection& client, void* state) {
	reinterpret_cast<processor_node*>(state)->on_connect(client);
}