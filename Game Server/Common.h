#pragma once

#include <Utilities/Common.h>
#include <Utilities/DataStream.h>

namespace game_server {
	typedef uint16 result_code;
	typedef uint64 owner_id;
	typedef uint64 obj_id;
	typedef float64 coord;
	typedef uint32 size;

	class result_codes {
		public:
			static const result_code success = 0;
			static const result_code server_error = 1;
			static const result_code retry_later = 2;
			static const result_code invalid_request_type = 3;
			static const result_code invalid_parameters = 4;
			static const result_code invalid_server = 5;
			static const result_code string_isnt_utf8 = 6;
			static const result_code string_too_long = 7;
			static const result_code out_of_bounds = 8;
			static const result_code not_in_los = 9;
			static const result_code location_occupied = 10;
			static const result_code invalid_location = 11;
			static const result_code no_response = 12;
			static const result_code not_authenticated = 13;
	};
}