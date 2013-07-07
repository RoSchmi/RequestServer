#include "CacheProvider.h"

#include <algorithm>
#include <cstring>
#include <exception>

using namespace std;
using namespace Utilities::SQLDatabase;
using namespace GameServer;
using namespace GameServer::Objects;

ICacheProvider::ICacheProvider(Coordinate startX, Coordinate startY, Size width, Size height, Size losRadius) {
	this->startX = startX;
	this->startY = startY;
	this->endX = startX + width;
	this->endY = startY + height;
	this->width = width;
	this->height = height;
	this->losRadius = losRadius;
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
	if (this->idIndex.count(searchId) == 0)
		return nullptr;

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

bool ICacheProvider::isLocationInLOS(Coordinate x, Coordinate y, OwnerId ownerId) {
	Coordinate endX = x + this->losRadius;
	Coordinate endY = y + this->losRadius;
	Coordinate startX = x - this->losRadius;
	Coordinate startY = y - this->losRadius;

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

bool ICacheProvider::isUserPresent(ObjectId userId) {
	return this->idIndex.count(userId) != 0;
}

map<ObjectId, IObject*> ICacheProvider::getByOwner(OwnerId ownerId) {
	return this->ownerIndex[ownerId];
}

map<ObjectId, IMap*> ICacheProvider::getInOwnerLOS(OwnerId ownerId) {
	map<ObjectId, IObject*> ownerObjects = this->ownerIndex[ownerId];
	
	Coordinate startX, startY, endX, endY, x, y;
	map<ObjectId, IMap*> result;
	for (auto i : ownerObjects) {
		IMap* currentOwnerObject = dynamic_cast<IMap*>(i.second);
		if (!currentOwnerObject) 
			continue;

		startX = currentOwnerObject->x - this->losRadius;
		startY = currentOwnerObject->y - this->losRadius;
		endX = currentOwnerObject->x + this->losRadius;
		endY = currentOwnerObject->y + this->losRadius;

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

map<ObjectId, IMap*> ICacheProvider::getInOwnerLOS(OwnerId ownerId, Coordinate x, Coordinate y, Size width, Size height) {
	map<ObjectId, IMap*> result;

	for (auto i : this->getInOwnerLOS(ownerId)) {
		IMap* currentObject = dynamic_cast<IMap*>(i.second);
		if (!currentObject)
			continue;

		if (currentObject->x >= x && currentObject->y >= y && currentObject->x <= x + width && currentObject->y <= y + height)
			result[currentObject->id] = currentObject;
	}

	return result;
}

std::vector<ObjectId> ICacheProvider::getUsersWithLOSAt(Coordinate x, Coordinate y) {
	map<ObjectId, ObjectId> resultMap; //we use a map to make it easy for large objects to only be added once
	Coordinate endX = x + this->losRadius;
	Coordinate endY = y + this->losRadius;
	Coordinate startX = x - this->losRadius;
	Coordinate startY = y - this->losRadius;

	this->clampToDimensions(startX, startY, endX, endY);
				
	for (Coordinate thiX = startX; thiX < endX; thiX++) {
		for (Coordinate thisY = startY; thisY < endY; thisY++) {
			IMap* current = this->getByLocation(thiX, thisY);
			if (current) {
				resultMap[current->ownerId] = current->ownerId;
			}
		}
	}

	vector<ObjectId> result;
	for (auto i : resultMap)
		result.push_back(i.first);

	return result;
}