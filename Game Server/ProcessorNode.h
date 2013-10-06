#pragma once

#include <string>
#include <mutex>
#include <map>
#include <memory>
#include <vector>

#include <libconfig.h++>

#include <Utilities/Common.h>
#include <Utilities/SQL/Database.h>
#include <Utilities/Net/RequestServer.h>
#include <Utilities/Net/TCPConnection.h>

#include "Common.h"
#include "DBContext.h"

namespace game_server {
	class processor_node {
		public:
			typedef std::shared_ptr<base_handler>(*handler_creator)(word worker_num, uint8 category, uint8 method, obj_id& userId, std::shared_ptr<db_context>& db, uint16& error_code, void* state);
			typedef std::shared_ptr<db_context>(*context_creator)(word worker_num, util::sql::connection::parameters& parameters, void* state);

			exported processor_node(handler_creator hndlr_creator, context_creator ctx_creator, libconfig::Setting& settings, obj_id area_id, void* state = nullptr);
			exported virtual ~processor_node() = default;

			exported void send(obj_id receipient_id, util::data_stream&& notification);
			exported void send_to_broker(obj_id target_id, util::data_stream&& message);
			exported util::data_stream create_message(uint8 category, uint8 type);

			processor_node(const processor_node& other) = delete;
			processor_node(processor_node&& other) = delete;
			processor_node& operator=(processor_node&& other) = delete;
			processor_node& operator=(const processor_node& other) = delete;

		protected:
			util::sql::connection::parameters db_parameters;
			util::net::request_server server;
			obj_id area_id;
			util::net::tcp_connection* broker;
			std::map<obj_id, std::vector<util::net::tcp_connection*>> authenticated_clients;
			std::mutex clients_lock;
			word workers;
			void* state;
			handler_creator hndlr_creator;
			context_creator ctx_creator;
			std::shared_ptr<db_context> empty_db;
			std::vector<std::shared_ptr<db_context>> dbs;

			virtual void on_connect(util::net::tcp_connection& client);
			virtual void on_disconnect(util::net::tcp_connection& client);
			virtual util::net::request_server::request_result on_request(util::net::tcp_connection& client, word worker_num, uint8 category, uint8 method, util::data_stream& parameters, util::data_stream& response);

			static void on_connect_d(util::net::tcp_connection& client, void* state);
			static void on_disconnect_d(util::net::tcp_connection& client, void* state);
			static util::net::request_server::request_result on_request_d(util::net::tcp_connection& client, void* state, word worker_num, uint8 category, uint8 method, util::data_stream& parameters, util::data_stream& response);
	};
}
