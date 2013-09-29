#pragma once

#include <map>
#include <atomic>
#include <thread>
#include <mutex>

#include <Utilities/TCPServer.h>

#include "Common.h"

namespace GameServer {
	class BrokerNode {
		std::vector<Utilities::Net::TCPConnection> clients;
		std::map<ObjectId, Utilities::Net::TCPConnection*> authenticated;
		Utilities::Net::TCPServer server;
		std::atomic<bool> running;
		std::thread ioWorker;
		std::mutex listLock;
		
		static void onClientConnect(Utilities::Net::TCPConnection&& client, void* state);

		void ioWorkerRun();
		void onMessage(Utilities::Net::TCPConnection& connection, Utilities::Net::TCPConnection::Message& message);

		public:
			exported BrokerNode(cstr port);
			exported ~BrokerNode();
	};
}
