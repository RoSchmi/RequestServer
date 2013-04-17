#include <algorithm>
#include <cstring>

#include "CacheProvider.h"

using namespace Utilities::SQLDatabase;
using namespace GameServer::Objects;
using namespace GameServer;

ICacheProvider::ICacheProvider(Size size, const Connection& connection) {
	this->size = size;
	this->locationIndex = new IMap*[size.width * size.height];
	memset(this->locationIndex, 0, size.width * size.height * sizeof(IMap*));
}

ICacheProvider::~ICacheProvider() {
	delete [] this->locationIndex;
	//need to delete all cached objects too.
}

uint64 ICacheProvider::locationToOffset(Location location) {
	return this->size.width * static_cast<uint64>(location.y) + static_cast<uint64>(location.x);
}

uint64 ICacheProvider::locationToOffset(float64 x, float64 y) {
	return this->size.width * static_cast<uint64>(y) + static_cast<uint64>(x);
}

uint64 ICacheProvider::locationToOffsetInt(uint64 x, uint64 y) {
	return this->size.width * y + x;
}

IMap* ICacheProvider::getAtLocation(uint64 offset) {
	if (offset < this->size.width * this->size.height)
		return this->locationIndex[offset];
	else
		return nullptr;
}

void ICacheProvider::remove(IObject* object) {
	this->idIndex.erase(object->id);
	this->ownerIndex[object->ownerId].erase(object->id);
}

void ICacheProvider::remove(IMap* object) {
	this->remove(static_cast<IObject*>(object));

	for (float64 x = object->x; x <= object->x + object->width; x++)
		for (float64 y = object->y; y <= object->y + object->height; y++)
			this->locationIndex[this->locationToOffset(x, y)] = nullptr;
}

void ICacheProvider::add(IObject* object) {
	this->idIndex[object->id] = object;
	this->ownerIndex[object->ownerId][object->id] = object;
}

void ICacheProvider::add(IMap* object) {
	this->add(static_cast<IObject*>(object));

	for (float64 x = object->x; x < object->x + object->width; x++)
		for (float64 y = object->y; y < object->y + object->height; y++)
			this->locationIndex[this->locationToOffset(x, y)] = object;
}

IObject* ICacheProvider::getById(uint64 searchId) {
	IObject* result = this->idIndex[searchId];
	
	return result;
}

std::map<uint64, IMap*> ICacheProvider::getInArea(Location location, Size size) {
	std::map<uint64, IMap*> result; //we use a map to make it easy for large objects to only be added once
				
	for (location.x += size.width; size.width > 0; location.x--, size.width--) {
		for (location.y += size.height; size.height > 0; location.y--, size.height--) {
			IMap* current = this->locationIndex[this->locationToOffset(location)];
			if (current) {
				result[current->id] = current;
			}
		}
	}

	return result;
}

bool ICacheProvider::isAreaEmpty(Location location, Size size) {
	Location end = location;
	end.x += size.width;
	end.y += size.height;
	
	for (; location.x < end.x; location.x++)
		for (location.y = end.y - size.height; location.y < end.y; location.y++)
			if (this->locationIndex[this->locationToOffset(location)])
				return false;

	return true;
}

IMap* ICacheProvider::getByLocation(Location searchLocation) {
	return this->getByLocation(searchLocation.x, searchLocation.y);
}

IMap* ICacheProvider::getByLocation(float64 x, float64 y) {
	return this->getByLocationInt(static_cast<uint64>(x), static_cast<uint64>(y));
}

IMap* ICacheProvider::getByLocationInt(uint64 x, uint64 y) {
	IMap* result = this->locationIndex[this->locationToOffsetInt(x, y)];

	return result;
}

bool ICacheProvider::isLocationInLOS(Location location, uint64 ownerId, uint32 radius) {
	for (float64 x = location.x - radius; x <= location.x + radius; ++x) {
		for (float64 y = location.y - radius; y <= location.y + radius; ++y) {
			IMap* current = this->locationIndex[this->locationToOffset(x, y)];
			if (current && current->ownerId == ownerId) {
				return true;
			}
		}
	}

	return false;
}

std::map<uint64, IObject*> ICacheProvider::getByOwner(uint64 ownerId) {
	std::map<uint64, IObject*> ownerMap = this->ownerIndex[ownerId];

	return ownerMap;
}

std::map<uint64, IMap*> ICacheProvider::getInOwnerLOS(uint64 ownerId, uint32 radius) {
	std::map<uint64, IObject*> ownerObjects = this->ownerIndex[ownerId];

	float64 x;
	float64 y;
	std::map<uint64, IMap*> result;
	for (auto i : ownerObjects) {
		IMap* currentOwnerObject = dynamic_cast<IMap*>(i.second);
		if (!currentOwnerObject) 
			continue;

		for (x = currentOwnerObject->x >= radius ? currentOwnerObject->x - radius : 0; x <= currentOwnerObject->x + radius; ++x) {
			for (y = currentOwnerObject->y >= radius ? currentOwnerObject->y - radius : 0; y <= currentOwnerObject->y + radius; ++y) {
				IMap* currentTestObject = this->locationIndex[this->locationToOffset(x, y)];
				if (currentTestObject) {
					result[currentTestObject->id] = currentTestObject;
				}
			}
		}
	}

	return result;
}
