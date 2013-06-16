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
			static const ResultCode SUCCESS = 0;
			static const ResultCode SERVER_ERROR = 1;
			static const ResultCode RETRY_LATER = 2;
			static const ResultCode INVALID_REQUEST_TYPE = 3;
			static const ResultCode INVALID_PARAMETERS = 4;
			static const ResultCode INVALID_SERVER = 5;
			static const ResultCode STRING_IS_NOT_UTF8 = 6;
			static const ResultCode STRING_TOO_LONG = 7;
			static const ResultCode OUT_OF_BOUNDS = 8;
			static const ResultCode NOT_IN_LOS = 9;
			static const ResultCode LOCATION_OCCUPIED = 10;
			static const ResultCode INVALID_LOCATION = 11;
	};
	
	template<typename T> class BaseRequest {
		public:
			virtual ResultCode process(ObjectId& userId, const uint8 ipAddress[Utilities::Net::Socket::ADDRESS_LENGTH], T& db) = 0;
			virtual void deserialize(Utilities::DataStream& parameters) = 0;
			virtual void serialize(Utilities::DataStream& response) = 0;
	};
}
