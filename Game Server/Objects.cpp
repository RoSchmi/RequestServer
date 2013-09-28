#include "Objects.h"

using namespace GameServer::Objects;

IObject::IObject(uint8 objectType) {
	this->objectType = objectType;
	this->ownerId = 0;
}

IObject::~IObject() {

}

IMapObject::IMapObject(uint8 objectType) : IObject(objectType) {
	this->width = 1;
	this->height = 1;
	this->planetId = 0;
	this->x = 0;
	this->y = 0;
}

IMapObject::~IMapObject() {
	
}
