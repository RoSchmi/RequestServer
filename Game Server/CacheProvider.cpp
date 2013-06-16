#include "CacheProvider.h"

#include <algorithm>
#include <cstring>
#include <exception>

using namespace std;
using namespace Utilities::SQLDatabase;
using namespace GameServer;
using namespace GameServer::Objects;

ICacheProvider::ICacheProvider(Coordinate startX, Coordinate startY, Size width, Size height) {
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

	for (Coordinate x = object->x; x < object->x + object->width; x++)
		for (Coordinate y = object->y; y < object->y + object->height; y++)
			this->getByLocation(x, y) = nullptr;
}

void ICacheProvider::add(IObject* object) {
	this->idIndex[object->id] = object;
	this->ownerIndex[object->ownerId][object->id] = object;
}

void ICacheProvider::add(IMap* object) {
	this->add(static_cast<IObject*>(object));

	for (Coordinate x = object->x; x < object->x + object->width; x++)
		for (Coordinate y = object->y; y < object->y + object->height; y++)
			this->getByLocation(x, y) = object;
}

void ICacheProvider::updateLocation(IMap* object, Coordinate newX, Coordinate newY) {
	for (Coordinate x = object->x; x < object->x + object->width; x++)
		for (Coordinate y = object->y; y < object->y + object->height; y++)
			this->getByLocation(x, y) = nullptr;

	object->x = newX;
	object->y = newY;

	for (Coordinate x = object->x; x < object->x + object->width; x++)
		for (Coordinate y = object->y; y < object->y + object->height; y++)
			this->getByLocation(x, y) = object;
}

void ICacheProvider::clampToDimensions(Coordinate& startX, Coordinate& startY, Coordinate& endX, Coordinate& endY) {
	if (startX < this->startX) startX = this->startX;
	if (startY < this->startY) startY = this->startY;
	if (endX >= this->endX) endX = this->endX - 1;
	if (endY >= this->endY) endY = this->endY - 1;
}

IObject* ICacheProvider::getById(ObjectId searchId) {
	return this->idIndex[searchId];
}

IMap*& ICacheProvider::getByLocation(Coordinate x, Coordinate y) {
	return this->locationIndex[this->width * static_cast<ObjectId>(y - this->startY) + static_cast<ObjectId>(x - this->startX)];
}

map<ObjectId, IMap*> ICacheProvider::getInArea(Coordinate x, Coordinate y, Size width, Size height) {
	map<ObjectId, IMap*> result; //we use a map to make it easy for large objects to only be added once
	Coordinate endX = x + width;
	Coordinate endY = y + height;

	this->clampToDimensions(x, y, endX, endY);
				
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

bool ICacheProvider::isAreaEmpty(Coordinate x, Coordinate y, Size width, Size height) {
	Coordinate endX = x + width;
	Coordinate endY = y + height;

	this->clampToDimensions(x, y, endX, endY);
	
	for (; x < endX; x++)
		for (y = endY - height; y < endY; y++)
			if (this->getByLocation(x, y))
				return false;

	return true;
}

bool ICacheProvider::isLocationInLOS(Coordinate x, Coordinate y, OwnerId ownerId, Size radius) {
	Coordinate endX = x + radius;
	Coordinate endY = y + radius;
	Coordinate startX = x - radius;
	Coordinate startY = y - radius;

	this->clampToDimensions(startX, startY, endX, endY);

	for (x = startX; x < endX; x++) {
		for (y = startY; y < endY; y++) {
			IMap* current = this->getByLocation(x, y);
			if (current && current->ownerId == ownerId) {
				return true;
			}
		}
	}

	return false;
}

bool ICacheProvider::isLocationInBounds(Coordinate x, Coordinate y, Size width, Size height) {
	return x >= this->startX && y >= this->startY && x + width <= this->endX && y + height <= this->endY;
}

map<OwnerId, IObject*> ICacheProvider::getByOwner(OwnerId ownerId) {
	return this->ownerIndex[ownerId];
}

map<OwnerId, IMap*> ICacheProvider::getInOwnerLOS(OwnerId ownerId, Size radius) {
	map<OwnerId, IObject*> ownerObjects = this->ownerIndex[ownerId];
	
	Coordinate startX, startY, endX, endY, x, y;
	map<OwnerId, IMap*> result;
	for (auto i : ownerObjects) {
		IMap* currentOwnerObject = dynamic_cast<IMap*>(i.second);
		if (!currentOwnerObject) 
			continue;

		startX = currentOwnerObject->x;
		startY = currentOwnerObject->y;
		endX = startX + radius;
		endY = startY + radius;

		this->clampToDimensions(startX, startY, endX, endY);

		for (x = startX; x < endX; ++x) {
			for (y = startY; y < endY; ++y) {
				IMap* currentTestObject = this->getByLocation(x, y);
				if (currentTestObject) {
					result[currentTestObject->id] = currentTestObject;
				}
			}
		}
	}

	return result;
}
