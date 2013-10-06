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

#include "IDBContext.h"
#include "Common.h"

namespace GameServer {
	class IProcessorNode {
		public:
			typedef std::shared_ptr<BaseHandler>(*HandlerCreator)(word workerNumber, uint8 category, uint8 method, ObjectId& userId, std::shared_ptr<IDBContext>& db, uint16& errorCode, void* state);
			typedef std::shared_ptr<IDBContext>(*ContextCreator)(word workerNumber, util::sql::connection::parameters& parameters, void* state);

			exported IProcessorNode(HandlerCreator handlerCreator, ContextCreator contextCreator, libconfig::Setting& settings, ObjectId areaId, void* state = nullptr);
			exported virtual ~IProcessorNode();

			exported void sendMessage(ObjectId receipientUserId, util::data_stream&& notification);
			exported void sendMessageToBroker(ObjectId targetAreaId, util::data_stream&& message);
			exported util::data_stream createMessage(uint8 category, uint8 type);

		protected:
			util::sql::connection::parameters dbParameters;
			util::net::request_server requestServer;
			ObjectId areaId;
			util::net::tcp_connection* brokerNode;
			std::map<ObjectId, std::vector<util::net::tcp_connection*>> authenticatedClients;
			std::mutex clientsLock;
			word workers;
			void* state;
			HandlerCreator handlerCreator;
			ContextCreator contextCreator;
			std::shared_ptr<IDBContext> emptyDB;
			std::vector<std::shared_ptr<IDBContext>> dbConnections;

			virtual void onConnect(util::net::tcp_connection& client);
			virtual void onDisconnect(util::net::tcp_connection& client);
			virtual util::net::request_server::request_result onRequest(util::net::tcp_connection& client, word workerNumber, uint8 requestCategory, uint8 requestMethod, util::data_stream& parameters, util::data_stream& response);

			static void onConnectDispatch(util::net::tcp_connection& client, void* state);
			static void onDisconnectDispatch(util::net::tcp_connection& client, void* state);
			static util::net::request_server::request_result onRequestDispatch(util::net::tcp_connection& client, void* state, word workerNumber, uint8 requestCategory, uint8 requestMethod, util::data_stream& parameters, util::data_stream& response);
	};
}
