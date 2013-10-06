#pragma once

#include <Utilities/Common.h>
#include <Utilities/DataStream.h>

namespace game_server {
	typedef uint16 result_code;
	typedef uint64 owner_id;
	typedef uint64 obj_id;
	typedef float64 coord;
	typedef uint32 size;

	class base_handler {
		public:
			exported virtual ~base_handler() = 0;
			exported virtual result_code process() = 0;
			exported virtual void deserialize(util::data_stream& parameters) = 0;
			exported virtual void serialize(util::data_stream& response) = 0;
	};

	class result_codes {
		public:
			static const result_code SUCCESS = 0;
			static const result_code SERVER_ERROR = 1;
			static const result_code RETRY_LATER = 2;
			static const result_code INVALID_REQUEST_TYPE = 3;
			static const result_code INVALID_PARAMETERS = 4;
			static const result_code INVALID_SERVER = 5;
			static const result_code STRING_IS_NOT_UTF8 = 6;
			static const result_code STRING_TOO_LONG = 7;
			static const result_code OUT_OF_BOUNDS = 8;
			static const result_code NOT_IN_LOS = 9;
			static const result_code LOCATION_OCCUPIED = 10;
			static const result_code INVALID_LOCATION = 11;
			static const result_code NO_RESPONSE = 12;
	};
}