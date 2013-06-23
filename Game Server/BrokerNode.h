#pragma once

#include <map>

#include <Utilities/TCPServer.h>

#include "Common.h"

using namespace std;
using namespace Utilities;
using namespace Utilities::Net;

namespace GameServer {
	class BrokerNode {
		std::map<ObjectId, Utilities::Net::TCPConnection*> servers;
		Utilities::Net::TCPServer server;
		
		static void* onClientConnect(Utilities::Net::TCPConnection& connection, void* serverState, const uint8 clientAddress[Net::Socket::ADDRESS_LENGTH]);
		static void onRequestReceived(Utilities::Net::TCPConnection& connection, void* state, Utilities::Net::TCPConnection::Message& message);

		public:
			exported BrokerNode(cstr port);
			exported ~BrokerNode();
			 
			exported void run();
	};
}
