#pragma once

#include <string>
#include <mutex>
#include <map>
#include <memory>
#include <vector>
#include <functional>
#include <algorithm>

#include <libconfig.h++>

#include <Utilities/Common.h>
#include <Utilities/Optional.h>
#include <Utilities/SQL/Database.h>
#include <Utilities/Net/RequestServer.h>
#include <Utilities/Net/TCPConnection.h>

#include "Common.h"

namespace game_server {
	class processor_node {
		public:
			class base_handler {
				public:
					exported virtual ~base_handler() = 0;
					exported virtual result_code process(obj_id& user_id);
					exported virtual void deserialize(util::data_stream& parameters) = 0;
					exported virtual void serialize(util::data_stream& response) = 0;
			};

			processor_node(const processor_node& other) = delete;
			processor_node(processor_node&& other) = delete;
			processor_node& operator=(processor_node&& other) = delete;
			processor_node& operator=(const processor_node& other) = delete;

		protected:
			std::map<uint16, std::unique_ptr<base_handler>> authenticated_handlers;
			std::map<uint16, std::unique_ptr<base_handler>> unauthenticated_handlers;
			util::net::request_server server;
			util::optional<util::net::tcp_connection&> broker;
			std::map<obj_id, std::vector<std::reference_wrapper<util::net::tcp_connection>>> authenticated_clients;
			std::mutex clients_lock;
			obj_id area_id;
			word workers;

			void add_client(obj_id id, util::net::tcp_connection& conn);
			void del_client(obj_id id, util::net::tcp_connection& conn);

		public:
			exported processor_node(libconfig::Setting& settings, obj_id area_id = 0);
			exported virtual ~processor_node() = default;

			exported void start();
			exported void send(obj_id receipient_id, util::data_stream notification);
			exported void send_to_broker(obj_id target_id, util::data_stream message);
			exported util::data_stream create_message(uint8 category, uint8 type);
			exported void register_handler(std::unique_ptr<base_handler> handler, uint8 category, uint8 method, bool authenticated = true);
			exported virtual void on_connect(util::net::tcp_connection& client);
			exported virtual void on_disconnect(util::net::tcp_connection& client);
			exported virtual util::net::request_server::request_result on_request(util::net::tcp_connection& client, word worker_num, uint8 category, uint8 method, util::data_stream& parameters, util::data_stream& response);
	};

	template<typename T> class processor_node_db : public processor_node {
		public:
			class base_handler : public processor_node::base_handler {
				public:
					exported virtual ~base_handler() = 0;
					exported virtual result_code process(obj_id& user_id, T& db);
			};

			typedef std::function<T(word, util::sql::connection::parameters&)> context_creator;

			processor_node_db(const processor_node_db& other) = delete;
			processor_node_db(processor_node_db&& other) = delete;
			processor_node_db& operator=(processor_node_db&& other) = delete;
			processor_node_db& operator=(const processor_node_db& other) = delete;

		protected:
			std::vector<T> dbs;

		public:
			exported processor_node_db(libconfig::Setting& settings, context_creator ctx_creator, obj_id area_id = 0) : processor_node(settings, area_id) {
				util::sql::connection::parameters paras = { settings["db"]["host"].c_str(), settings["db"]["port"].c_str(), settings["db"]["dbname"].c_str(), settings["db"]["role"].c_str(), settings["db"]["password"].c_str() };

				for (word i = 0; i < this->workers; i++)
					this->dbs.emplace_back(std::move(ctx_creator(i, paras)));
			}

			exported virtual ~processor_node_db() = default;

			exported virtual util::net::request_server::request_result on_request(util::net::tcp_connection& client, word worker_num, uint8 category, uint8 method, util::data_stream& parameters, util::data_stream& response) override {
				result_code result = result_codes::success;
				obj_id authenticated_id = reinterpret_cast<obj_id>(client.state);
				obj_id start_id = authenticated_id;
				uint16 type = (category << 8) | method;
				T& context = this->dbs[worker_num];

				if ((authenticated_id != 0 && this->authenticated_handlers.count(type) == 0) || (authenticated_id == 0 && this->unauthenticated_handlers.count(type) == 0)) {
					response.write(result_codes::invalid_request_type);
					return util::net::request_server::request_result::success;
				}

				auto& handler = *reinterpret_cast<base_handler*>(authenticated_id != 0 ? this->authenticated_handlers[type].get() : this->unauthenticated_handlers[type].get());

				try {
					handler.deserialize(parameters);
				}
				catch (util::data_stream::read_past_end_exception&) {
					response.write(result_codes::invalid_parameters);
					return util::net::request_server::request_result::success;
				}

				context.begin_transaction();
				result = handler.process(authenticated_id, context);
				if (!context.committed()) {
					try {
						context.commit_transaction();
					}
					catch (const util::sql::db_exception&) {
						context.rollback_transaction();
						return util::net::request_server::request_result::retry_later;
					}
				}

				response.write(result);

				if (result == result_codes::success)
					handler.serialize(response);
				else if (result == result_codes::no_response)
					return util::net::request_server::request_result::no_response;

				if (authenticated_id != start_id) {
					if (authenticated_id != 0) {
						this->add_client(authenticated_id, client);
					}
					else {
						this->del_client(start_id, client);
					}
				}

				return util::net::request_server::request_result::success;
			}
	};
}
