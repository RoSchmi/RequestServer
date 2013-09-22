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
#include <Utilities/TCPConnection.h>
#include <Utilities/Socket.h>

#include "BaseMessages.h"
#include "DBContext.h"

#include "Common.h"

namespace GameServer {
	template<typename T> class NodeInstance {
		public:
			typedef BaseRequest<T>* (*HandlerCreator)(uint8 category, uint8 method, uint64 userId, uint16& errorCode);
			typedef T* (*ContextCreator)(Utilities::SQLDatabase::Connection::Parameters& parameters);

		private:
			Utilities::RequestServer* requestServer;
			Utilities::SQLDatabase::Connection::Parameters dbParameters;
			Utilities::Net::TCPConnection* brokerNode;
			Utilities::Net::SocketAsyncWorker brokerAsyncWorker;
			Utilities::RequestServer::Client* brokerClient;
			ObjectId areaId;
			std::map<ObjectId, std::vector<Utilities::RequestServer::Client*>> authenticatedClients;
			std::mutex clientsLock;
			uint32 workers;
			std::string brokerAddress;
			std::string brokerPort;
			std::string tcpPort;
			std::string wsPort;
			HandlerCreator handlerCreator;
			ContextCreator contextCreator;
			T** dbConnections;

		public:
			libconfig::Config config;

			exported NodeInstance(HandlerCreator handlerCreator, ContextCreator contextCreator, std::string configFileName, ObjectId areaId) : brokerAsyncWorker(NodeInstance::onBrokerReceived) {
				this->config.readFile(configFileName.c_str());
				libconfig::Setting& settings = this->config.getRoot();

				this->workers = static_cast<uint32>(settings["workerThreads"]);
				this->tcpPort = std::string(settings["tcpServerPort"].c_str());
				this->wsPort = std::string(settings["webSocketServerPort"].c_str());
				this->brokerPort = std::string(settings["brokerPort"].c_str());
				this->brokerAddress = std::string(settings["brokerAddress"].c_str());
				this->handlerCreator = handlerCreator;
				this->contextCreator = contextCreator;
				this->requestServer = nullptr;
				this->brokerClient = nullptr;
				this->areaId = areaId;

				const libconfig::Setting& dbSettings = settings["Database"];
				this->dbParameters = { dbSettings["host"].c_str(), dbSettings["port"].c_str(), dbSettings["dbname"].c_str(), dbSettings["role"].c_str(), dbSettings["password"].c_str() };

				this->dbConnections = new T*[this->workers];
				for (uint8 i = 0; i < this->workers; i++)
					this->dbConnections[i] = this->contextCreator(this->dbParameters);

				this->requestServer = new Utilities::RequestServer({ this->tcpPort, this->wsPort }, this->workers, { false, true }, IResultCode::RETRY_LATER, onRequest, onConnect, onDisconnect, this);

				if (this->areaId != 0) {
					this->brokerNode = new Utilities::Net::TCPConnection(this->brokerAddress, this->brokerPort, this);
					this->brokerAsyncWorker.registerSocket(this->brokerNode->getBaseSocket(), this->brokerNode);
					this->brokerAsyncWorker.start();
					this->brokerClient = new Utilities::RequestServer::Client(*this->brokerNode, *this->requestServer, this->brokerNode->getBaseSocket().getRemoteAddress());
					this->brokerNode->send(reinterpret_cast<uint8*>(&this->areaId), sizeof(ObjectId));
				}
			}

			exported ~NodeInstance(){
				for (uint8 i = 0; i < this->workers; i++)
					delete this->dbConnections[i];

				delete[] this->dbConnections;

				delete this->requestServer;

				if (this->brokerClient)
					delete this->brokerClient;
			}

			exported Utilities::DataStream getNewMessage(uint16 messageId, uint8 category, uint8 type) {
				Utilities::DataStream stream;
				Utilities::RequestServer::Message::getHeader(stream, messageId, category, type);
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

			exported void sendToBroker(ObjectId recepientAreaId, Utilities::DataStream& stream) {
				this->brokerNode->addPart(reinterpret_cast<uint8*>(&recepientAreaId), sizeof(ObjectId));
				this->brokerNode->addPart(stream.getBuffer(), stream.getLength());
				this->brokerNode->sendParts();
			}

		private:
			static void onBrokerReceived(const Utilities::Net::Socket& socket, void* state) {
				auto& connection = *static_cast<Utilities::Net::TCPConnection*>(state);
				NodeInstance& node = *static_cast<NodeInstance*>(connection.getState());
				auto messages = connection.read(0);

				if (messages.getCount() == 0)
					return;

				for (auto& i : messages) {
					Utilities::DataStream stream(i.data, i.length);
					node.requestServer->addToIncomingQueue(new Utilities::RequestServer::Message(*node.brokerClient, stream));
				}
			}

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

			static bool onRequest(uint8 workerNumber, Utilities::RequestServer::Client& client, uint8 requestCategory, uint8 requestMethod, Utilities::DataStream& parameters, Utilities::DataStream& response, void* state) {
				NodeInstance& node = *static_cast<NodeInstance*>(state);
				ResultCode resultCode = IResultCode::SUCCESS;
				T& context = *node.dbConnections[workerNumber];
				ObjectId authenticatedId = reinterpret_cast<ObjectId>(client.state);
				ObjectId startId = authenticatedId;

				auto handler = node.handlerCreator(requestCategory, requestMethod, authenticatedId, resultCode);
				if (!handler)
					goto end;

				try {
					handler->deserialize(parameters);
				}
				catch (Utilities::DataStream::ReadPastEndException&) {
					resultCode = IResultCode::SERVER_ERROR;
					goto end;
				}
	
				context.beginTransaction();
				resultCode = handler->process(authenticatedId, context);
				try {
					context.commitTransaction();
				} catch (const Utilities::SQLDatabase::Exception&) {
					context.rollbackTransaction();
					resultCode = IResultCode::SERVER_ERROR;
				}

			end:
				response.write(resultCode);
				if (resultCode == IResultCode::SUCCESS)
					handler->serialize(response);

				//It is invalid to change the authenticated id more than once per session.
				if (authenticatedId != startId) {
					node.clientsLock.lock();
					node.authenticatedClients[authenticatedId].push_back(&client);
					node.clientsLock.unlock();
					client.state = reinterpret_cast<void*>(authenticatedId);
				}

				delete handler;

				return true;
			}
	};
}
