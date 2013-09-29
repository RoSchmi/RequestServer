#pragma once

#include <string>
#include <mutex>
#include <map>
#include <memory>
#include <vector>

#include <libconfig.h++>

#include <Utilities/Common.h>
#include <Utilities/SQLDatabase.h>
#include <Utilities/RequestServer.h>
#include <Utilities/TCPConnection.h>

#include "IDBContext.h"
#include "Common.h"

namespace GameServer {
	class BaseHandler {
		public:
			exported virtual ResultCode process() = 0;
			exported virtual void deserialize(Utilities::DataStream& parameters) = 0;
			exported virtual void serialize(Utilities::DataStream& response) = 0;
	};

	class ProcessorNode {
		public:
			typedef std::unique_ptr<BaseHandler> (*HandlerCreator)(uint8 category, uint8 method, ObjectId& userId, std::unique_ptr<IDBContext>& db, uint16& errorCode);
			typedef std::unique_ptr<IDBContext>(*ContextCreator)(Utilities::SQLDatabase::Connection::Parameters& parameters);

			libconfig::Config config;

			exported ProcessorNode(HandlerCreator handlerCreator, ContextCreator contextCreator, std::string configFileName, ObjectId areaId);
			exported ~ProcessorNode();

			exported void sendNotification(ObjectId receipientUserId, Utilities::DataStream&& notification);
			exported void sendMessageToBroker(Utilities::DataStream&& message);
			exported Utilities::DataStream createNotification(uint8 category, uint8 type);
			exported Utilities::DataStream ProcessorNode::createBrokerMessage(ObjectId targetAreaId, uint8 category, uint8 type);

		private:
			Utilities::SQLDatabase::Connection::Parameters dbParameters;
			Utilities::Net::RequestServer requestServer;
			ObjectId areaId;
			Utilities::Net::TCPConnection* brokerNode;
			std::map<ObjectId, std::vector<Utilities::Net::TCPConnection*>> authenticatedClients;
			std::mutex clientsLock;
			word workers;
			HandlerCreator handlerCreator;
			ContextCreator contextCreator;
			std::vector<std::unique_ptr<IDBContext>> dbConnections;

			static void onConnect(Utilities::Net::TCPConnection& client, void* state);
			static void onDisconnect(Utilities::Net::TCPConnection& client, void* state);
			static Utilities::Net::RequestServer::RequestResult onRequest(Utilities::Net::TCPConnection& client, void* state, word workerNumber, uint8 requestCategory, uint8 requestMethod, Utilities::DataStream& parameters, Utilities::DataStream& response);
	};
}
