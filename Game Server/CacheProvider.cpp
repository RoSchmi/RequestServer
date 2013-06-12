#include <algorithm>
#include <cstring>
#include <exception>

#include "CacheProvider.h"

using namespace Utilities::SQLDatabase;
using namespace GameServer::Objects;
using namespace GameServer;
using namespace std;

ICacheProvider::ICacheProvider(float64 startX, float64 startY, uint32 width, uint32 height) {
	this->startX = startX;
	this->startY = startY;
	this->endX = startX + width;
	this->endY = startY + height;
	this->width = width;
	this->height = height;
	this->locationIndex = new IMap*[width * height];
	memset(this->locationIndex, 0, width * height * sizeof(IMap*));
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

	for (float64 x = object->x; x < object->x + object->width; x++)
		for (float64 y = object->y; y < object->y + object->height; y++)
			this->getByLocation(x, y) = nullptr;
}

void ICacheProvider::add(IObject* object) {
	this->idIndex[object->id] = object;
	this->ownerIndex[object->ownerId][object->id] = object;
}

void ICacheProvider::add(IMap* object) {
	this->add(static_cast<IObject*>(object));

	for (float64 x = object->x; x < object->x + object->width; x++)
		for (float64 y = object->y; y < object->y + object->height; y++)
			this->getByLocation(x, y) = object;
}

void ICacheProvider::updateLocation(IMap* object, float64 newX, float64 newY) {
	for (float64 x = object->x; x < object->x + object->width; x++)
		for (float64 y = object->y; y < object->y + object->height; y++)
			this->getByLocation(x, y) = nullptr;

	object->x = newX;
	object->y = newY;

	for (float64 x = object->x; x < object->x + object->width; x++)
		for (float64 y = object->y; y < object->y + object->height; y++)
			this->getByLocation(x, y) = object;
}

void ICacheProvider::moveTo(IMap* object, float64 x, float64 y) {
	for (float64 oldX = object->x; oldX < object->x + object->width; oldX++)
		for (float64 oldY = object->y; oldY < object->y + object->height; oldY++)
			this->getByLocation(oldX, oldY) = nullptr;

	for (float64 newX = x; newX < x + object->width; newX++)
		for (float64 newY = y; newY < y + object->height; newY++)
			this->getByLocation(newX, newY) = object;
}

IObject* ICacheProvider::getById(uint64 searchId) {
	return this->idIndex[searchId];
}

IMap*& ICacheProvider::getByLocation(float64 x, float64 y) {
	return this->locationIndex[this->width * static_cast<uint64>(y - this->startY) + static_cast<uint64>(x - this->startX)];
}

map<uint64, IMap*> ICacheProvider::getInArea(float64 x, float64 y, uint32 width, uint32 height) {
	map<uint64, IMap*> result; //we use a map to make it easy for large objects to only be added once
	float64 endX = x + width;
	float64 endY = y + height;
				
	for (; x < endX; x++) {
		for (y = endY - height; y < endY; y++) {
			IMap* current = this->getByLocation(x, y);
			if (current) {
				result[current->id] = current;
			}
		}
	}

	return result;
}

bool ICacheProvider::isAreaEmpty(float64 x, float64 y, uint32 width, uint32 height) {
	float64 endX = x + width;
	float64 endY = y + height;
	
	for (; x < endX; x++)
		for (y = endY - height; y < endY; y++)
			if (this->getByLocation(x, y))
				return false;

	return true;
}

bool ICacheProvider::isLocationInLOS(float64 x, float64 y, uint64 ownerId, uint32 radius) {
	float64 endX = x + radius;
	float64 endY = y + radius;
	x -= radius;
	y -= radius;

	for (; x < endX; x++) {
		for (y = endY - radius * 2; y < endY; y++) {
			IMap* current = this->getByLocation(x, y);
			if (current && current->ownerId == ownerId) {
				return true;
			}
		}
	}

	return false;
}

bool ICacheProvider::isLocationInBounds(float64 x, float64 y, uint32 width, uint32 height) {
	return x >= this->startX && y >= this->startY && x + width <= this->endX && y + height <= this->endY;
}

map<uint64, IObject*> ICacheProvider::getByOwner(uint64 ownerId) {
	return this->ownerIndex[ownerId];
}

map<uint64, IMap*> ICacheProvider::getInOwnerLOS(uint64 ownerId, uint32 radius) {
	map<uint64, IObject*> ownerObjects = this->ownerIndex[ownerId];

	float64 x;
	float64 y;
	map<uint64, IMap*> result;
	for (auto i : ownerObjects) {
		IMap* currentOwnerObject = dynamic_cast<IMap*>(i.second);
		if (!currentOwnerObject) 
			continue;

		for (x = currentOwnerObject->x >= radius ? currentOwnerObject->x - radius : 0; x < currentOwnerObject->x + radius; ++x) {
			for (y = currentOwnerObject->y >= radius ? currentOwnerObject->y - radius : 0; y < currentOwnerObject->y + radius; ++y) {
				IMap* currentTestObject = this->getByLocation(x, y);
				if (currentTestObject) {
					result[currentTestObject->id] = currentTestObject;
				}
			}
		}
	}

	return result;
}
