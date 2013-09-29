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
	class IProcessorNode {
		public:
			typedef std::shared_ptr<BaseHandler>(*HandlerCreator)(word workerNumber, uint8 category, uint8 method, ObjectId& userId, std::shared_ptr<IDBContext>& db, uint16& errorCode, void* state);
			typedef std::shared_ptr<IDBContext>(*ContextCreator)(word workerNumber, Utilities::SQLDatabase::Connection::Parameters& parameters, void* state);

			exported IProcessorNode(HandlerCreator handlerCreator, ContextCreator contextCreator, libconfig::Setting& settings, ObjectId areaId, void* state = nullptr);
			exported virtual ~IProcessorNode();

			exported void sendMessage(ObjectId receipientUserId, Utilities::DataStream&& notification);
			exported void sendMessageToBroker(ObjectId targetAreaId, Utilities::DataStream&& message);
			exported Utilities::DataStream createMessage(uint8 category, uint8 type);

		protected:
			Utilities::SQLDatabase::Connection::Parameters dbParameters;
			Utilities::Net::RequestServer requestServer;
			ObjectId areaId;
			Utilities::Net::TCPConnection* brokerNode;
			std::map<ObjectId, std::vector<Utilities::Net::TCPConnection*>> authenticatedClients;
			std::mutex clientsLock;
			word workers;
			void* state;
			HandlerCreator handlerCreator;
			ContextCreator contextCreator;
			std::shared_ptr<IDBContext> emptyDB;
			std::vector<std::shared_ptr<IDBContext>> dbConnections;

			virtual void onConnect(Utilities::Net::TCPConnection& client);
			virtual void onDisconnect(Utilities::Net::TCPConnection& client);
			virtual Utilities::Net::RequestServer::RequestResult onRequest(Utilities::Net::TCPConnection& client, word workerNumber, uint8 requestCategory, uint8 requestMethod, Utilities::DataStream& parameters, Utilities::DataStream& response);

			static void onConnectDispatch(Utilities::Net::TCPConnection& client, void* state);
			static void onDisconnectDispatch(Utilities::Net::TCPConnection& client, void* state);
			static Utilities::Net::RequestServer::RequestResult onRequestDispatch(Utilities::Net::TCPConnection& client, void* state, word workerNumber, uint8 requestCategory, uint8 requestMethod, Utilities::DataStream& parameters, Utilities::DataStream& response);
	};
}
