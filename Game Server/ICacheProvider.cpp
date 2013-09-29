#include "ICacheProvider.h"

#include <algorithm>
#include <cstring>

using namespace std;
using namespace Utilities;
using namespace Utilities::SQLDatabase;
using namespace GameServer;
using namespace Objects;

ICacheProvider::ICacheProvider(coord startX, coord startY, size width, size height, size losRadius) {
	this->startX = startX;
	this->startY = startY;
	this->endX = startX + width;
	this->endY = startY + height;
	this->width = width;
	this->height = height;
	this->losRadius = losRadius;
	this->locationIndex = new IMapObject*[width * height];
	memset(this->locationIndex, 0, width * height * sizeof(IMapObject*));
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

void ICacheProvider::remove(IMapObject* object) {
	this->remove(static_cast<IObject*>(object));

	for (coord x = object->x; x < object->x + object->width; x++)
		for (coord y = object->y; y < object->y + object->height; y++)
			this->getByLocation(x, y) = nullptr;
}

void ICacheProvider::add(IObject* object) {
	this->idIndex[object->id] = object;
	this->ownerIndex[object->ownerId][object->id] = object;
}

void ICacheProvider::add(IMapObject* object) {
	this->add(static_cast<IObject*>(object));

	for (coord x = object->x; x < object->x + object->width; x++)
		for (coord y = object->y; y < object->y + object->height; y++)
			this->getByLocation(x, y) = object;
}

void ICacheProvider::updateLocation(IMapObject* object, coord newX, coord newY) {
	for (coord x = object->x; x < object->x + object->width; x++)
		for (coord y = object->y; y < object->y + object->height; y++)
			this->getByLocation(x, y) = nullptr;

	object->x = newX;
	object->y = newY;

	for (coord x = object->x; x < object->x + object->width; x++)
		for (coord y = object->y; y < object->y + object->height; y++)
			this->getByLocation(x, y) = object;
}

void ICacheProvider::clampToDimensions(coord& startX, coord& startY, coord& endX, coord& endY) {
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

IMapObject*& ICacheProvider::getByLocation(coord x, coord y) {
	return this->locationIndex[this->width * static_cast<ObjectId>(y - this->startY) + static_cast<ObjectId>(x - this->startX)];
}

unordered_set<IMapObject*> ICacheProvider::getInArea(coord x, coord y, size width, size height) {
	unordered_set<IMapObject*> result;
	coord endX = x + width;
	coord endY = y + height;

	this->clampToDimensions(x, y, endX, endY);
				
	for (; x < endX; x++) {
		for (y = endY - height; y < endY; y++) {
			IMapObject* current = this->getByLocation(x, y);
			if (current)
				result.insert(current);
		}
	}

	return result;
}

bool ICacheProvider::isAreaEmpty(coord x, coord y, size width, size height) {
	coord endX = x + width;
	coord endY = y + height;

	this->clampToDimensions(x, y, endX, endY);
	
	for (; x < endX; x++)
		for (y = endY - height; y < endY; y++)
			if (this->getByLocation(x, y))
				return false;

	return true;
}

bool ICacheProvider::isLocationInLOS(coord x, coord y, OwnerId ownerId) {
	coord endX = x + this->losRadius;
	coord endY = y + this->losRadius;
	coord startX = x - this->losRadius;
	coord startY = y - this->losRadius;

	this->clampToDimensions(startX, startY, endX, endY);

	for (x = startX; x < endX; x++) {
		for (y = startY; y < endY; y++) {
			IMapObject* current = this->getByLocation(x, y);
			if (current && current->ownerId == ownerId) {
				return true;
			}
		}
	}

	return false;
}

bool ICacheProvider::isLocationInBounds(coord x, coord y, size width, size height) {
	return x >= this->startX && y >= this->startY && x + width <= this->endX && y + height <= this->endY;
}

bool ICacheProvider::isUserPresent(ObjectId userId) {
	return this->idIndex.count(userId) != 0;
}

const unordered_map<ObjectId, IObject*> ICacheProvider::getByOwner(OwnerId ownerId) {
	return this->ownerIndex[ownerId];
}

unordered_set<IMapObject*> ICacheProvider::getInOwnerLOS(OwnerId ownerId) {
	unordered_map<ObjectId, IObject*> ownerObjects = this->ownerIndex[ownerId];
	
	coord startX, startY, endX, endY, x, y;
	unordered_set<IMapObject*> result;
	for (auto i : ownerObjects) {
		IMapObject* currentOwnerObject = dynamic_cast<IMapObject*>(i.second);
		if (!currentOwnerObject) 
			continue;

		startX = currentOwnerObject->x - this->losRadius;
		startY = currentOwnerObject->y - this->losRadius;
		endX = currentOwnerObject->x + this->losRadius;
		endY = currentOwnerObject->y + this->losRadius;

		this->clampToDimensions(startX, startY, endX, endY);

		for (x = startX; x < endX; x++) {
			for (y = startY; y < endY; y++) {
				IMapObject* currentTestObject = this->getByLocation(x, y);
				if (currentTestObject)
					result.insert(currentTestObject);
			}
		}
	}

	return result;
}

unordered_set<IMapObject*> ICacheProvider::getInOwnerLOS(OwnerId ownerId, coord x, coord y, size width, size height) {
	unordered_set<IMapObject*> result;

	for (auto i : this->getInOwnerLOS(ownerId)) {
		IMapObject* currentObject = dynamic_cast<IMapObject*>(i);
		if (!currentObject)
			continue;

		if (currentObject->x >= x && currentObject->y >= y && currentObject->x <= x + width && currentObject->y <= y + height)
			result.insert(currentObject);
	}

	return result;
}

unordered_set<ObjectId> ICacheProvider::getUsersWithLOSAt(coord x, coord y) {
	unordered_set<ObjectId> result;
	coord endX = x + this->losRadius;
	coord endY = y + this->losRadius;
	coord startX = x - this->losRadius;
	coord startY = y - this->losRadius;

	this->clampToDimensions(startX, startY, endX, endY);
				
	for (coord thisX = startX; thisX < endX; thisX++) {
		for (coord thisY = startY; thisY < endY; thisY++) {
			IMapObject* current = this->getByLocation(thisX, thisY);
			if (current)
				result.insert(current->ownerId);
		}
	}

	return result;
}