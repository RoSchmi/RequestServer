#pragma once

#include <Utilities/Common.h>
#include <Utilities/DataStream.h>
#include <Utilities/Socket.h>
#include <Utilities/Time.h>

#include "Objects.h"
#include "DBContext.h"

namespace GameServer {
	class IResultCode {
		public:
			static const uint16 SUCCESS = 0;
			static const uint16 SERVER_ERROR = 1;
			static const uint16 RETRY_LATER = 2;
			static const uint16 INVALID_REQUEST_TYPE = 3;
			static const uint16 INVALID_PARAMETERS = 4;
			static const uint16 INVALID_SERVER = 5;
			static const uint16 STRING_IS_NOT_UTF8 = 6;
			static const uint16 STRING_TOO_LONG = 7;
			static const uint16 OUT_OF_BOUNDS = 8;
			static const uint16 NOT_IN_LOS = 9;
			static const uint16 LOCATION_OCCUPIED = 10;
			static const uint16 INVALID_LOCATION = 11;
	};
	
	template<typename T> class BaseRequest {
		public:
			virtual uint16 process(uint64& userId, const uint8 ipAddress[Utilities::Net::Socket::ADDRESS_LENGTH], T& db) = 0;
			virtual void deserialize(Utilities::DataStream& parameters) = 0;
			virtual void serialize(Utilities::DataStream& response) = 0;
	};
}
