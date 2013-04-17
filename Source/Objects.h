#pragma once

#include <string>
#include <Utilities/Time.h>
#include <Utilities/Array.h>
#include <Utilities/SQLDatabase.h>

namespace GameServer {
	namespace Objects {
		struct Location {
			uint64 planetId;
			float64 x;
			float64 y;
			
			Location() : planetId(0), x(0.0), y(0.0) { }
			Location(float64 x, float64 y) : planetId(0), x(x), y(y) { }
			Location(uint64 x, uint64 y) : planetId(0), x(static_cast<float64>(x)), y(static_cast<float64>(y)) { }
			Location(uint64 planetId, float64 x, float64 y) : planetId(planetId), x(x), y(y) { }
		};

		struct Size {
			uint32 width;
			uint32 height;
			
			Size() : width(1), height(1) { }
			Size(uint32 width, uint32 height) : width(width), height(height) { }
		};

		struct IObject : public Utilities::SQLDatabase::IDBObject  {
			IObject(uint8 objectType);
			virtual ~IObject();

			uint64 ownerId;
			uint8 objectType;
		};

		struct IMap : public IObject {
			IMap(uint8 objectType);
			virtual ~IMap();
			
			uint64 planetId;
			float64 x;
			float64 y;

			uint32 width;
			uint32 height;
		};
	}
}
