#include <algorithm>
#include <cstring>

#include "CacheProvider.h"

using namespace Utilities::SQLDatabase;
using namespace GameServer::Objects;
using namespace GameServer;

ICacheProvider::ICacheProvider(Size size) {
	this->size = size;
	this->locationIndex = new IMap*[size.width * size.height];
	memset(this->locationIndex, 0, size.width * size.height * sizeof(IMap*));
}

ICacheProvider::~ICacheProvider() {
	delete [] this->locationIndex;

	for (auto i : this->idIndex)
		delete i.second;
}

void ICacheProvider::remove(IObject* object) {
	this->idIndex.erase(object->id);
	this->ownerIndex[object->ownerId].erase(object->id);
}

void ICacheProvider::remove(IMap* object) {
	this->remove(static_cast<IObject*>(object));

	for (float64 x = object->x; x <= object->x + object->width; x++)
		for (float64 y = object->y; y <= object->y + object->height; y++)
			this->locationIndex[this->size.width * static_cast<uint64>(y) + static_cast<uint64>(x)] = nullptr;
}

void ICacheProvider::add(IObject* object) {
	this->idIndex[object->id] = object;
	this->ownerIndex[object->ownerId][object->id] = object;
}

void ICacheProvider::add(IMap* object) {
	this->add(static_cast<IObject*>(object));

	for (float64 x = object->x; x <= object->x + object->width; x++)
		for (float64 y = object->y; y <= object->y + object->height; y++)
			this->locationIndex[this->size.width * static_cast<uint64>(y) + static_cast<uint64>(x)] = object;
}

IObject* ICacheProvider::getById(uint64 searchId) {
	return this->idIndex[searchId];
}

std::map<uint64, IMap*> ICacheProvider::getInArea(Location location, Size size) {
	std::map<uint64, IMap*> result; //we use a map to make it easy for large objects to only be added once
				
	for (location.x += size.width; size.width > 0; location.x--, size.width--) {
		for (location.y += size.height; size.height > 0; location.y--, size.height--) {
			IMap* current = this->getByLocation(location);
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
			if (this->getByLocation(location))
				return false;

	return true;
}

IMap* ICacheProvider::getByLocation(Location searchLocation) {
	return this->getByLocation(searchLocation.x, searchLocation.y);
}

IMap* ICacheProvider::getByLocation(float64 x, float64 y) {
	return this->locationIndex[this->size.width * static_cast<uint64>(y) + static_cast<uint64>(x)];
}

bool ICacheProvider::isLocationInLOS(Location location, uint64 ownerId, uint32 radius) {
	for (float64 x = location.x - radius; x <= location.x + radius; ++x) {
		for (float64 y = location.y - radius; y <= location.y + radius; ++y) {
			IMap* current = this->getByLocation(x, y);
			if (current && current->ownerId == ownerId) {
				return true;
			}
		}
	}

	return false;
}

std::map<uint64, IObject*> ICacheProvider::getByOwner(uint64 ownerId) {
	return this->ownerIndex[ownerId];
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
				IMap* currentTestObject = this->getByLocation(x, y);
				if (currentTestObject) {
					result[currentTestObject->id] = currentTestObject;
				}
			}
		}
	}

	return result;
}
