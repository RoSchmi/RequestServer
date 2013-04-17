#pragma once

#include <Utilities/Common.h>
#include <Utilities/SQLDatabase.h>
#include <Utilities/RequestServer.h>
#include <libconfig.h++>
#include <iostream>
#include <cstring>
#include <string>

#include "BaseRequest.h"
#include "DBContext.h"

namespace GameServer {
	template<typename T> class NodeInstance {
		public:
			libconfig::Config config;

			typedef BaseRequest<T>* (*HandlerCreator)(uint8 category, uint8 method, uint64 userId, uint16& errorCode);
			typedef T* (*ContextCreator)(Utilities::SQLDatabase::Connection::Parameters& parameters);

			exported NodeInstance(HandlerCreator handlerCreator, ContextCreator contextCreator, std::string configFileName) {
				this->config.readFile(configFileName.c_str());
				libconfig::Setting& settings = this->config.getRoot();

				this->workers = static_cast<uint32>(settings["workerThreads"]);
				this->tcpPort = string(settings["tcpServerPort"].c_str());
				this->wsPort = string(settings["webSocketServerPort"].c_str());
				this->handlerCreator = handlerCreator;
				this->dbConnections = new T*[this->workers];

				const libconfig::Setting& dbSettings = settings["Database"];
				Utilities::SQLDatabase::Connection::Parameters parameters = {dbSettings["host"].c_str(), dbSettings["port"].c_str(), dbSettings["dbname"].c_str(), dbSettings["role"].c_str(), dbSettings["password"].c_str()};
				for (uint8 i = 0; i < this->workers; i++)
					this->dbConnections[i] = contextCreator(parameters);
			}

			exported ~NodeInstance(){
				for (uint8 i = 0; i < this->workers; i++)
					delete this->dbConnections[i];

				delete[] this->dbConnections;
			}

			exported void run() {
				std::vector<string> ports;
				ports.push_back(this->tcpPort);
				ports.push_back(this->wsPort);

				std::vector<bool> flags;
				flags.push_back(false);
				flags.push_back(true);

				Utilities::RequestServer requestServer(ports, this->workers, flags, IResultCode::RETRY_LATER, onRequest, this);

				int8 input;
				do {
					cin >> input;
				} while (input != 'c');
			}


		private:
			uint32 workers;
			std::string tcpPort;
			std::string wsPort;
			HandlerCreator handlerCreator;
			T** dbConnections;

			static bool onRequest(uint8 workerNumber, Utilities::RequestServer::Client& client, uint8 requestCategory, uint8 requestMethod, Utilities::DataStream& parameters, Utilities::DataStream& response, void* state) {
				NodeInstance& node = *static_cast<NodeInstance*>(state);
				uint16 resultCode = IResultCode::SUCCESS;
				T& context = *node.dbConnections[workerNumber];

				auto handler = node.handlerCreator(requestCategory, requestMethod, client.authenticatedId, resultCode);
				if (resultCode != IResultCode::SUCCESS) {
					response.write(resultCode);
					return true;
				}

				try {
					handler->deserialize(parameters);
				} catch (Utilities::DataStream::ReadPastEndException&) {
					response.write(resultCode);
					return true;
				}
	
				context.beginTransaction();
				resultCode = handler->process(client.authenticatedId, client.ipAddress, context);
				try {
					context.commitTransaction();
				} catch (const Utilities::SQLDatabase::Exception& e) {
					std::cout << e.what << std::endl;
					context.rollbackTransaction();
					resultCode = IResultCode::SERVER_ERROR;
				}

				response.write<uint16>(static_cast<uint16>(resultCode));
				if (resultCode == IResultCode::SUCCESS)
					handler->serialize(response);

				delete handler;

				return true;
			}
	};
}
