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
			
			exported Location() : planetId(0), x(0.0), y(0.0) { }
			exported Location(float64 x, float64 y) : planetId(0), x(x), y(y) { }
			exported Location(uint64 x, uint64 y) : planetId(0), x(static_cast<float64>(x)), y(static_cast<float64>(y)) { }
			exported Location(uint64 planetId, float64 x, float64 y) : planetId(planetId), x(x), y(y) { }
		};

		struct Size {
			uint32 width;
			uint32 height;
			
			exported Size() : width(1), height(1) { }
			exported Size(uint32 width, uint32 height) : width(width), height(height) { }
		};

		struct IObject : public Utilities::SQLDatabase::IDBObject  {
			exported IObject(uint8 objectType);
			exported virtual ~IObject();

			uint64 ownerId;
			uint8 objectType;
		};

		struct IMap : public IObject {
			exported IMap(uint8 objectType);
			exported virtual ~IMap();
			
			uint64 planetId;
			float64 x;
			float64 y;

			uint32 width;
			uint32 height;
		};
	}
}
