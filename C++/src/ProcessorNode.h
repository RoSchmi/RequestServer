#pragma once

#include <string>
#include <mutex>
#include <unordered_map>
#include <memory>
#include <vector>
#include <functional>
#include <algorithm>
#include <type_traits>

#include <ArkeIndustries.CPPUtilities/Common.h>
#include <ArkeIndustries.CPPUtilities/Optional.h>
#include <ArkeIndustries.CPPUtilities/SQL/Database.h>
#include <ArkeIndustries.CPPUtilities/Net/RequestServer.h>
#include <ArkeIndustries.CPPUtilities/Net/TCPConnection.h>

#include "Common.h"

namespace game_server {
	class processor_node {
		public:
			class base_handler {
				public:
					virtual ~base_handler() = default;
					virtual result_code process(obj_id& user_id) = 0;
					virtual void deserialize(util::data_stream& parameters) = 0;
					virtual void serialize(util::data_stream& response) = 0;
			};

			class broker_node_down_exception {};

			processor_node(const processor_node& other) = delete;
			processor_node(processor_node&& other) = delete;
			processor_node& operator=(processor_node&& other) = delete;
			processor_node& operator=(const processor_node& other) = delete;

		protected:
			std::unordered_map<uint16, std::vector<std::unique_ptr<base_handler>>> authenticated_handlers;
			std::unordered_map<uint16, std::vector<std::unique_ptr<base_handler>>> unauthenticated_handlers;
			util::net::request_server server;
			util::net::endpoint broker_ep;
			std::shared_ptr<util::net::tcp_connection> broker;
			std::unordered_map<obj_id, std::vector<std::shared_ptr<util::net::tcp_connection>>> authenticated_clients;
			std::mutex clients_lock;
			obj_id area_id;
			word workers;

			void add_client(obj_id id, std::shared_ptr<util::net::tcp_connection> conn);
			void del_client(obj_id id, std::shared_ptr<util::net::tcp_connection> conn);

			virtual void on_connect(std::shared_ptr<util::net::tcp_connection> client);
			virtual void on_disconnect(std::shared_ptr<util::net::tcp_connection> client);
			virtual util::net::request_server::request_result on_request(std::shared_ptr<util::net::tcp_connection> client, word worker_num, uint8 category, uint8 method, util::data_stream& parameters, util::data_stream& response);

		public:
			processor_node(word workers, util::net::endpoint ep, util::net::endpoint broker_ep = util::net::endpoint(), obj_id area_id = 0);
			processor_node(word workers, std::vector<util::net::endpoint> eps, util::net::endpoint broker_ep = util::net::endpoint(), obj_id area_id = 0);
			virtual ~processor_node();

			void start();
			void send(obj_id receipient_id, util::data_stream notification);
			void send_to_broker(obj_id target_id, util::data_stream message);
			util::data_stream create_message(uint8 category, uint8 type);

			template<typename T> void register_handler(uint8 category, uint8 method, bool authenticated) {
				static_assert(std::is_base_of<base_handler, T>::value, "typename T must derive from base_handler.");

				for (word i = 0; i < this->workers; i++)
					(authenticated ? this->authenticated_handlers : this->unauthenticated_handlers)[(category << 8) | method].emplace_back(new T());
			}
	};

	template<typename T> class processor_node_db : public processor_node {
		public:
			class base_handler : public processor_node::base_handler {
				virtual result_code process(obj_id& user_id) override { return result_codes::success; };

				public:
					virtual ~base_handler() = default;
					virtual result_code process(obj_id& user_id, T& db) = 0;
			};

			typedef std::function<std::unique_ptr<T>(word)> context_creator;

			processor_node_db(const processor_node_db& other) = delete;
			processor_node_db(processor_node_db&& other) = delete;
			processor_node_db& operator=(processor_node_db&& other) = delete;
			processor_node_db& operator=(const processor_node_db& other) = delete;

		protected:
			std::vector<std::unique_ptr<T>> dbs;

		public:
			processor_node_db(context_creator ctx_creator, word workers, std::vector<util::net::endpoint> eps, util::net::endpoint broker_ep = util::net::endpoint(), obj_id area_id = 0) : processor_node(workers, eps, broker_ep, area_id) {
				for (word i = 0; i < this->workers; i++)
					this->dbs.emplace_back(std::move(ctx_creator(i)));
			}

			virtual ~processor_node_db() = default;

			virtual util::net::request_server::request_result on_request(std::shared_ptr<util::net::tcp_connection> client, word worker_num, uint8 category, uint8 method, util::data_stream& parameters, util::data_stream& response) override {
				result_code result = result_codes::success;
				obj_id authenticated_id = reinterpret_cast<obj_id>(client->state);
				obj_id start_id = authenticated_id;
				uint16 type = (category << 8) | method;
				T& context = *this->dbs[worker_num].get();

				if ((authenticated_id != 0 && this->authenticated_handlers.count(type) == 0) || (authenticated_id == 0 && this->unauthenticated_handlers.count(type) == 0)) {
					response.write(result_codes::invalid_request_type);
					return util::net::request_server::request_result::success;
				}

				auto& handler = *reinterpret_cast<base_handler*>(authenticated_id != 0 ? this->authenticated_handlers[type][worker_num].get() : this->unauthenticated_handlers[type][worker_num].get());

				try {
					handler.deserialize(parameters);
				}
				catch (util::data_stream::read_past_end_exception&) {
					response.write(result_codes::invalid_parameters);
					return util::net::request_server::request_result::success;
				}

				context.begin_transaction(util::sql::connection::isolation_level::repeatable_read);
				try {
					result = handler.process(authenticated_id, context);
				}
				catch (const util::sql::synchronization_exception&) {
					return util::net::request_server::request_result::retry_later;
				}
				if (!context.committed()) {
					try {
						context.commit_transaction();
					}
					catch (const util::sql::synchronization_exception&) {
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
