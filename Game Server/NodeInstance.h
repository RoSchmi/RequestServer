#pragma once

#include <iostream>
#include <cstring>
#include <string>
#include <mutex>
#include <map>
#include <vector>
#include <algorithm>

#include <libconfig.h++>

#include <Utilities/Common.h>
#include <Utilities/SQLDatabase.h>
#include <Utilities/RequestServer.h>

#include "BaseMessages.h"
#include "DBContext.h"

#include "Common.h"

namespace GameServer {
	template<typename T> class NodeInstance {
		private:
			Utilities::RequestServer* requestServer;

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
				this->requestServer = nullptr;

				const libconfig::Setting& dbSettings = settings["Database"];
				Utilities::SQLDatabase::Connection::Parameters parameters = {dbSettings["host"].c_str(), dbSettings["port"].c_str(), dbSettings["dbname"].c_str(), dbSettings["role"].c_str(), dbSettings["password"].c_str()};
				for (uint8 i = 0; i < this->workers; i++)
					this->dbConnections[i] = contextCreator(parameters);
			}

			exported ~NodeInstance(){
				for (uint8 i = 0; i < this->workers; i++)
					delete this->dbConnections[i];

				delete[] this->dbConnections;

				if (this->requestServer)
					delete this->requestServer;
			}

			exported void run() {
				std::vector<std::string> ports;
				ports.push_back(this->tcpPort);
				ports.push_back(this->wsPort);

				std::vector<bool> flags;
				flags.push_back(false);
				flags.push_back(true);

				this->requestServer = new Utilities::RequestServer(ports, this->workers, flags, IResultCode::RETRY_LATER, onRequest, onConnect, onDisconnect, this);

				int8 input;
				do {
					std::cin >> input;
				} while (input != 'c');
			}

			exported Utilities::DataStream getNewNotification() {
				Utilities::DataStream stream;
				Utilities::RequestServer::Message::getHeader(stream, 0, 0, 0);
				return stream;
			}

			exported void sendNotification(ObjectId receipientUserId, Utilities::DataStream& stream) {
				this->clientsLock.lock();

				auto iter = this->authenticatedClients.find(receipientUserId);
				if (iter != this->authenticatedClients.end()) {
					for (auto i : iter->second) {
						auto message = new Utilities::RequestServer::Message(*i, stream);
						this->requestServer->addToOutgoingQueue(message);
					}
				}

				this->clientsLock.unlock();
			}


		private:
			std::map<ObjectId, std::vector<Utilities::RequestServer::Client*>> authenticatedClients;
			std::mutex clientsLock;

			uint32 workers;
			std::string tcpPort;
			std::string wsPort;
			HandlerCreator handlerCreator;
			T** dbConnections;

			static void* onConnect(Utilities::RequestServer::Client& client, void* state) {
				return nullptr;
			}

			static void onDisconnect(Utilities::RequestServer::Client& client, void* state) {
				NodeInstance& node = *static_cast<NodeInstance*>(state);
				ObjectId authenticatedId = reinterpret_cast<ObjectId>(client.state);
				
				node.clientsLock.lock();

				auto i = node.authenticatedClients.find(authenticatedId);
				if (i != node.authenticatedClients.end()) {
					auto& list = i->second;
					auto j = std::find(list.begin(), list.end(), &client);
					list.erase(j);

					if (list.size() == 0)
						node.authenticatedClients.erase(authenticatedId);
				}

				node.clientsLock.unlock();
			}

			static bool onRequest(uint8 workerNumber, Utilities::RequestServer::Client& client, uint8 requestCategory, uint8 requestMethod, Utilities::DataStream& parameters, Utilities::DataStream& response, uint16& resultCode, void* state) {
				NodeInstance& node = *static_cast<NodeInstance*>(state);
				ResultCode resultStatus = IResultCode::SUCCESS;
				T& context = *node.dbConnections[workerNumber];
				ObjectId authenticatedId = reinterpret_cast<ObjectId>(client.state);
				ObjectId startId = authenticatedId;

				auto handler = node.handlerCreator(requestCategory, requestMethod, authenticatedId, resultStatus);
				if (resultStatus != IResultCode::SUCCESS) {
					response.write(resultStatus);
					resultCode = resultStatus;
					return true;
				}

				try {
					handler->deserialize(parameters);
				} catch (Utilities::DataStream::ReadPastEndException&) {
					response.write(resultStatus);
					resultCode = resultStatus;
					return true;
				}
	
				context.beginTransaction();
				resultStatus = handler->process(authenticatedId, context);
				try {
					context.commitTransaction();
				} catch (const Utilities::SQLDatabase::Exception& e) {
					std::cout << e.what << std::endl;
					context.rollbackTransaction();
					resultStatus = IResultCode::SERVER_ERROR;
				}

				response.write<ResultCode>(static_cast<ResultCode>(resultStatus));
				if (resultStatus == IResultCode::SUCCESS)
					handler->serialize(response);

				delete handler;

				//It is invalid to change the authenticated id more than once per session.
				if (authenticatedId != startId) {
					node.clientsLock.lock();
					node.authenticatedClients[authenticatedId].push_back(&client);
					node.clientsLock.unlock();
					client.state = reinterpret_cast<void*>(authenticatedId);
				}

				resultCode = resultStatus;
				return true;
			}
	};
}
